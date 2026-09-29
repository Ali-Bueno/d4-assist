using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class SeenNotStoodInTests
{
    private const int E = ExitMaskBits.East, W = ExitMaskBits.West;
    private const float Half = GridMath.CellSize / 2f;

    private static TileInput Tile(int cx, int cy, int mask, float size = 32) =>
        new(1, "t", cx * 32, cy * 32, size, size, mask, (uint)(1000 + cx * 100 + cy), 0);

    private static DungeonMap Map(params TileInput[] tiles)
    {
        var map = new DungeonMap();
        map.AddOrUpdate(tiles);
        return map;
    }

    [Fact]
    public void A_doorway_peek_marks_a_connected_dead_end_explored_and_chimes()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W));
        map.MarkVisited(Half, Half);
        var guide = new ExplorationGuide(map);
        Assert.Equal("tile:0,1", guide.Step(new PlayerInput(Half, Half, 0)).Target!.Id);

        var atTheDoorway = guide.Step(new PlayerInput(Half, 30, 0)); // 18 from the dead end's centre

        Assert.True(map.IsVisited(map.TileAt(Half, 48)!));
        Assert.Equal(GuideStatus.AllExplored, atTheDoorway.Status);
        Assert.Equal(1, atTheDoorway.Arrivals);
    }

    [Fact]
    public void Too_far_from_the_centre_is_not_a_peek()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W));
        map.MarkVisited(Half, Half);

        new ExplorationGuide(map).Step(new PlayerInput(Half, 26, 0)); // 22 from the centre

        Assert.False(map.IsVisited(map.TileAt(Half, 48)!));
    }

    [Fact]
    public void A_tile_across_a_wall_is_not_marked()
    {
        var map = Map(Tile(0, 0, 0), Tile(0, 1, 0)); // adjacent cells, no opening between them
        map.MarkVisited(Half, Half);

        new ExplorationGuide(map).Step(new PlayerInput(Half, 31, 0)); // 17 from the far tile's centre

        Assert.False(map.IsVisited(map.TileAt(Half, 48)!));
    }

    [Fact]
    public void A_big_tile_still_needs_entering()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W, size: 64)); // centre at (32, 64)
        map.MarkVisited(Half, Half);
        var guide = new ExplorationGuide(map);

        var step = guide.Step(new PlayerInput(Half, 31.5f, 0));

        Assert.False(map.IsVisited(map.TileAt(Half, 48)!));
        Assert.Equal("tile:0,1", step.Target!.Id);
    }

    [Fact]
    public void A_peeked_tile_with_an_unloaded_exit_becomes_a_frontier_target()
    {
        var map = Map(Tile(0, 0, E), Tile(0, 1, W | E));
        map.MarkVisited(Half, Half);
        var guide = new ExplorationGuide(map);
        guide.Step(new PlayerInput(Half, Half, 0));

        var step = guide.Step(new PlayerInput(Half, 30, 0));

        Assert.Equal(GuideTargetKind.Frontier, step.Target!.Kind);
        Assert.Equal(2 * 32f, step.Target.Position.Y);
        Assert.Equal(1, step.Arrivals);
    }

    [Fact]
    public void Two_targets_whose_costs_cross_by_less_than_a_tile_never_flip_flop()
    {
        // Corridor open at both ends; the player wobbles around the middle so the east/west route difference stays under a tile.
        var map = Map(Enumerable.Range(0, 5).Select(cy => Tile(0, cy, W | E)).ToArray());
        for (var cy = 0; cy < 5; cy++) map.MarkVisited(Half, cy * 32 + Half);
        var guide = new ExplorationGuide(map);
        var middle = 2 * 32 + Half;
        var first = guide.Step(new PlayerInput(Half, middle, 0)).Target!.Id;

        foreach (var offset in new float[] { 8, -8, 12, -12, 5, -11, 12, -12, 0 })
            Assert.Equal(first, guide.Step(new PlayerInput(Half, middle + offset, 0)).Target!.Id);
    }

    [Fact]
    public void A_target_close_by_is_kept_while_it_is_still_a_candidate()
    {
        var map = Map(Enumerable.Range(0, 5).Select(cy => Tile(0, cy, W | E)).ToArray());
        for (var cy = 0; cy < 5; cy++) map.MarkVisited(Half, cy * 32 + Half);
        var guide = new ExplorationGuide(map);
        var first = guide.Step(new PlayerInput(Half, 4 * 32 + 20, 0)).Target!; // east frontier, 12 away
        Assert.True(first.Position.Y > 100);

        var step = guide.Step(new PlayerInput(Half, 4 * 32 + 20 - 40, 0)); // a bit back, still nearer the east end

        Assert.Equal(first.Id, step.Target!.Id);
        Assert.True(ExplorationGuide.CommitDistanceUnits >= ExplorationGuide.SwitchMarginUnits);
    }
}
