using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class BarrierGuideTests
{
    private const int E = ExitMaskBits.East, W = ExitMaskBits.West;
    private const float Half = GridMath.CellSize / 2f;
    private const int FloorFrom = 13, FloorTo = 18; // corridor floor: local rows (X) 13-18, along the whole tile
    private const int BarrierTile = 5;

    private static TileInput Tile(int cy, int mask) => new(1, "t", 0, cy * 32, 32, 32, mask, (uint)(1000 + cy), 0);

    private static PlayerInput Centre(int cy) => new(Half, cy * 32 + Half, 0);

    private static FakeTileWalkability CorridorFloor()
    {
        var cells = new bool[32, 32];
        for (var r = FloorFrom; r <= FloorTo; r++)
            for (var c = 0; c < 32; c++) cells[r, c] = true;
        return new FakeTileWalkability().Add(1, new WalkGrid(1f, 0f, 0f, 32, 32, cells));
    }

    // Seven corridor tiles along +Y: 0 unexplored at the west end (112 away), 1-5 explored with the player in 4, 6 unexplored at the east end (48 away).
    private static (DungeonMap Map, ExplorationGuide Guide) Corridor(ITileWalkability? walkability = null, params int[] alsoVisited)
    {
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, E), .. Enumerable.Range(1, 5).Select(cy => Tile(cy, W | E)), Tile(6, W)]);
        foreach (var cy in Enumerable.Range(1, 5).Concat(alsoVisited)) map.MarkVisited(Half, cy * 32 + Half);
        return (map, new ExplorationGuide(map, walkability ?? CorridorFloor()));
    }

    // A wall straight across the corridor floor at world Y = y.
    private static HashSet<(int X, int Y)> WallAcross(int y) => [.. Enumerable.Range(FloorFrom, FloorTo - FloorFrom + 1).Select(x => (x, y))];

    private static (int X, int Y) CellOf(float x, float y) => ((int)MathF.Floor(x), (int)MathF.Floor(y));

    [Fact]
    public void Touching_tiles_of_one_room_with_no_floor_at_their_shared_edge_are_not_joined()
    {
        // The Skovos docks (live 2026-10-17): one room, water between two of its tiles; the guide led to the far one across it.
        var shore = new bool[32, 32];
        for (var r = 0; r < 32; r++)
            for (var c = 8; c < 32; c++) shore[r, c] = true; // eight units of water along the west edge
        var full = new bool[32, 32];
        for (var r = 0; r < 32; r++)
            for (var c = 0; c < 32; c++) full[r, c] = true;
        var walkability = new FakeTileWalkability().Add(1, new WalkGrid(1f, 0f, 0f, 32, 32, shore)).Add(2, new WalkGrid(1f, 0f, 0f, 32, 32, full));
        TileInput RoomTile(int sceneSno, int cy) => new(sceneSno, "t", 0, cy * 32, 32, 32, 0, 7, 0);
        var map = new DungeonMap();
        map.AddOrUpdate([RoomTile(2, 0), RoomTile(1, 1), RoomTile(2, 2)]);
        map.MarkVisited(Half, 32 + Half);
        var guide = new ExplorationGuide(map, walkability);

        Assert.Equal("tile:0,2", guide.Step(Centre(1)).Target!.Id);

        map.MarkVisited(Half, 64 + Half);
        Assert.Equal(GuideStatus.AllExplored, guide.Step(Centre(1)).Status);
    }

    [Fact]
    public void An_opening_with_only_stray_floor_cells_beyond_it_is_no_way_through()
    {
        // The Labyrinthhain's entrance (live 2026-10-21): floor along the tile's edge at two stretches, but beyond the first
        // only two single floor cells among the trees; the guide sent the player into them instead of the stretch further on.
        var near = new bool[32, 32];
        for (var r = 0; r < 32; r++)
            for (var c = 0; c < 31; c++) near[r, c] = true;
        for (var r = 4; r <= 12; r++) near[r, 31] = true;
        for (var r = 20; r <= 25; r++) near[r, 31] = true;
        var far = new bool[32, 32];
        for (var r = 20; r < 32; r++)
            for (var c = 0; c < 32; c++) far[r, c] = true;
        far[6, 1] = far[10, 2] = true; // stray cells, out of reach of each other
        var walkability = new FakeTileWalkability().Add(1, new WalkGrid(1f, 0f, 0f, 32, 32, near)).Add(2, new WalkGrid(1f, 0f, 0f, 32, 32, far));
        var map = new DungeonMap();
        map.AddOrUpdate([new TileInput(1, "t", 0, 0, 32, 32, E, 1000, 0), new TileInput(2, "t", 0, 32, 32, 32, W, 1001, 0)]);
        map.MarkVisited(Half, Half);

        var step = new ExplorationGuide(map, walkability).Step(new PlayerInput(8f, 28f, 0));

        Assert.Equal("tile:0,1", step.Target!.Id);
        Assert.True(step.Waypoint.X > 16f, $"waypoint {step.Waypoint} heads for the stray cells"); // the stretch at rows 20-25
    }

    [Fact]
    public void A_barrier_splitting_a_corridor_tile_cuts_off_the_tiles_beyond_until_it_opens()
    {
        var (_, guide) = Corridor();
        Assert.Equal("tile:0,6", guide.Step(Centre(4)).Target!.Id);

        guide.SetBlockedCells(WallAcross(BarrierTile * 32 + 5)); // five units into the tile, as live
        var cutOff = guide.Step(Centre(4));
        Assert.Equal(GuideStatus.Guiding, cutOff.Status);
        Assert.Equal("tile:0,0", cutOff.Target!.Id);

        guide.SetBlockedCells(new HashSet<(int X, int Y)>());
        Assert.Equal("tile:0,6", guide.Step(Centre(4)).Target!.Id);
    }

    [Fact]
    public void Once_a_barrier_falls_the_ground_past_it_leads_before_nearer_ground_left_behind()
    {
        // Tester, Derelict Lodge (2026-10-16): past the door just unlocked, the guide turned back to the first area's corners.
        var (map, guide) = Corridor();
        guide.SetBlockedCells(WallAcross(BarrierTile * 32 + 5));
        Assert.Equal("tile:0,0", guide.Step(Centre(1)).Target!.Id);

        guide.SetBlockedCells(new HashSet<(int X, int Y)>());
        Assert.Equal("tile:0,6", guide.Step(Centre(1)).Target!.Id);

        map.MarkVisited(Half, 6 * 32 + Half);
        Assert.Equal("tile:0,0", guide.Step(Centre(1)).Target!.Id);
    }

    [Fact]
    public void A_barrier_that_only_rises_keeps_the_nearest_ground_first()
    {
        var (_, guide) = Corridor();
        guide.SetBlockedCells(WallAcross(BarrierTile * 32 + 5));
        guide.SetBlockedCells(WallAcross(BarrierTile * 32 + 5).Concat(WallAcross(BarrierTile * 32 + 6)).ToHashSet());

        Assert.Equal("tile:0,0", guide.Step(Centre(1)).Target!.Id);
    }

    [Fact]
    public void Ground_left_only_behind_a_barrier_is_all_explored()
    {
        var (_, guide) = Corridor(alsoVisited: 0);
        Assert.Equal("tile:0,6", guide.Step(Centre(4)).Target!.Id);

        guide.SetBlockedCells(WallAcross(BarrierTile * 32 + 5));

        Assert.Equal(GuideStatus.AllExplored, guide.Step(Centre(4)).Status);
    }

    [Theory]
    [InlineData(6 * 32 - 1)] // the explored tile's own doorway cells
    [InlineData(6 * 32)]     // the cells just across it, in the unexplored tile
    public void A_barrier_over_a_doorway_closes_it(int y)
    {
        var (_, guide) = Corridor();

        guide.SetBlockedCells(WallAcross(y));

        Assert.Equal("tile:0,0", guide.Step(Centre(4)).Target!.Id);
    }

    [Fact]
    public void A_barrier_over_a_doorway_leaves_no_opening_and_over_part_of_one_leaves_the_free_part()
    {
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(BarrierTile, W | E)]);
        var tile = map.Tiles.Single();
        var walkability = CorridorFloor();
        int edge = (BarrierTile + 1) * 32 - 1;

        var closed = OpeningFinder.Find(map, [tile], [tile], walkability, new BlockedCells(WallAcross(edge)));
        Assert.DoesNotContain(closed, o => o.Side == ExitSide.East);
        Assert.Contains(closed, o => o.Side == ExitSide.West);

        var narrowed = OpeningFinder.Find(map, [tile], [tile], walkability, new BlockedCells([(FloorFrom, edge), (FloorFrom + 1, edge)]));
        var east = Assert.Single(narrowed, o => o.Side == ExitSide.East);
        Assert.Equal(4f, east.Width);
        Assert.Equal((17f, edge + 1f), east.Position);
        Assert.False(east.UsedFallback);
    }

    [Fact]
    public void An_objective_behind_a_barrier_waits_for_a_reachable_one()
    {
        var (_, guide) = Corridor();
        GuideDestination[] objectives = [new("behind", (Half, BarrierTile * 32 + 20), 0), new("open", (Half, Half), 0)];

        guide.SetBlockedCells(WallAcross(BarrierTile * 32 + 5)); // the "behind" point is in the same tile, past the wall
        Assert.Equal("open", guide.Step(Centre(4), objectives: objectives).Target!.Id);

        guide.SetBlockedCells(new HashSet<(int X, int Y)>());
        Assert.Equal("behind", guide.Step(Centre(4), objectives: objectives).Target!.Id);
    }

    [Fact]
    public void An_objective_cut_off_only_by_a_door_the_game_keeps_shut_leads_to_that_door()
    {
        // The Tunnels of Hefaetrust (live 2026-10-23): an invisible door shut the way to the quest's last trigger until the fight
        // before it ended; the guide explored the start's corners 400 units away instead.
        var (_, guide) = Corridor();
        GuideDestination[] objectives = [new("behind", (Half, BarrierTile * 32 + 20), 0, Located: true)];
        var door = WallAcross(BarrierTile * 32 + 5);
        guide.SetBlockedCells(door);

        Assert.Equal(GuideTargetKind.UnvisitedTile, guide.Step(Centre(4), objectives: objectives).Target!.Kind);

        guide.SetGates([new GuideGate("door", (Half, BarrierTile * 32 + 5.5f), door)]);
        var step = guide.Step(Centre(4), objectives: objectives);
        Assert.Equal(GuideTargetKind.Gate, step.Target!.Kind);
        Assert.Equal("behind", step.Toward!.Id);
        Assert.True(step.RemainingLength < 32, $"route {step.RemainingLength}");

        guide.SetBlockedCells(new HashSet<(int X, int Y)>());
        guide.SetGates([]);
        Assert.Equal("behind", guide.Step(Centre(4), objectives: objectives).Target!.Id);
    }

    [Fact]
    public void A_shut_door_the_player_stood_at_and_walked_away_from_leads_no_more_until_switched_on_again()
    {
        // A key door is shut by the game too, its key elsewhere: waiting at it would wait forever.
        var (_, guide) = Corridor();
        GuideDestination[] objectives = [new("behind", (Half, BarrierTile * 32 + 20), 0, Located: true)];
        var door = WallAcross(BarrierTile * 32 + 5);
        guide.SetBlockedCells(door);
        guide.SetGates([new GuideGate("door", (Half, BarrierTile * 32 + 5.5f), door)]);
        var atDoor = new PlayerInput(Half, BarrierTile * 32 + 2, 0);

        Assert.Equal(GuideTargetKind.Gate, guide.Step(atDoor, objectives: objectives).Target!.Kind);
        Assert.Equal(GuideTargetKind.Gate, guide.Step(Centre(4), objectives: objectives).Target!.Kind); // a step back, still near
        Assert.Equal(GuideTargetKind.UnvisitedTile, guide.Step(Centre(3), objectives: objectives).Target!.Kind);

        guide.RearmGates();
        Assert.Equal(GuideTargetKind.Gate, guide.Step(Centre(3), objectives: objectives).Target!.Kind);
    }

    [Fact]
    public void A_shut_door_with_no_objective_past_it_is_no_target()
    {
        var (_, guide) = Corridor();
        GuideDestination[] objectives = [new("elsewhere", (Half, -40), 0, Located: true)];
        var door = WallAcross(BarrierTile * 32 + 5);
        guide.SetBlockedCells(door);
        guide.SetGates([new GuideGate("door", (Half, BarrierTile * 32 + 5.5f), door)]);

        Assert.NotEqual(GuideTargetKind.Gate, guide.Step(Centre(4), objectives: objectives).Target!.Kind);
    }

    // One open 32x32 room with its exit east (Y = 32); a wall across it at Y = 16 leaves a gap at X 26-31.
    private static readonly HashSet<(int X, int Y)> RoomWall = [.. Enumerable.Range(0, 26).Select(x => (x, 16))];

    private static (DungeonMap Map, FakeTileWalkability Walkability) OpenRoom()
    {
        var cells = new bool[32, 32];
        for (var r = 0; r < 32; r++)
            for (var c = 0; c < 32; c++) cells[r, c] = true;
        var map = new DungeonMap();
        map.AddOrUpdate([new TileInput(1, "room", 0, 0, 32, 32, E, 7, 0)]);
        map.MarkVisited(Half, Half);
        return (map, new FakeTileWalkability().Add(1, new WalkGrid(1f, 0f, 0f, 32, 32, cells)));
    }

    [Fact]
    public void The_room_path_goes_around_a_barrier_never_through_it()
    {
        var (map, walkability) = OpenRoom();

        var grid = RoomWalkGrid.Build([.. map.Tiles], walkability, new BlockedCells(RoomWall))!;

        Assert.True(RoomPathfinder.TryFindPath(grid, (16.5f, 4.5f), [(16, 29)], out var path, out _));
        Assert.All(path, p => Assert.DoesNotContain(CellOf(p.X, p.Y), RoomWall));
        Assert.Contains(path, p => p.X > 26f);
    }

    [Fact]
    public void The_look_ahead_heads_for_the_gap_and_never_crosses_the_barrier()
    {
        var (map, walkability) = OpenRoom();
        var guide = new ExplorationGuide(map, walkability);
        var player = new PlayerInput(16.5f, 4.5f, 0);
        var straight = guide.Step(player);

        guide.SetBlockedCells(RoomWall);
        var step = guide.Step(player);

        Assert.Equal(GuideTargetKind.Frontier, step.Target!.Kind);
        Assert.True(step.Waypoint.X > 25f, $"waypoint {step.Waypoint} is not toward the gap");
        Assert.True(step.RemainingLength > straight.RemainingLength, $"route {step.RemainingLength} not longer than the straight {straight.RemainingLength}");
        for (var t = 0f; t <= 1f; t += 1f / 512)
        {
            var (x, y) = (player.X + (step.Waypoint.X - player.X) * t, player.Y + (step.Waypoint.Y - player.Y) * t);
            Assert.DoesNotContain(CellOf(x, y), RoomWall);
        }
    }

    [Fact]
    public void The_same_barriers_again_reuse_the_routes_and_new_ones_rebuild_them()
    {
        var walkability = new CountingWalkability(CorridorFloor());
        var (_, guide) = Corridor(walkability);
        guide.SetBlockedCells(WallAcross(BarrierTile * 32 + 5));
        var first = guide.Step(Centre(4));
        var lookups = walkability.Lookups;

        guide.SetBlockedCells(WallAcross(BarrierTile * 32 + 5)); // a fresh set with the same cells
        Assert.Same(first, guide.Step(Centre(4)));
        Assert.Equal(lookups, walkability.Lookups);

        guide.SetBlockedCells(WallAcross(BarrierTile * 32 + 6));
        Assert.Equal(first.Target, guide.Step(Centre(4)).Target);
        Assert.True(walkability.Lookups > lookups, "a different set must rebuild the routes");
    }

    private sealed class CountingWalkability(ITileWalkability inner) : ITileWalkability
    {
        public int Lookups { get; private set; }

        public bool TryGet(int sceneSno, out WalkGrid grid)
        {
            Lookups++;
            return inner.TryGet(sceneSno, out grid);
        }
    }
}
