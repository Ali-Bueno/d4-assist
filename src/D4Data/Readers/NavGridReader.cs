using D4Data.Format;
using D4Data.Model;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>Decodes the per-cell walkable voxel grids of AIPathData into walkable columns (top-down).</summary>
/// <remarks>Layout recovered empirically (104 tiles): u64 word = 4x4x4 brick, one nibble per column, 4 levels per
/// nibble; words ordered level-layer, then brick row (Y), then brick column (X). See research/dungeon-data.md.</remarks>
public sealed class NavGridReader(TypeSchema schema)
{
    private const string Grid = "Type_5f1859b4";

    /// <summary>Voxel edge in world units (empirical: 64 columns span the 32-unit cell).</summary>
    public const float VoxelSize = 0.5f;

    /// <summary>Resolution of the optional ASCII map, in world units per character.</summary>
    public const float MapResolution = 1f;

    private const int BrickEdge = 4;
    private const int ColumnsPerNibbleRow = 4;

    /// <summary>Walkable columns of every grid cell, in scene-local voxel coordinates (X, Y).</summary>
    public HashSet<(int X, int Y)> WalkableColumns(SnoBlob blob, int gridArray, float cellWidth, float cellHeight)
    {
        int columns = (int)(cellWidth / VoxelSize), bricksPerRow = columns / BrickEdge;
        int wordsPerLayer = bricksPerRow * bricksPerRow;
        int words = (schema.Offset(Grid, "m_slicesInternal") - schema.Offset(Grid, "m_detailGridWalkable")) / sizeof(ulong);
        int levels = words / wordsPerLayer * BrickEdge;
        int margin = (levels - (int)(cellHeight / VoxelSize)) / 2; // levels duplicated from the cells below/above

        var walkable = new HashSet<(int, int)>();
        foreach (var cell in blob.Array(gridArray, schema.Size(Grid)))
        {
            var (ix, iy, _) = Conventions.DecodeCellId(blob.U64(cell + schema.Offset(Grid, "m_cellId")));
            int grid = cell + schema.Offset(Grid, "m_detailGridWalkable");
            for (int w = 0; w < words; w++)
            {
                ulong word = blob.U64(grid + w * sizeof(ulong));
                if (word == 0) continue;
                int layer = w / wordsPerLayer, brick = w % wordsPerLayer;
                int bx = brick % bricksPerRow, by = brick / bricksPerRow;
                for (int n = 0; n < BrickEdge * BrickEdge; n++)
                {
                    uint nibble = (uint)(word >> (n * BrickEdge)) & 0xF;
                    if (nibble == 0 || !HasCoreLevel(nibble, layer * BrickEdge, margin, levels - margin)) continue;
                    walkable.Add((ix * columns + bx * BrickEdge + n % ColumnsPerNibbleRow,
                                  iy * columns + by * BrickEdge + n / ColumnsPerNibbleRow));
                }
            }
        }
        return walkable;
    }

    /// <summary>Top-down map: row r = X (north to south), column c = Y (west to east); '#' walkable.</summary>
    public static WalkableMap Map(HashSet<(int X, int Y)> columns, Box extent)
    {
        int step = (int)(MapResolution / VoxelSize);
        int x0 = (int)MathF.Floor(extent.Min[0] / MapResolution), y0 = (int)MathF.Floor(extent.Min[1] / MapResolution);
        int rows = (int)MathF.Ceiling(extent.Max[0] / MapResolution) - x0;
        int cols = (int)MathF.Ceiling(extent.Max[1] / MapResolution) - y0;
        var lines = new string[rows];
        for (int r = 0; r < rows; r++)
        {
            var chars = new char[cols];
            for (int c = 0; c < cols; c++)
                chars[c] = AnyWalkable(columns, (x0 + r) * step, (y0 + c) * step, step) ? '#' : '.';
            lines[r] = new string(chars);
        }
        return new WalkableMap(MapResolution, [x0 * MapResolution, y0 * MapResolution], lines);
    }

    private static bool HasCoreLevel(uint nibble, int firstLevel, int minLevel, int maxLevel)
    {
        for (int b = 0; b < BrickEdge; b++)
            if ((nibble >> b & 1) != 0 && firstLevel + b >= minLevel && firstLevel + b < maxLevel) return true;
        return false;
    }

    private static bool AnyWalkable(HashSet<(int X, int Y)> columns, int vx, int vy, int step)
    {
        for (int dx = 0; dx < step; dx++)
            for (int dy = 0; dy < step; dy++)
                if (columns.Contains((vx + dx, vy + dy))) return true;
        return false;
    }
}
