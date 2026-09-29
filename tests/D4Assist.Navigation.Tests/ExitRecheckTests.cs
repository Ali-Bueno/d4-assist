using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class ExitRecheckTests
{
    private const int E = ExitMaskBits.East, W = ExitMaskBits.West, ExitScene = 9;
    private const float Half = GridMath.CellSize / 2f;

    private static TileInput Tile(int cy, int mask, int sno) =>
        new(sno, "t", 0, cy * 32, 32, 32, mask, (uint)(1000 + cy), 0);

    private static PlayerInput Centre(int cy) => new(Half, cy * 32 + Half, 0);

    // A closed corridor of five explored tiles along +Y whose two dead ends (cy 0 and 4) can host the exit.
    private static (DungeonMap Map, ExplorationGuide Guide) Closed(Func<int, bool>? isCandidate = null, int endScene = ExitScene)
    {
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, E, endScene), Tile(1, W | E, 1), Tile(2, W | E, 1), Tile(3, W | E, 1), Tile(4, W, endScene)]);
        for (int cy = 0; cy < 5; cy++) map.MarkVisited(Half, cy * 32 + Half);
        return (map, new ExplorationGuide(map, null, isCandidate ?? (sno => sno == ExitScene)));
    }

    [Fact]
    public void Leads_to_the_nearest_exit_candidate_when_nothing_is_left_and_the_exit_is_missing()
    {
        var (_, guide) = Closed();

        var step = guide.Step(Centre(1), exitMissing: true);

        Assert.Equal(GuideStatus.Rechecking, step.Status);
        Assert.Equal(GuideTargetKind.ExitCandidate, step.Target!.Kind);
        Assert.Equal("exit-candidate:0,0", step.Target.Id);
        Assert.Equal(1, step.Round);
    }

    [Fact]
    public void Each_candidate_is_an_arrival_and_a_full_round_starts_the_next()
    {
        var (_, guide) = Closed();
        guide.Step(Centre(0), exitMissing: true);

        var toFar = guide.Step(Centre(0), exitMissing: true);
        Assert.Equal("exit-candidate:0,4", toFar.Target!.Id);
        Assert.Equal(1, toFar.Arrivals);

        guide.Step(Centre(4), exitMissing: true);
        var next = guide.Step(Centre(4), exitMissing: true);

        Assert.Equal(GuideStatus.Rechecking, next.Status);
        Assert.Equal(2, next.Round);
        Assert.Equal(2, next.Arrivals);
        Assert.Equal("exit-candidate:0,0", next.Target!.Id);
    }

    [Fact]
    public void A_single_candidate_under_the_player_is_not_re_arrived_until_the_player_leaves()
    {
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, E, ExitScene), Tile(1, W | E, 1), Tile(2, W | E, 1), Tile(3, W, 1)]);
        for (int cy = 0; cy < 4; cy++) map.MarkVisited(Half, cy * 32 + Half);
        var guide = new ExplorationGuide(map, null, sno => sno == ExitScene);

        guide.Step(Centre(0), exitMissing: true);
        for (int i = 0; i < 3; i++) Assert.Equal(1, guide.Step(Centre(0), exitMissing: true).Arrivals);

        guide.Step(Centre(3), exitMissing: true);
        var back = guide.Step(Centre(0), exitMissing: true);
        Assert.Equal(2, back.Arrivals);
    }

    [Fact]
    public void Stays_all_explored_when_no_candidate_tile_is_known()
    {
        var (_, guide) = Closed(isCandidate: _ => false);

        Assert.Equal(GuideStatus.AllExplored, guide.Step(Centre(1), exitMissing: true).Status);
    }

    [Fact]
    public void Stays_all_explored_when_the_caller_does_not_miss_the_exit()
    {
        var (_, guide) = Closed();

        Assert.Equal(GuideStatus.AllExplored, guide.Step(Centre(1)).Status);
    }

    [Fact]
    public void Exploration_still_comes_first_and_re_check_ends_when_the_exit_is_no_longer_missing()
    {
        var (map, guide) = Closed();
        Assert.Equal(GuideStatus.Rechecking, guide.Step(Centre(1), exitMissing: true).Status);

        Assert.Equal(GuideStatus.AllExplored, guide.Step(Centre(1), exitMissing: false).Status);

        map.AddOrUpdate([Tile(4, W | E, ExitScene), Tile(5, W, 1)]);
        Assert.Equal("tile:0,5", guide.Step(Centre(4), exitMissing: true).Target!.Id);
    }

    [Fact]
    public void Reset_starts_over_at_round_one()
    {
        var (_, guide) = Closed();
        foreach (int cy in new[] { 0, 0, 4, 4 }) guide.Step(Centre(cy), exitMissing: true);
        Assert.Equal(2, guide.Step(Centre(4), exitMissing: true).Round);
        guide.Reset();

        Assert.Equal(1, guide.Step(Centre(0), exitMissing: true).Round);
    }
}
