using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

/// <summary>Test double for ITileWalkability, plus a helper to build a WalkGrid from ASCII rows
/// (row = X north-to-south, column = Y west-to-east, '#' walkable) — the same shape D4Data's
/// WalkableMap uses (research/dungeon-data.md §4.1, D4Data.Model.WalkableMap).</summary>
internal sealed class FakeTileWalkability : ITileWalkability
{
    private readonly Dictionary<int, WalkGrid> _grids = new();

    public FakeTileWalkability Add(int sceneSno, WalkGrid grid)
    {
        _grids[sceneSno] = grid;
        return this;
    }

    /// <summary>How many times a grid was asked for: one room build asks once per member tile.</summary>
    public int Lookups { get; private set; }

    public bool TryGet(int sceneSno, out WalkGrid grid)
    {
        Lookups++;
        return _grids.TryGetValue(sceneSno, out grid!);
    }

    public static WalkGrid GridFromRows(string[] rows, float resolution = 1f, float originX = 0f, float originY = 0f)
    {
        var cells = new bool[rows.Length, rows[0].Length];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                cells[r, c] = rows[r][c] == '#';
        return new WalkGrid(resolution, originX, originY, rows.Length, rows[0].Length, cells);
    }
}
