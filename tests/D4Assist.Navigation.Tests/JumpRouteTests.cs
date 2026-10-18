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

    private static float Distance((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
}
