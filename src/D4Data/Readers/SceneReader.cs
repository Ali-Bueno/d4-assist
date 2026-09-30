using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Model;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>Reads SceneDefinition records: bounds, DRLG exits, nav cells and gameplay markers.</summary>
public sealed class SceneReader(SnoStore store, TypeSchema schema, MarkerReader markers, NavGridReader navGrid, bool allLayers, bool includeMap)
{
    private const string Scene = "SceneDefinition";
    private const string PathData = "AIPathData";
    private const string Cell = "AICellData";
    private const string CellBytes = "AICellBytes";
    private const string CellBytesIndex = "unk_c225718"; // AICellData: index into m_cellBytes (cells share blobs)

    // Marker-set layers holding gameplay actors (entrances, portals, checkpoints); others are art/lighting/themes.
    private static readonly string[] GameplayLayers = ["Game", "Shared"];

    public int Group => schema.SnoGroup(Scene);

    public SceneInfo Read(int sceneSno)
    {
        var meta = store.Meta(Group, sceneSno) ?? throw new InvalidDataException($"Scene {sceneSno} has no Meta.");
        var summary = ReadSummary(meta, sceneSno);

        var sceneMarkers = new List<MarkerInfo>();
        foreach (var layer in meta.Array(schema.Offset(Scene, "arLayers"), schema.Size("LayerData")))
        {
            int set = meta.I32(layer + schema.Offset("LayerData", "snoLayerMarkerset"));
            var name = LayerName(store.Name(set));
            if (allLayers || GameplayLayers.Contains(name, StringComparer.OrdinalIgnoreCase))
                markers.ReadLayer(set, name, Matrix4x4.Identity, 0, sceneMarkers);
        }

        return new SceneInfo(summary.Scene, summary.TileSize, summary.Bounds, summary.ExitMask, summary.Exits,
            ReadWalkability(meta, sceneSno, summary.TileSize), sceneMarkers);
    }

    /// <summary>Cheap facts only (tile size, exits, bounds) — no marker/walkability payload reads.</summary>
    /// <remarks>Used to build the candidate-scene set for the memory scanner over thousands of SNOs.</remarks>
    public SceneSummary ReadSummary(int sceneSno) =>
        ReadSummary(store.Meta(Group, sceneSno) ?? throw new InvalidDataException($"Scene {sceneSno} has no Meta."), sceneSno);

    private SceneSummary ReadSummary(SnoBlob meta, int sceneSno)
    {
        int exitMask = meta.I32(schema.Offset(Scene, "eDRLGTileType"));
        var tile = new[] { meta.F32(schema.Offset(Scene, "flTileX")), meta.F32(schema.Offset(Scene, "flTileY")) };
        return new SceneSummary(new SnoRef(sceneSno, store.Name(sceneSno)),
            tile, exitMask, Conventions.Exits(exitMask), AabbToBox(meta, schema.Offset(Scene, "aabbBounds")));
    }

    private Walkability? ReadWalkability(SnoBlob meta, int sceneSno, float[] tile)
    {
        var blob = schema.IsInPayload(Scene, "ptAIPathData") ? store.Payload(Group, sceneSno) : meta;
        if (blob is null) return null;
        // The array descriptor lives in Meta; its data offset points into the Payload.
        var (data, size) = meta.ArrayDescriptor(schema.Offset(Scene, "ptAIPathData"));
        if (data <= 0 || size < schema.Size(PathData)) return null;

        float width = blob.F32(data + schema.Offset(PathData, "m_cellWidth"));
        float height = blob.F32(data + schema.Offset(PathData, "m_cellHeight"));
        if (width <= 0 || height <= 0) return null; // e.g. filler tiles: path data present but empty
        var cells = new List<NavCell>();
        foreach (var c in blob.Array(data + schema.Offset(PathData, "m_cellData"), schema.Size(Cell)))
        {
            ulong id = blob.U64(c + schema.Offset(Cell, "m_cellId"));
            var local = AabbToBox(blob, c + schema.Offset(Cell, "aabbWalkableBounds"));
            if (local.Min[0] > local.Max[0]) continue; // empty-AABB sentinel: nothing walkable in this cell
            var (ix, iy, iz) = Conventions.DecodeCellId(id);
            var origin = new Vector3(ix * width, iy * width, iz * height);
            cells.Add(new NavCell($"0x{id:X}", new Box(
                ToArray(origin + AiToScene(local.Min)), ToArray(origin + AiToScene(local.Max)))));
        }

        // Voxel grids ship uncompressed for DRLG tiles; other scenes only carry the compressed m_cellBytes form.
        int grids = data + schema.Offset(PathData, "unk_5d4c076");
        var columns = blob.ArrayDescriptor(grids).Size > 0 ? navGrid.WalkableColumns(blob, grids, width, height)
            : SurfaceColumns(blob, data, width, height);
        if (columns is null) return new Walkability(width, height, cells, null, null);
        float area = columns.Count * NavGridReader.VoxelSize * NavGridReader.VoxelSize;
        return new Walkability(width, height, cells, area, includeMap ? NavGridReader.Map(columns, MapExtent(columns, tile)) : null);
    }

