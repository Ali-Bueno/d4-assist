using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class ExplorationGuideTests
{
    private const int E = ExitMaskBits.East, W = ExitMaskBits.West;
    private const float Half = GridMath.CellSize / 2f;

    // A 32x32 tile at grid cell (cx, cy); X = cx*32 (south), Y = cy*32 (east). Each tile is its own room unless a node is given.
    private static TileInput Tile(int cx, int cy, int mask, uint node = 0, int sno = 1) =>
        new(sno, "t", cx * 32, cy * 32, 32, 32, mask, node == 0 ? (uint)(1000 + cx * 100 + cy) : node, 0);

    private static PlayerInput Centre(int cx, int cy) => new(cx * 32 + Half, cy * 32 + Half, 0);

    private static DungeonMap Map(params TileInput[] tiles)
    {
        var map = new DungeonMap();
        map.AddOrUpdate(tiles);
        return map;
    }

    private static void Visit(DungeonMap map, params (int Cx, int Cy)[] cells)
    {
        foreach (var (cx, cy) in cells) map.MarkVisited(cx * 32 + Half, cy * 32 + Half);
    }

    // A corridor of visited tiles along +Y, open at both ends: frontiers at Y=0 (west) and Y=160 (east).
    private static (DungeonMap Map, ExplorationGuide Guide) Corridor()
    {
        var tiles = Enumerable.Range(0, 5).Select(cy => Tile(0, cy, W | E)).ToArray();
        var map = Map(tiles);
        Visit(map, Enumerable.Range(0, 5).Select(cy => (0, cy)).ToArray());
        return (map, new ExplorationGuide(map));
    }

    [Fact]
    public void Leads_to_a_frontier_when_an_exit_side_was_never_loaded()
    {
        var map = Map(Tile(0, 0, E));
        Visit(map, (0, 0));

        var step = new ExplorationGuide(map).Step(Centre(0, 0));

        Assert.Equal(GuideStatus.Guiding, step.Status);
        Assert.Equal(GuideTargetKind.Frontier, step.Target!.Kind);
        Assert.Equal((Half, 32f), step.Waypoint);
        Assert.Equal(Half, step.RemainingLength, 3);
    }

    [Fact]
    public void Links_touching_tiles_of_one_room_even_without_exit_bits_between_them()
    {
        const uint room = 7; // a preset room whose live masks omit the internal side
        var map = Map(Tile(0, 0, 0, room), Tile(0, 1, 0, room));
        Visit(map, (0, 0));

        var step = new ExplorationGuide(map).Step(Centre(0, 0));

        Assert.Equal(GuideStatus.Guiding, step.Status);
        Assert.Equal("tile:0,1", step.Target!.Id);
    }

    [Fact]
    public void Counts_an_arrival_when_the_player_steps_into_the_target_tile()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W | E));
        Visit(map, (0, 0));
        var guide = new ExplorationGuide(map);
        Assert.Equal(0, guide.Step(Centre(0, 0)).Arrivals);

        Visit(map, (0, 1));

        Assert.Equal(1, guide.Step(Centre(0, 1)).Arrivals);
    }

    [Fact]
    public void A_frontier_whose_far_side_loads_is_not_an_arrival()
    {
        var map = Map(Tile(0, 0, E));
        Visit(map, (0, 0));
        var guide = new ExplorationGuide(map);
        Assert.Equal(GuideTargetKind.Frontier, guide.Step(Centre(0, 0)).Target!.Kind);

        map.AddOrUpdate([Tile(0, 1, W | E)]);
        var step = guide.Step(Centre(0, 0));

        Assert.Equal("tile:0,1", step.Target!.Id);
        Assert.Equal(0, step.Arrivals);
    }

    [Fact]
    public void Prefers_a_loaded_unvisited_tile_over_a_farther_frontier()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W | E));
        Visit(map, (0, 0));

        var step = new ExplorationGuide(map).Step(Centre(0, 0));

        Assert.Equal(GuideTargetKind.UnvisitedTile, step.Target!.Kind);
        Assert.Equal("tile:0,1", step.Target.Id);
    }

    [Fact]
    public void Picks_the_frontier_with_the_shorter_route()
    {
        var (_, guide) = Corridor();

        var step = guide.Step(new PlayerInput(Half, 4 * 32 + 20, 0)); // 12 from the east end, 148 from the west end

        Assert.Equal(GuideTargetKind.Frontier, step.Target!.Kind);
        Assert.Equal(5 * 32f, step.Target.Position.Y);
    }

    [Fact]
    public void Leaves_a_dead_end_through_its_only_opening_towards_the_unvisited_tile()
    {
        // Pocket (0,0) -> corridor (0,1) -> unvisited (0,2): the waypoint is the pocket's exit, not the far tile.
        var map = Map(Tile(0, 0, E), Tile(0, 1, W | E), Tile(0, 2, W));
        Visit(map, (0, 0), (0, 1));

        var step = new ExplorationGuide(map).Step(Centre(0, 0));

        Assert.Equal("tile:0,2", step.Target!.Id);
        Assert.Equal((Half, 32f), step.Waypoint);
        Assert.Equal(Half + 32f, step.RemainingLength, 3);
    }

    [Fact]
    public void Target_inside_the_current_room_is_the_next_waypoint()
    {
        var map = Map(Tile(0, 0, E, node: 7), Tile(0, 1, W, node: 7));
        Visit(map, (0, 0));

        var step = new ExplorationGuide(map).Step(Centre(0, 0));

        Assert.Equal("tile:0,1", step.Target!.Id);
        Assert.Equal((Half, 32f), step.Waypoint);
    }

    [Fact]
    public void Hysteresis_keeps_a_near_equal_target_and_switches_only_past_the_margin()
    {
        var (_, guide) = Corridor();
        var middle = new PlayerInput(Half, 2 * 32 + Half, 0); // equidistant from both ends
        var first = guide.Step(middle).Target!;
        int towardOther = first.Position.Y > middle.Y ? -1 : 1;

        var slightlyCloserToOther = guide.Step(middle with { Y = middle.Y + towardOther * 3 });
        Assert.Equal(first.Id, slightlyCloserToOther.Target!.Id);

        var pastTheMargin = guide.Step(middle with { Y = middle.Y + towardOther * (ExplorationGuide.SwitchMarginUnits + 3) });
        Assert.NotEqual(first.Id, pastTheMargin.Target!.Id);
    }

    [Fact]
    public void Everything_visited_and_closed_is_all_explored()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W));
        Visit(map, (0, 0), (0, 1));

        Assert.Equal(GuideStatus.AllExplored, new ExplorationGuide(map).Step(Centre(0, 1)).Status);
    }

    [Fact]
    public void Destinations_wait_until_nothing_is_left_to_explore()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W));
        Visit(map, (0, 0));
        GuideDestination[] portal = [new("portal", (Half, Half), 0)];

        Assert.Equal(GuideTargetKind.UnvisitedTile, new ExplorationGuide(map).Step(Centre(0, 0), destinations: portal).Target!.Kind);

        Visit(map, (0, 1));
        var step = new ExplorationGuide(map).Step(Centre(0, 1), destinations: portal);
        Assert.Equal(GuideStatus.Guiding, step.Status);
        Assert.Equal("portal", step.Target!.Id);
    }

    [Fact]
    public void The_lowest_reachable_tier_wins_and_reaching_it_never_falls_back_a_tier()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W | E), Tile(0, 2, W));
        Visit(map, (0, 0), (0, 1), (0, 2));
        var guide = new ExplorationGuide(map);
        var back = new GuideDestination("back", (Half, Half), 1);
        var on = new GuideDestination("on", (Half, 2 * 32 + Half), 0);
        var elsewhere = new GuideDestination("cut-off", (9 * 32 + Half, Half), 0); // no loaded tile there

        Assert.Equal("on", guide.Step(Centre(0, 1), destinations: [back, on, elsewhere]).Target!.Id);

        var atThePortal = guide.Step(Centre(0, 2), destinations: [back, on, elsewhere]);
        Assert.Equal(GuideStatus.AllExplored, atThePortal.Status);
        Assert.Equal(1, atThePortal.Arrivals);
    }

    [Fact]
    public void A_used_portal_leads_when_it_is_the_only_one()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W));
        Visit(map, (0, 0), (0, 1));

        var step = new ExplorationGuide(map).Step(Centre(0, 1), destinations: [new("back", (Half, Half), 1)]);

        Assert.Equal("back", step.Target!.Id);
        Assert.Equal((Half, 32f), step.Waypoint); // through the doorway into the portal's tile
    }

    [Fact]
    public void An_exit_whose_far_side_never_loads_stops_being_a_target_once_reached()
    {
        var map = Map(Tile(0, 0, E));
        Visit(map, (0, 0));
        var guide = new ExplorationGuide(map);
        Assert.Equal(GuideStatus.Guiding, guide.Step(Centre(0, 0)).Status);

        var atTheExit = guide.Step(new PlayerInput(Half, 32 - ExplorationGuide.FrontierArrivalUnits / 2, 0));

        Assert.Equal(GuideStatus.AllExplored, atTheExit.Status);
    }

    [Fact]
    public void Tiles_not_connected_through_exits_are_not_reachable_targets()
    {
        var map = Map(Tile(0, 0, 0), Tile(5, 5, W));
        Visit(map, (0, 0));

        Assert.Equal(GuideStatus.AllExplored, new ExplorationGuide(map).Step(Centre(0, 0)).Status);
    }

    [Fact]
    public void Off_the_known_grid_is_unknown_not_explored()
    {
        var map = Map(Tile(0, 0, E));

        Assert.Equal(GuideStatus.Unknown, new ExplorationGuide(map).Step(new PlayerInput(500, 500, 0)).Status);
    }

    [Fact]
    public void Waypoint_follows_the_walk_grid_path_not_the_straight_line()
    {
        // L-shaped room, two cells thick: down the east side, then west along the south. The West exit is at (31, 0).
        var cells = new bool[32, 32];
        for (var c = 0; c < 32; c++) cells[30, c] = cells[31, c] = true;
        for (var r = 0; r < 32; r++) cells[r, 30] = cells[r, 31] = true;
        var walkability = new FakeTileWalkability().Add(1, new WalkGrid(1f, 0f, 0f, 32, 32, cells));
        var map = Map(Tile(0, 0, W));
        Visit(map, (0, 0));
        var player = new PlayerInput(0.5f, 31.5f, 0);

        var step = new ExplorationGuide(map, walkability).Step(player);

        Assert.Equal(GuideStatus.Guiding, step.Status);
        Assert.True(step.Waypoint.Y > 25f, $"waypoint {step.Waypoint} cuts through the wall");
        Assert.True(step.RemainingLength > 55f, $"route {step.RemainingLength} shorter than the walkable path");
    }

    [Fact]
    public void Reset_forgets_dead_exits_and_the_current_target()
    {
        var map = Map(Tile(0, 0, E));
        Visit(map, (0, 0));
        var guide = new ExplorationGuide(map);
        guide.Step(new PlayerInput(Half, 31, 0)); // spends the exit

        guide.Reset();

        Assert.Equal(GuideStatus.Guiding, guide.Step(Centre(0, 0)).Status);
    }
}
