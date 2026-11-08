using System.Numerics;
using D4Assist.Navigation;
using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Assist.Game;

/// <summary>A traversal end a scene places (a jump, duck-under or slide gizmo, or where one lands), in the scene's own axes.</summary>
public readonly record struct TraversalPoint(Vector3 Local, bool Slide = false);

/// <summary>
/// Paths over a fixed-grid world (the open world: 1220 scene chunks of 96 units, research/dungeon-data.md §3; a story area's
/// static scenes) from the offline surfaces of the chunks around the walk (SurfacePathfinder), joined across gaps by the
/// scenes' traversals. Thread-safe; scene surfaces and traversals are kept once read.
/// </summary>
/// <param name="chunks">The scene placements of a world by its CoreTOC name; empty for a world without a fixed grid.</param>
/// <param name="surfaces">A scene's surface voxels, scene-local (SceneReader.SurfaceVoxels); null when it has none.</param>
/// <param name="traversals">A scene's traversal ends, scene-local; null for none.</param>
/// <param name="worldTraversals">The traversal ends a world's own marker layers place, by its CoreTOC name, in world space: the
/// Darkened Way's jump to the wounded knight and its ladder down from the lift sit in <c>Frac_Underworld (…)</c> sets, in no
/// scene (research/dungeon-data.md §4.1c); null for none.</param>
public sealed class OverworldPaths(
    Func<string, IReadOnlyList<SceneChunkInfo>> chunks, Func<int, IReadOnlyList<Vector3>?> surfaces,
    Func<int, IReadOnlyList<TraversalPoint>>? traversals = null, Func<string, IReadOnlyList<TraversalPoint>>? worldTraversals = null)
{
    /// <summary>The chunks searched reach this far beyond the box of start and target, room for a detour.</summary>
    public const float MarginUnits = 150f;

    /// <summary>Longer walks aren't searched (the map grows with the square of the distance).</summary>
    public const float MaxDistanceUnits = 1500f;

    private const float ChunkUnits = 96f;

    // A traversal's gizmos (Traversal_Gizmo_Jump, _DuckUnder, _Slide...) and the spots it lands at (research/dungeon-data.md §4.1c).
    private const string TraversalGizmoPrefix = "Traversal_Gizmo_";
    private const string TraversalSlideName = "Traversal_Gizmo_Slide";
    private const string TraversalExitName = "MarkerLocation_TraversalExit";
    private const string MarkerSetType = "MarkerSetDefinition", ActorDefinitionType = "ActorDefinition";

    /// <summary>Paths over the game data's scene surfaces and traversals; every game-data read holds <paramref name="gate"/>.</summary>
    public static OverworldPaths FromGameData(SnoStore store, TypeSchema schema, object gate)
    {
        var worlds = new WorldReader(store, schema);
        var scenes = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: false);
        // Traversals sit in any marker layer ("Game", "Traversals"), as the jumps of JumpLinkCollector.
        var traversals = new TileActorReader(
            new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: true, includeMap: false),
            sno => IsTraversal(store.Name(sno)));
        var chunks = new Dictionary<string, IReadOnlyList<SceneChunkInfo>>();
        // A world's own marker layers ("Frac_Underworld (QST_Frac_Underworld_02_JumpTraversal)") place traversals in world space.
        var markers = new MarkerReader(store, schema);
        int markerSets = schema.SnoGroup(MarkerSetType), actors = schema.SnoGroup(ActorDefinitionType);
        var worldTraversals = new Dictionary<string, IReadOnlyList<TraversalPoint>>();
        return new OverworldPaths(
            worldName =>
            {
                lock (gate)
                {
                    if (chunks.TryGetValue(worldName, out var known)) return known;
                    int? world;
                    try { world = store.Resolve(worlds.Group, worldName); }
                    catch (ArgumentException) { world = null; }
                    return chunks[worldName] = world is null ? [] : worlds.Read(world.Value).Chunks;
                }
            },
            scene => { lock (gate) return scenes.SurfaceVoxels(scene); },
            scene =>
            {
                lock (gate)
                    return [.. traversals.In(scene).Select(t => new TraversalPoint(t.Local, store.Name(t.ActorSno) == TraversalSlideName))];
            },
            worldName =>
            {
                lock (gate)
                {
                    if (worldTraversals.TryGetValue(worldName, out var known)) return known;
                    return worldTraversals[worldName] = [.. store.EntriesWithPrefix(markerSets, worldName + " (")
                        .SelectMany(set => markers.ReadAll(set.Id))
                        .Where(m => m.Group == actors && IsTraversal(store.Name(m.Sno)))
                        .Select(m => new TraversalPoint(m.Position, store.Name(m.Sno) == TraversalSlideName))];
                }
            });
    }

    private static bool IsTraversal(string? name) =>
        name is not null && (name.StartsWith(TraversalGizmoPrefix, StringComparison.Ordinal) || name == TraversalExitName);

    private readonly object _gate = new();
    private readonly Dictionary<int, IReadOnlyList<Vector3>?> _scenes = [];
    private readonly Dictionary<int, IReadOnlyList<TraversalPoint>> _traversals = [];

    /// <summary>A new search reuses the last map while its start and target lie this far inside the map's box: building the map
    /// took most of each search (0.2-1.3 s and 67-475 MB, 40-1400 units, Temis 2026-11-07), and the guide searches again each
    /// time the player strays off the path, every few seconds in a helltide fight.</summary>
    public const float ReuseInsideUnits = 50f;

    private sealed record MapBox(string World, float MinX, float MinY, float MaxX, float MaxY, int Doors, SurfacePathfinder.SurfaceMap Map)
    {
        public bool Holds(Vector2 p) =>
            p.X >= MinX + ReuseInsideUnits && p.X <= MaxX - ReuseInsideUnits && p.Y >= MinY + ReuseInsideUnits && p.Y <= MaxY - ReuseInsideUnits;
    }

    private MapBox? _last;

    /// <summary>How many searches built a map, and how many reused the last one (for the log and the tests).</summary>
    public (int Built, int Reused) MapUse { get; private set; }

    /// <summary>Lets go of the last map (the guide stopped), so an idle app doesn't hold it.</summary>
    public void Forget()
    {
        lock (_gate) _last = null;
    }

    /// <summary>A closed door bars the floor within this height of its base: floors stacked in one column lie farther apart
    /// (BreakableProps.SameFloorUnits).</summary>
    public const float DoorFloorUnits = BreakableProps.SameFloorUnits;

    /// <summary>The route's points (SurfacePathfinder.Find), or null when there is none or the walk is too long.</summary>
    /// <param name="doors">Closed doors the player can't open yet (LockedDoors.Walls): their floor is no way. The Darkened Way's
    /// invisible doors stand on the straight way from Lilith's petal down to the Ancient City (tester 2026-10-31,
    /// research/dungeon-data.md §4.1c).</param>
    public IReadOnlyList<Vector2>? Find(string worldName, Vector3 from, Vector2 to, IReadOnlyList<DoorWall>? doors = null)
    {
        var start = new Vector2(from.X, from.Y);
        if (Vector2.Distance(start, to) > MaxDistanceUnits) return null;
        int doorKey = DoorKey(doors);
        MapBox? last;
        lock (_gate) last = _last;
        if (last is not null && last.World == worldName && last.Doors == doorKey && last.Holds(start) && last.Holds(to))
        {
            lock (_gate) MapUse = (MapUse.Built, MapUse.Reused + 1);
            return SurfacePathfinder.Find(last.Map, start, to, from.Z);
        }
        float minX = MathF.Min(start.X, to.X) - MarginUnits, maxX = MathF.Max(start.X, to.X) + MarginUnits;
        float minY = MathF.Min(start.Y, to.Y) - MarginUnits, maxY = MathF.Max(start.Y, to.Y) + MarginUnits;
        var voxels = new List<Vector3>();
        var ends = new List<(Vector3 At, bool Slide)>();
        lock (_gate)
            foreach (var chunk in chunks(worldName))
            {
                if (chunk.Position.X + ChunkUnits <= minX || chunk.Position.X >= maxX || chunk.Position.Y + ChunkUnits <= minY || chunk.Position.Y >= maxY) continue;
                if (!_scenes.TryGetValue(chunk.SceneSno, out var local)) _scenes[chunk.SceneSno] = local = surfaces(chunk.SceneSno);
                if (local is null) continue;
                foreach (var v in local) voxels.Add(chunk.Position + Vector3.Transform(v, chunk.Rotation));
                if (traversals is null) continue;
                if (!_traversals.TryGetValue(chunk.SceneSno, out var points)) _traversals[chunk.SceneSno] = points = traversals(chunk.SceneSno);
                foreach (var p in points) ends.Add((chunk.Position + Vector3.Transform(p.Local, chunk.Rotation), p.Slide));
            }
        if (worldTraversals is not null)
            foreach (var p in worldTraversals(worldName))
                if (p.Local.X >= minX && p.Local.X <= maxX && p.Local.Y >= minY && p.Local.Y <= maxY) ends.Add((p.Local, p.Slide));
        if (doors is { Count: > 0 })
        {
            var barred = BarredCells(doors);
            voxels.RemoveAll(v => barred.TryGetValue(((int)MathF.Floor(v.X), (int)MathF.Floor(v.Y)), out var floors)
                && floors.Exists(z => MathF.Abs(v.Z - z) <= DoorFloorUnits));
        }
        if (SurfacePathfinder.SurfaceMap.Create(voxels, Links(ends)) is not { } map) return null;
        lock (_gate)
        {
            _last = new MapBox(worldName, minX, minY, maxX, maxY, doorKey, map);
            MapUse = (MapUse.Built + 1, MapUse.Reused);
        }
        return SurfacePathfinder.Find(map, start, to, from.Z);
    }

    // The same closed doors give the same key: a door that opens or locks builds the map anew.
    private static int DoorKey(IReadOnlyList<DoorWall>? doors)
    {
        if (doors is not { Count: > 0 }) return 0;
        int key = doors.Count;
        foreach (var door in doors)
        {
            int cells = 0;
            foreach (var cell in door.Cells) cells += cell.GetHashCode(); // order-free
            key = HashCode.Combine(key, door.Z, door.Cells.Count, cells);
        }
        return key;
    }

    /// <summary>Each door's cells and one cell round them, by the heights of the doors there: the player's body doesn't pass a
    /// cell from a door, and the Darkened Way's invisible doors stand a cell apart across the ramp they shut (tester 2026-11-01).</summary>
    private static Dictionary<(int X, int Y), List<float>> BarredCells(IReadOnlyList<DoorWall> doors)
    {
        var barred = new Dictionary<(int X, int Y), List<float>>();
        foreach (var door in doors)
            foreach (var (x, y) in door.Cells)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (!barred.TryGetValue((x + dx, y + dy), out var floors)) barred[(x + dx, y + dy)] = floors = [];
                        if (!floors.Contains(door.Z)) floors.Add(door.Z);
                    }
        return barred;
    }

    /// <summary>Every two traversal ends within JumpLinkCollector.MaxPairUnits, a slide one way down (not nearest-only pairing:
    /// research/dungeon-data.md §4.1c).</summary>
    public static IReadOnlyList<SurfaceLink> Links(IReadOnlyList<(Vector3 At, bool Slide)> ends)
    {
        var links = new List<SurfaceLink>();
        for (int i = 0; i < ends.Count; i++)
            for (int j = i + 1; j < ends.Count; j++)
            {
                var (a, b) = ends[i].At.Z >= ends[j].At.Z ? (ends[i].At, ends[j].At) : (ends[j].At, ends[i].At);
                if (Vector2.Distance(new(a.X, a.Y), new(b.X, b.Y)) <= JumpLinkCollector.MaxPairUnits)
                    links.Add(new SurfaceLink(a, b, OneWay: ends[i].Slide || ends[j].Slide));
            }
        return links;
    }
}
