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
}
