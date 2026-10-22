using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class RoomPathfinderTests
{
    [Fact]
    public void Corner_cutting_is_refused_when_both_orthogonal_neighbours_are_blocked()
    {
        var cells = new bool[2, 2];
        cells[0, 0] = true;
        cells[1, 1] = true; // diagonal target; (0,1) and (1,0) stay unwalkable
        var grid = new WalkGrid(1f, 0f, 0f, 2, 2, cells);

        var found = RoomPathfinder.TryFindPath(grid, (0.5f, 0.5f), [(1, 1)], out _, out _);

        Assert.False(found);
    }

    [Fact]
    public void A_valid_route_detours_around_a_blocked_corner_instead_of_cutting_it()
    {
        var cells = new bool[3, 3];
        cells[0, 0] = true;
        cells[0, 1] = true;
        cells[1, 1] = true;
        cells[2, 1] = true;
        cells[2, 2] = true; // target
        var grid = new WalkGrid(1f, 0f, 0f, 3, 3, cells);

        var found = RoomPathfinder.TryFindPath(grid, (0.5f, 0.5f), [(2, 2)], out var path, out var length);

        Assert.True(found);
        Assert.Equal(5, path.Count); // (0,0)-(0,1)-(1,1)-(2,1)-(2,2), no diagonal shortcut
        Assert.Equal(4f, length);
    }

    [Fact]
    public void A_start_on_a_blocked_cell_snaps_to_the_nearest_walkable_cell()
    {
        var cells = new bool[8, 8];
        cells[0, 3] = true; // 3 cells away from the (unwalkable) start; within the snap radius
        var grid = new WalkGrid(1f, 0f, 0f, 8, 8, cells);

        var found = RoomPathfinder.TryFindPath(grid, (0.5f, 0.5f), [(0, 3)], out var path, out _);

        Assert.True(found);
        Assert.Equal((0.5f, 3.5f), path[0]); // snapped start, not the blocked (0,0.5)
    }

    [Fact]
    public void A_start_beyond_the_snap_radius_stays_unreachable()
    {
        var cells = new bool[8, 8];
        cells[0, 4] = true; // 4 cells away: just beyond StartSnapRadiusCells (3)
        var grid = new WalkGrid(1f, 0f, 0f, 8, 8, cells);

        var found = RoomPathfinder.TryFindPath(grid, (0.5f, 0.5f), [(0, 4)], out _, out _);

        Assert.False(found);
    }

    [Fact]
    public void FarthestVisiblePoint_returns_the_paths_end_when_fully_open()
    {
        var grid = new WalkGrid(1f, 0f, 0f, 10, 10, FullyWalkable(10, 10));
        var found = RoomPathfinder.TryFindPath(grid, (0.5f, 0.5f), [(9, 9)], out var path, out _);
        Assert.True(found);

        var farthest = RoomPathfinder.FarthestVisiblePoint(grid, path);

        Assert.Equal(path[^1], farthest);
    }

    [Fact]
    public void FarthestVisiblePoint_stops_at_the_corner_when_the_far_leg_is_blocked()
    {
        // Same L shape as RoomRouteCacheTests.LShapedGrid: only the bottom row and right column are open.
        var cells = new bool[32, 32];
        for (var c = 0; c < 32; c++) cells[31, c] = true;
        for (var r = 0; r < 32; r++) cells[r, 31] = true;
        var grid = new WalkGrid(1f, 0f, 0f, 32, 32, cells);
        var found = RoomPathfinder.TryFindPath(grid, (0.5f, 31.5f), [(31, 1)], out var path, out _);
        Assert.True(found);

        var farthest = RoomPathfinder.FarthestVisiblePoint(grid, path);

        Assert.Equal((31.5f, 31.5f), farthest); // the corner: the far leg would require seeing through the wall
        Assert.NotEqual(path[^1], farthest);
    }

    [Fact]
    public void FarthestVisiblePoint_of_a_single_point_path_is_that_point()
    {
        var grid = new WalkGrid(1f, 0f, 0f, 4, 4, FullyWalkable(4, 4));

        var farthest = RoomPathfinder.FarthestVisiblePoint(grid, [(1.5f, 1.5f)]);

        Assert.Equal((1.5f, 1.5f), farthest);
    }

    [Fact]
    public void Clearance_counts_cells_to_the_nearest_wall_or_grid_edge()
    {
        var grid = new WalkGrid(1f, 0f, 0f, 5, 9, FullyWalkable(5, 9));

        Assert.Equal(1, grid.Clearance(0, 4));
        Assert.Equal(2, grid.Clearance(1, 4));
        Assert.Equal(3, grid.Clearance(2, 4));
        Assert.Equal(0, grid.Clearance(5, 4));
    }

    [Fact]
    public void A_route_runs_down_the_middle_of_a_walkway_not_along_its_edge()
    {
        // The Skovos docks' pier (live 2026-10-17): a walkway five cells wide, the player at its edge.
        var grid = new WalkGrid(1f, 0f, 0f, 5, 40, FullyWalkable(5, 40));

        Assert.True(RoomPathfinder.TryFindPath(grid, (4.5f, 39.5f), [(2, 0)], out var path, out _));

        Assert.All(path.Where(p => p.Y is > 5f and < 34f), p => Assert.Equal(2.5f, p.X));
    }

    [Fact]
    public void A_bearing_from_beside_a_wall_leads_away_from_it_first()
    {
        var grid = new WalkGrid(1f, 0f, 0f, 5, 40, FullyWalkable(5, 40));
        Assert.True(RoomPathfinder.TryFindPath(grid, (4.5f, 39.5f), [(2, 0)], out var path, out _));

        var aim = RoomPathfinder.FarthestVisiblePoint(grid, path);

        Assert.True(aim.Y > 30f, $"aimed at {aim}, along the edge"); // a near point toward the middle, not the far end
        Assert.True(aim.X < 4f);
    }

    private static bool[,] FullyWalkable(int rows, int cols)
    {
        var cells = new bool[rows, cols];
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                cells[r, c] = true;
        return cells;
    }
}
