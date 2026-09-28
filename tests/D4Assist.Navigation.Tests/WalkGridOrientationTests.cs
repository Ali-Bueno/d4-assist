using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

/// <summary>
/// Pins WalkGrid's row/column convention against real offline data: TT_Step_City_to_Protodun_Jail_NSEW_01
/// (SNO 715062, 64x64, exit=15 — the exact tile from the 2026-09-28 live bug report), dumped via
/// D4DungeonDump --grid against the installed game (offline CASC read). Its N/S/E/W walkable-run ranges
/// on the map's boundary rows/columns, distilled here, confirm row = X (north to south, row 0 = the
/// -X/North edge) and column = Y (west to east, column 0 = the -Y/West edge) — matching dungeon-data.md
/// §4.1 and D4Data.Model.WalkableMap's own doc comment.
/// </summary>
public class WalkGridOrientationTests
{
    private static WalkGrid JailTileGrid()
    {
        var cells = new bool[64, 64];
        foreach (var c in Enumerable.Range(10, 13)) cells[0, c] = true;   // N edge (row 0): Y 10..22
        foreach (var c in Enumerable.Range(40, 11)) cells[63, c] = true;  // S edge (row 63): Y 40..50
        foreach (var r in Enumerable.Range(9, 15)) cells[r, 0] = true;    // W edge (col 0): X 9..23
        cells[4, 0] = true;                                              // W edge noise: isolated 1-cell run
        foreach (var r in Enumerable.Range(39, 16)) cells[r, 63] = true;  // E edge (col 63): X 39..54
        return new WalkGrid(1f, 0f, 0f, 64, 64, cells);
    }

    [Fact]
    public void North_edge_run_sits_on_row_zero_within_its_own_Y_range()
    {
        var grid = JailTileGrid();
        Assert.True(grid.IsWalkable(0, 10));
        Assert.True(grid.IsWalkable(0, 22));
        Assert.False(grid.IsWalkable(0, 9));
        Assert.False(grid.IsWalkable(0, 23));
        Assert.False(grid.IsWalkable(1, 16)); // one row south of the edge: not part of the boundary run
    }

    [Fact]
    public void South_edge_run_sits_on_the_last_row_in_its_own_Y_range()
    {
        var grid = JailTileGrid();
        Assert.True(grid.IsWalkable(63, 40));
        Assert.True(grid.IsWalkable(63, 50));
        Assert.False(grid.IsWalkable(63, 39));
        Assert.False(grid.IsWalkable(63, 51));
    }

    [Fact]
    public void West_and_east_runs_sit_on_column_zero_and_the_last_column()
    {
        var grid = JailTileGrid();
        Assert.True(grid.IsWalkable(9, 0));
        Assert.True(grid.IsWalkable(23, 0));
        Assert.True(grid.IsWalkable(39, 63));
        Assert.True(grid.IsWalkable(54, 63));
        Assert.False(grid.IsWalkable(30, 0));
        Assert.False(grid.IsWalkable(30, 63));
    }

    [Fact]
    public void Cell_center_matches_the_row_is_X_column_is_Y_convention()
    {
        var grid = new WalkGrid(1f, 0f, 0f, 64, 64, new bool[64, 64]);
        Assert.Equal((0.5f, 16.5f), grid.CellCenter(0, 16));
        Assert.Equal((1, 16), grid.CellAt(1.5f, 16.5f));
    }
}