    /// <summary>Walkable columns from the compressed m_cellBytes (the surfaces an agent stands on); null when the scene has none.</summary>
    private HashSet<(int X, int Y)>? SurfaceColumns(SnoBlob blob, int data, float width, float height)
    {
        var blobs = blob.Array(data + schema.Offset(PathData, "m_cellBytes"), schema.Size(CellBytes)).ToList();
        if (blobs.Count == 0) return null;
        var surfaces = new Dictionary<int, ulong[]>(); // cells share blobs
        var columns = new HashSet<(int X, int Y)>();
        foreach (var c in blob.Array(data + schema.Offset(PathData, "m_cellData"), schema.Size(Cell)))
        {
            int index = blob.I32(c + schema.Offset(Cell, CellBytesIndex));
            if (index < 0 || index >= blobs.Count) continue;
            if (!surfaces.TryGetValue(index, out var grid))
            {
                try { grid = CellBytesDecoder.Surfaces(blob.ByteArray(blobs[index] + schema.Offset(CellBytes, "m_data"))); }
                catch (ArgumentOutOfRangeException) { grid = []; } // malformed blob: that cell stays unwalkable
                surfaces[index] = grid;
            }
            if (grid.Length == 0) continue;
            var (ix, iy, _) = Conventions.DecodeCellId(blob.U64(c + schema.Offset(Cell, "m_cellId")));
            navGrid.AddWalkableColumns(w => grid[w], ix, iy, width, height, columns);
        }
        return columns;
    }

    /// <summary>The tile footprint, grown to include walkable columns outside it.</summary>
    private static Box MapExtent(HashSet<(int X, int Y)> columns, float[] tile)
    {
        float v = NavGridReader.VoxelSize;
        float minX = columns.Count > 0 ? Math.Min(0, columns.Min(c => c.X) * v) : 0;
        float minY = columns.Count > 0 ? Math.Min(0, columns.Min(c => c.Y) * v) : 0;
        float maxX = columns.Count > 0 ? Math.Max(tile[0], (columns.Max(c => c.X) + 1) * v) : tile[0];
        float maxY = columns.Count > 0 ? Math.Max(tile[1], (columns.Max(c => c.Y) + 1) * v) : tile[1];
        return new Box([minX, minY, 0], [maxX, maxY, 0]);
    }

    /// <summary>AI cell AABBs are stored as (localY, localZ, localX); reorder to scene axes.</summary>
    private static Vector3 AiToScene(float[] ai) => new(ai[2], ai[0], ai[1]);

    private Box AabbToBox(SnoBlob blob, int aabb)
    {
        var center = blob.Vec3(aabb + schema.Offset("AABB", "wp"));
        var half = blob.Vec3(aabb + schema.Offset("AABB", "wvExt"));
        return new Box(ToArray(center - half), ToArray(center + half));
    }

    private static string LayerName(string? markerSetName)
    {
        if (markerSetName is null) return "";
        int open = markerSetName.LastIndexOf('('), close = markerSetName.LastIndexOf(')');
        return open >= 0 && close > open ? markerSetName[(open + 1)..close] : markerSetName;
    }

    private static float[] ToArray(Vector3 v) => [v.X, v.Y, v.Z];
}
