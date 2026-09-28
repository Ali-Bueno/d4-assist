using System.Numerics;
using D4Data.Format;

namespace D4MemScan.Scan;

/// <summary>A placed tile with the offline footprint needed to detect whether it spans one grid cell or several.</summary>
public sealed record TileFootprint(int Sno, int ExitMask, Vector3 Wp, float SizeX, float SizeY);

public sealed record ExitConsistencyResult(int ConsistentPairs, int TotalPairs, double Percent, int SkippedMultiCellTiles);

/// <summary>
/// Self-validation for a scan: lays single-grid-cell tiles on a grid and checks that every pair of
/// grid-adjacent tiles has matching exit bits facing each other (a real DRLG corridor connects both sides;
/// a mismatch means either tile's placement or exit mask was misread, so this is a correctness signal on the
/// scan itself). Axes per research/dungeon-data.md §5: X = south, Y = east. Tiles larger than one grid cell
/// (e.g. 64x64) cannot be placed on a single-cell grid this way and are reported as skipped rather than guessed.
/// </summary>
public static class ExitConsistencyChecker
{
    /// <summary>Confirmed DRLG grid granularity (the tile-hash formula's unit; research/client-memory.md §1).</summary>
    public const float GridUnit = 32f;

    /// <summary>Slack, in world units, for treating a tile's footprint as exactly one grid cell.</summary>
    private const float SizeToleranceUnits = 0.5f;

    public static ExitConsistencyResult CheckAdjacency(IReadOnlyList<TileFootprint> tiles)
    {
        var singleCell = tiles.Where(IsSingleCell).ToList();
        int skipped = tiles.Count - singleCell.Count;

        var byCell = new Dictionary<(long X, long Y), TileFootprint>();
        foreach (var t in singleCell) byCell[(Cell(t.Wp.X), Cell(t.Wp.Y))] = t;

        int consistent = 0, total = 0;
        foreach (var (cell, tile) in byCell)
        {
            // Only check south/east neighbours per tile: each adjacent pair is then evaluated exactly once.
            Evaluate(byCell, (cell.X + 1, cell.Y), tile, Conventions.ExitSouth, Conventions.ExitNorth, ref consistent, ref total);
            Evaluate(byCell, (cell.X, cell.Y + 1), tile, Conventions.ExitEast, Conventions.ExitWest, ref consistent, ref total);
        }
        return new ExitConsistencyResult(consistent, total, ConsistencyPercent(consistent, total), skipped);
    }

    public static double ConsistencyPercent(int consistentPairs, int totalPairs) =>
        totalPairs == 0 ? 0 : 100.0 * consistentPairs / totalPairs;

    private static bool IsSingleCell(TileFootprint t) => IsOneCell(t.SizeX) && IsOneCell(t.SizeY);

    private static bool IsOneCell(float size) => size <= 0 || MathF.Abs(size - GridUnit) <= SizeToleranceUnits;

    private static void Evaluate(
        Dictionary<(long X, long Y), TileFootprint> byCell, (long X, long Y) cellB,
        TileFootprint a, int exitTowardB, int exitTowardA, ref int consistent, ref int total)
    {
        if (!byCell.TryGetValue(cellB, out var b)) return;
        total++;
        // Neighbours agree when the shared border is open on both sides or closed on both sides.
        if (((a.ExitMask & exitTowardB) != 0) == ((b.ExitMask & exitTowardA) != 0)) consistent++;
    }

    private static long Cell(float coordinate) => (long)MathF.Round(coordinate / GridUnit);
}
