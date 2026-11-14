using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class JumpRouteTests
{
    // Sightless_Skovos_SW_Traversal_01 (Philios mindscape, live 2026-10-18), placed at (64,64): its walk map is four islands,
    // joined only by three jump pairs; the west exit leaves from the left island. Row = local X, column = local Y.
    private static readonly string[] TraversalTile =
    [
        "................................",
        "................................",
        "................#.#.............",
        ".......................##.......",
        "................................",
        ".................###............",
        "..........##...........#........",
        "..........#......#####..........",
        "............###########.........",
        "...........#############........",
        "..........#############.........",
        "..........############......##..",
        ".........#############...#####..",
        "........##########.......######.",
        "........#########.........#####.",
        "#........######...........#####.",
        "##........#.#..............#####",
        "######.....................#####",
        "######.....................#####",
        "#######....................#####",
        "########...................####.",
        "###..###...............#.######.",
        "##...#.................########.",
        "##......................#######.",
        "#...#.....................###...",
        "#...............................",
        "#...................###.........",
        ".................#######........",
        "#...............#######.#.......",
        "................#######.#.......",
        "............#..########.#.......",
        "...........##.###########.......",    ];

    // The tile's jump pairs in world units (tile origin + the scene's Traversal_Gizmo_Jump markers): bottom-right, right-top, top-left.
    private static readonly JumpLink[] Jumps =
    [
        new((91.4f, 87.0f), (87.4f, 89.8f)),
        new((76.8f, 89.8f), (74.7f, 85.4f)),
        new((79.9f, 74.3f), (82.7f, 70.3f)),
    ];

    private const int TraversalSno = 1, NextSno = 2;

    private static (DungeonMap Map, ExplorationGuide Guide) Mindscape()
    {
        var full = new bool[32, 32];
        for (var r = 0; r < 32; r++)
            for (var c = 0; c < 32; c++) full[r, c] = true;
        var walkability = new FakeTileWalkability()
            .Add(TraversalSno, FakeTileWalkability.GridFromRows(TraversalTile))
            .Add(NextSno, new WalkGrid(1f, 0f, 0f, 32, 32, full));
        var map = new DungeonMap();
        map.AddOrUpdate(
        [
            new TileInput(TraversalSno, "Sightless_Skovos_SW_Traversal_01", 64, 64, 32, 32, ExitMaskBits.South | ExitMaskBits.West, 0x51, 0x59),
            new TileInput(NextSno, "Sightless_Skovos_NE_01", 64, 32, 32, 32, ExitMaskBits.North | ExitMaskBits.East, 0x5E, 0x59),
        ]);
        map.MarkVisited(92.5f, 84.5f);
        return (map, new ExplorationGuide(map, walkability));
    }

    [Fact]
    public void From_an_island_the_guide_leads_to_the_jump_point_the_route_needs_first()
    {
        var (_, guide) = Mindscape();
        guide.SetJumpLinks(Jumps);

        var step = guide.Step(new PlayerInput(92.5f, 82.5f, 0)); // the bottom island, where the player came in

        Assert.Equal("tile:2,1", step.Target!.Id);
        Assert.Equal(Jumps[0].A, step.JumpAt);
        Assert.True(Distance(step.Waypoint, Jumps[0].A) < 4f, $"waypoint {step.Waypoint}");
    }

    [Fact]
    public void Past_the_last_jump_the_guide_leads_to_the_exit()
    {
        var (_, guide) = Mindscape();
        guide.SetJumpLinks(Jumps);

        var step = guide.Step(new PlayerInput(82.5f, 68.5f, 0)); // the left island

        Assert.Null(step.JumpAt);
        Assert.True(step.Waypoint.Y < 68.5f, $"waypoint {step.Waypoint}"); // toward the west exit at Y 64
    }

    [Fact]
    public void Without_jump_links_no_jump_is_led_to()
    {
        var (_, guide) = Mindscape();

        Assert.Null(guide.Step(new PlayerInput(92.5f, 82.5f, 0)).JumpAt);
    }

    [Fact]
    public void A_path_over_a_jump_reports_where_it_jumps()
    {
        var grid = FakeTileWalkability.GridFromRows(["###....###", "###....###", "###....###"]); // two floors, a 4-cell gap
        var jump = new JumpLink((1.5f, 2.5f), (1.5f, 7.5f));

        Assert.False(RoomPathfinder.TryFindPath(grid, (1.5f, 0.5f), [(1, 9)], [], out _, out _, out _));
        Assert.True(RoomPathfinder.TryFindPath(grid, (1.5f, 0.5f), [(1, 9)], [jump], out var path, out _, out var first));

        Assert.Equal(jump.A, first!.Value.At);
        Assert.True(path[first.Value.Index].Y < 3f && path[first.Value.Index + 1].Y > 7f);
    }

    // The Necropolis's west platform (tester log 2026-11-01): a ladder joins the chamber's main floor (rows 16-31) to a platform
    // (rows 0-12, cols 0-19) whose way in is from the tile north of it; the main floor leaves east. Row = local X, column = local Y.
    private const int ChamberSno = 3, OpenSno = 4;
    private static readonly JumpLink Ladder = new((17.5f, 10.5f), (11.5f, 10.5f));
    private static readonly GuideDestination Tumor = new("tumor", (6.5f, 10.5f), Radius: 2f, Located: true);
    private static readonly PlayerInput OnMainFloor = new(24.5f, 10.5f, 0);

    private static ExplorationGuide Chamber(bool wayRound)
    {
        var cells = new bool[32, 32];
        var full = new bool[32, 32];
        for (var r = 0; r < 32; r++)
            for (var c = 0; c < 32; c++) (cells[r, c], full[r, c]) = (r >= 16 || (r <= 12 && c <= 19), true);
        var walkability = new FakeTileWalkability()
            .Add(ChamberSno, new WalkGrid(1f, 0f, 0f, 32, 32, cells))
            .Add(OpenSno, new WalkGrid(1f, 0f, 0f, 32, 32, full));
        var map = new DungeonMap();
        map.AddOrUpdate(
        [
            new TileInput(ChamberSno, "Chamber", 0, 0, 32, 32, ExitMaskBits.North | ExitMaskBits.East, 1, 1),
            new TileInput(OpenSno, "East", 0, 32, 32, 32, ExitMaskBits.West | (wayRound ? ExitMaskBits.North : 0), 2, 2),
            .. wayRound
                ? new[]
                {
                    new TileInput(OpenSno, "NorthEast", -32, 32, 32, 32, ExitMaskBits.South | ExitMaskBits.West, 3, 3),
                    new TileInput(OpenSno, "North", -32, 0, 32, 32, ExitMaskBits.South | ExitMaskBits.East, 4, 4),
                }
                : [],
        ]);
        foreach (var (x, y) in new[] { (16f, 16f), (16f, 48f), (-16f, 48f), (-16f, 16f) }) map.MarkVisited(x, y);
        var guide = new ExplorationGuide(map, walkability);
        guide.SetJumpLinks([Ladder]);
        return guide;
    }

    // A flesh web standing over the ladder's foot: no free floor within reach of it.
    private static readonly IReadOnlySet<(int X, int Y)> WebOnTheFoot =
        Enumerable.Range(14, 8).SelectMany(x => Enumerable.Range(5, 12).Select(y => (x, y))).ToHashSet();

    [Fact]
    public void A_platform_a_ladder_reaches_is_led_to_over_the_ladder()
    {
        var guide = Chamber(wayRound: true);

        var step = guide.Step(OnMainFloor, objectives: [Tumor]);

        Assert.Equal("tumor", step.Target!.Id);
        Assert.Equal(Ladder.A, step.JumpAt);
    }

    [Fact]
    public void A_ladder_a_standing_prop_buries_gives_way_to_the_way_round_through_the_next_tiles()
    {
        var guide = Chamber(wayRound: true);
        guide.SetPropCells(WebOnTheFoot);

        var step = guide.Step(OnMainFloor, objectives: [Tumor]);

        Assert.Equal("tumor", step.Target!.Id);
        Assert.Null(step.JumpAt);
        Assert.True(step.Waypoint.Y > 20f, $"waypoint {step.Waypoint}"); // east, towards the tiles round to the platform's way in
    }

    [Fact]
    public void A_buried_ladder_with_no_way_round_still_leads_over_it()
    {
        var guide = Chamber(wayRound: false);
        guide.SetPropCells(WebOnTheFoot);

        var step = guide.Step(OnMainFloor, objectives: [Tumor]);

        Assert.Equal("tumor", step.Target!.Id);
        Assert.Equal(Ladder.A, step.JumpAt);
    }

    [Fact]
    public void Once_the_prop_is_broken_the_ladder_leads_again()
    {
        var guide = Chamber(wayRound: true);
        guide.SetPropCells(WebOnTheFoot);
        guide.Step(OnMainFloor, objectives: [Tumor]);

        guide.SetPropCells(new HashSet<(int X, int Y)>());

        Assert.Equal(Ladder.A, guide.Step(OnMainFloor, objectives: [Tumor]).JumpAt);
    }

    // Tester log of 2026-11-14, the same chamber's north-west ladder: its web left free floor beside the foot, the guide led to
    // the foot three times, and each time the player stood there and walked away without getting up.
    private static readonly IReadOnlySet<(int X, int Y)> WebByTheFoot =
        Enumerable.Range(16, 3).SelectMany(x => Enumerable.Range(9, 3).Select(y => (x, y))).ToHashSet();
    private static readonly PlayerInput AtTheFoot = new(18.5f, 10.5f, 0), WalkedAway = new(29.5f, 10.5f, 0);

    private static ExplorationGuide ChamberWithAWebbedLadderTried(bool wayRound, PlayerInput then)
    {
        var guide = Chamber(wayRound);
        guide.SetPropCells(WebByTheFoot);
        Assert.Equal(Ladder.A, guide.Step(OnMainFloor, objectives: [Tumor]).JumpAt);
        guide.Step(AtTheFoot, objectives: [Tumor]);
        guide.Step(then, objectives: [Tumor]);
        return guide;
    }

    [Fact]
    public void A_webbed_ladder_stood_at_and_left_uncrossed_gives_way_to_the_way_round()
    {
        var closed = new List<JumpLink>();
        var guide = Chamber(wayRound: true);
        guide.TraversalClosed += closed.Add;
        guide.SetPropCells(WebByTheFoot);
        Assert.Equal(Ladder.A, guide.Step(OnMainFloor, objectives: [Tumor]).JumpAt);
        guide.Step(AtTheFoot, objectives: [Tumor]);

        var step = guide.Step(WalkedAway, objectives: [Tumor]);

        Assert.Equal([Ladder], closed);
        Assert.Empty(guide.JumpLinks);
        Assert.Equal("tumor", step.Target!.Id);
        Assert.Null(step.JumpAt);
        Assert.True(step.Waypoint.Y > 20f, $"waypoint {step.Waypoint}");
    }

    [Fact]
    public void A_webbed_ladder_given_up_with_no_way_round_no_longer_leads()
    {
        var guide = ChamberWithAWebbedLadderTried(wayRound: false, WalkedAway);

        var step = guide.Step(OnMainFloor, objectives: [Tumor]);

        Assert.Null(step.JumpAt);
        Assert.NotEqual("tumor", step.Target?.Id);
    }

    [Fact]
    public void A_webbed_ladder_only_passed_by_or_still_stood_near_keeps_leading()
    {
        var passedBy = Chamber(wayRound: true);
        passedBy.SetPropCells(WebByTheFoot);
        passedBy.Step(AtTheFoot with { X = 24.5f }, objectives: [Tumor]); // led over it, never at its foot
        Assert.Equal(Ladder.A, passedBy.Step(WalkedAway, objectives: [Tumor]).JumpAt);

        var near = ChamberWithAWebbedLadderTried(wayRound: true, AtTheFoot with { X = 17.5f + ExplorationGuide.TraversalLeaveUnits - 1f });
        Assert.Equal(Ladder.A, near.Step(OnMainFloor, objectives: [Tumor]).JumpAt);
    }

    [Fact]
    public void A_webbed_ladder_the_player_climbed_stays_open()
    {
        var guide = ChamberWithAWebbedLadderTried(wayRound: true, new PlayerInput(Ladder.B.X, Ladder.B.Y, 0));
        guide.Step(new PlayerInput(2.5f, 3.5f, 0), objectives: [Tumor]); // on along the platform, far from the foot

        Assert.Equal(Ladder.A, guide.Step(OnMainFloor, objectives: [Tumor]).JumpAt);
    }

    [Fact]
    public void A_ladder_given_up_leads_again_once_its_web_is_gone_or_the_guide_is_switched_on_again()
    {
        var freed = ChamberWithAWebbedLadderTried(wayRound: true, WalkedAway);
        freed.SetPropCells(new HashSet<(int X, int Y)>());
        Assert.Equal(Ladder.A, freed.Step(OnMainFloor, objectives: [Tumor]).JumpAt);

        var rearmed = ChamberWithAWebbedLadderTried(wayRound: true, WalkedAway);
        rearmed.Rearm();
        Assert.Equal(Ladder.A, rearmed.Step(OnMainFloor, objectives: [Tumor]).JumpAt);
    }

    private static float Distance((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
}
