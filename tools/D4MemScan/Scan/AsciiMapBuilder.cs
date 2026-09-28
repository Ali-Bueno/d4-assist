using D4Memory.Scan;
using D4Data.Format;

namespace D4MemScan.Scan;

/// <summary>
/// Renders a scan's tiles as a top-down ASCII grid: rows = X increasing = south, columns = Y increasing =
/// east, one cell per ExitConsistencyChecker.GridUnit world units. Each cell shows the exit-letter label of
/// the tile covering it (a tile larger than one cell fills every cell of its footprint with the same label).
/// </summary>
public static class AsciiMapBuilder
{
    private const string EmptyCell = ".";
    private const string ColumnSeparator = " ";

    public static List<string> Build(IReadOnlyList<CSceneHit> tiles, IReadOnlyDictionary<int, CandidateScene> candidates)
    {
        var cellLabels = new Dictionary<(long X, long Y), string>();
        foreach (var tile in tiles)
            foreach (var cell in Footprint(tile, candidates))
                cellLabels[cell] = ExitLabel(tile.ExitMask);

        if (cellLabels.Count == 0) return [];

        long minX = cellLabels.Keys.Min(c => c.X), maxX = cellLabels.Keys.Max(c => c.X);
        long minY = cellLabels.Keys.Min(c => c.Y), maxY = cellLabels.Keys.Max(c => c.Y);

        var rows = new List<string>();
        for (long x = minX; x <= maxX; x++)
        {
            var cells = new List<string>();
            for (long y = minY; y <= maxY; y++)
                cells.Add(cellLabels.GetValueOrDefault((x, y), EmptyCell));
            rows.Add(string.Join(ColumnSeparator, cells));
        }
        return rows;
    }

    private static IEnumerable<(long X, long Y)> Footprint(CSceneHit tile, IReadOnlyDictionary<int, CandidateScene> candidates)
    {
        var size = candidates.TryGetValue(tile.Sno, out var c) ? c.TileSize : [];
        float sizeX = size.Length > 0 && size[0] > 0 ? size[0] : ExitConsistencyChecker.GridUnit;
        float sizeY = size.Length > 1 && size[1] > 0 ? size[1] : ExitConsistencyChecker.GridUnit;

        long x0 = Cell(tile.Wp.X), y0 = Cell(tile.Wp.Y);
        long cellsX = Math.Max(1, (long)MathF.Round(sizeX / ExitConsistencyChecker.GridUnit));
        long cellsY = Math.Max(1, (long)MathF.Round(sizeY / ExitConsistencyChecker.GridUnit));
        for (long dx = 0; dx < cellsX; dx++)
            for (long dy = 0; dy < cellsY; dy++)
                yield return (x0 + dx, y0 + dy);
    }

    private static long Cell(float coordinate) => (long)MathF.Round(coordinate / ExitConsistencyChecker.GridUnit);

    private static string ExitLabel(int exitMask)
    {
        var exits = Conventions.Exits(exitMask);
        return exits.Length == 0 ? "-" : string.Concat(exits);
    }
}
