using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

/// <summary>
/// Covers the second half of the room-announcement fix: the spoken direction follows a BFS path's
/// farthest visible point, not a straight line that can cut through walls on an L-shaped room, and
/// not a fixed short look-ahead that reads as whichever grid axis the path happens to start along.
/// </summary>
public class RoomRouteCacheTests
{
    // An L: the south row and the east column of a 32x32 grid, meeting at the bottom-right corner.
    // A straight line from the north-east corner to the south-west corner crosses the empty middle.
    private static WalkGrid LShapedGrid()
    {
        var cells = new bool[32, 32];
        for (var c = 0; c < 32; c++) cells[31, c] = true;
        for (var r = 0; r < 32; r++) cells[r, 31] = true;
        return new WalkGrid(1f, 0f, 0f, 32, 32, cells);
    }

    private static Room LShapedRoom(RoomOpening opening) =>
        new(new RoomKey(0, 0, true), [new MapTile(new TileInput(1, "l", 0, 0, 32, 32, ExitMaskBits.West, 1, 1))],
            [opening], LShapedGrid());

    [Fact]
    public void L_shaped_room_direction_follows_the_paths_first_leg_not_the_straight_line()
    {
        var opening = new RoomOpening(ExitSide.West, (31.5f, 0.5f), Unexplored: false);
        var room = LShapedRoom(opening);
        var player = new PlayerInput(0.5f, 31.5f, 0);

        var route = Assert.Single(new RoomRouteCache().Resolve(room, player));

        Assert.False(route.UsedPathFallback);
        Assert.True(route.PathLength > 0);
        Assert.Equal(StickDirection.DownLeft, route.Direction); // first leg: straight south (engine +X)

        // The bug this guards against: a straight line to the raw opening point crosses the empty
        // middle of the L and reads as a different, wrong direction.
        var straightLine = ScreenDirections.Bin(opening.Position.X - player.X, opening.Position.Y - player.Y);
        Assert.NotEqual(straightLine, route.Direction);
    }

    [Fact]
    public void Routes_are_cached_until_the_player_moves_past_the_threshold()
    {
        var opening = new RoomOpening(ExitSide.West, (31.5f, 0.5f), Unexplored: false);
        var room = LShapedRoom(opening);
        var cache = new RoomRouteCache();

        var first = cache.Resolve(room, new PlayerInput(0.5f, 31.5f, 0));
        var stillCached = cache.Resolve(room, new PlayerInput(0.5f, 31.5f + RoomRouteCache.RecomputeThresholdUnits / 2, 0));
        Assert.Same(first, stillCached);

        var recomputed = cache.Resolve(room, new PlayerInput(0.5f, 31.5f + RoomRouteCache.RecomputeThresholdUnits * 2, 0));
        Assert.NotSame(first, recomputed);
    }

    [Fact]
    public void Open_room_exit_offsets_bin_to_the_true_screen_cardinal_not_a_short_lookaheads_diagonal()
    {
        // A fully open room: the old fixed 3-unit look-ahead would have read the path's first
        // (grid-axis-aligned) step, not the true bearing to the exit.
        var grid = new WalkGrid(1f, 0f, 0f, 21, 21, FullyWalkable(21, 21));
        var openingRight = new RoomOpening(ExitSide.North, (0f, 20f), Unexplored: false); // offset (-10,+10)
        var openingDown = new RoomOpening(ExitSide.South, (20f, 20f), Unexplored: false); // offset (+10,+10)
        var room = new Room(new RoomKey(0, 0, true),
            [new MapTile(new TileInput(1, "open", 0, 0, 21, 21, ExitMaskBits.North | ExitMaskBits.South, 1, 1))],
            [openingRight, openingDown], grid);
        var player = new PlayerInput(10f, 10f, 0);

        var routes = new RoomRouteCache().Resolve(room, player);

        Assert.Equal(StickDirection.Right, routes.Single(r => r.Opening == openingRight).Direction);
        Assert.Equal(StickDirection.Down, routes.Single(r => r.Opening == openingDown).Direction);
    }

    [Fact]
    public void A_farthest_point_too_close_to_the_player_falls_back_to_the_openings_position()
    {
        var grid = new WalkGrid(1f, 0f, 0f, 5, 5, FullyWalkable(5, 5));
        var opening = new RoomOpening(ExitSide.North, (1f, 2.5f), Unexplored: false);
        var room = new Room(new RoomKey(0, 0, true),
            [new MapTile(new TileInput(1, "tiny", 0, 0, 5, 5, ExitMaskBits.North, 1, 1))], [opening], grid);
        var player = new PlayerInput(2.5f, 2.5f, 0); // sits exactly on the opening's own inward target cell

        var route = Assert.Single(new RoomRouteCache().Resolve(room, player));

        Assert.False(route.UsedPathFallback); // BFS did find a (trivial, zero-length) path
        var expected = ScreenDirections.Bin(opening.Position.X - player.X, opening.Position.Y - player.Y);
        Assert.Equal(expected, route.Direction);
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
