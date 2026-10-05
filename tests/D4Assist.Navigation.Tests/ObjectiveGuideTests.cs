using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class ObjectiveGuideTests
{
    private const int E = ExitMaskBits.East, W = ExitMaskBits.West;
    private const float Half = GridMath.CellSize / 2f;

    private static TileInput Tile(int cy, int mask) => new(1, "t", 0, cy * 32, 32, 32, mask, (uint)(1000 + cy), 0);

    private static PlayerInput Centre(int cy) => new(Half, cy * 32 + Half, 0);

    private static (float X, float Y) CentreOf(int cy) => (Half, cy * 32 + Half);

    // A corridor of four tiles along +Y; the first three visited, the last one still unexplored.
    private static (DungeonMap Map, ExplorationGuide Guide) Corridor()
    {
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, E), Tile(1, W | E), Tile(2, W | E), Tile(3, W)]);
        foreach (int cy in new[] { 0, 1, 2 }) map.MarkVisited(Half, cy * 32 + Half);
        return (map, new ExplorationGuide(map));
    }

    [Fact]
    public void An_objective_seen_earlier_leads_before_unexplored_ground()
    {
        var (_, guide) = Corridor();
        Assert.Equal(GuideTargetKind.UnvisitedTile, guide.Step(Centre(2)).Target!.Kind);

        var step = guide.Step(Centre(2), objectives: [new("lever", CentreOf(0), 1)]);

        Assert.Equal(GuideStatus.Guiding, step.Status);
        Assert.Equal(GuideTargetKind.Objective, step.Target!.Kind);
        Assert.Equal("lever", step.Target.Id);
    }

    [Fact]
    public void The_quest_step_leads_before_a_nearer_dungeon_objective()
    {
        var (_, guide) = Corridor();

        var step = guide.Step(Centre(2), objectives: [new("prisoner", CentreOf(1), 1), new("quest", CentreOf(0), 0)]);

        Assert.Equal("quest", step.Target!.Id);
    }

    [Fact]
    public void An_objective_on_an_unknown_tile_waits_and_exploring_goes_on()
    {
        var (_, guide) = Corridor();

        var step = guide.Step(Centre(2), objectives: [new("far", (9 * 32 + Half, Half), 0)]);

        Assert.Equal(GuideTargetKind.UnvisitedTile, step.Target!.Kind);
    }

    [Fact]
    public void An_objective_dropped_beside_the_player_is_an_arrival_and_one_dropped_far_away_is_not()
    {
        var (_, guide) = Corridor();
        GuideDestination[] lever = [new("lever", CentreOf(0), 1)];
        Assert.Equal(0, guide.Step(Centre(2), objectives: lever).Arrivals);

        Assert.Equal(0, guide.Step(Centre(2)).Arrivals); // done elsewhere (another player)

        guide.Step(Centre(0), objectives: lever);
        Assert.Equal(0, guide.Step(Centre(0), objectives: lever).Arrivals); // still offered: not yet
        Assert.Equal(1, guide.Step(Centre(0)).Arrivals);
    }

    [Fact]
    public void In_the_players_room_the_guide_walks_round_what_stands_before_the_objective()
    {
        // One room, a wall across it (local Y 16) open only at its west end (X 0-3); the player south of it, the lever north.
        var cells = new bool[32, 32];
        for (var x = 0; x < 32; x++)
            for (var y = 0; y < 32; y++) cells[x, y] = y != 16 || x < 4;
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, E)]);
        var guide = new ExplorationGuide(map, new FakeTileWalkability().Add(1, new WalkGrid(1f, 0f, 0f, 32, 32, cells)));

        var step = guide.Step(new PlayerInput(8.5f, 8.5f, 0), objectives: [new("lever", (8, 24), 0, Radius: 3)]);

        Assert.Equal("lever", step.Target!.Id);
        Assert.True(step.Waypoint.X < 4, $"heads for the gap, not through the wall: {step.Waypoint}");
        Assert.True(step.RemainingLength > 16, "the route round the wall is longer than the straight line");
    }

    [Fact]
    public void An_objective_known_beyond_the_explored_map_draws_exploring_towards_it()
    {
        // Seven tiles along +Y, the player in tile 4: unexplored tile 6 is nearer, tile 0 lies on the way to the lever the map marks.
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, E), .. Enumerable.Range(1, 5).Select(cy => Tile(cy, W | E)), Tile(6, W)]);
        foreach (int cy in Enumerable.Range(1, 5)) map.MarkVisited(Half, cy * 32 + Half);
        var guide = new ExplorationGuide(map);
        Assert.Equal("tile:0,6", guide.Step(Centre(4)).Target!.Id);

        Assert.Equal("tile:0,6", guide.Step(Centre(4), objectives: [new("script", (Half, -200), 0)]).Target!.Id); // maybe another world

        var step = guide.Step(Centre(4), objectives: [new("lever", (Half, -200), 0, Located: true)]);

        Assert.Equal("tile:0,0", step.Target!.Id);
        Assert.Equal("lever", step.Toward!.Id);
    }
}
