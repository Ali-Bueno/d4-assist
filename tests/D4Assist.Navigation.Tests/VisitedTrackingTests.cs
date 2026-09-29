using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class VisitedTrackingTests
{
    private static TileInput Tile(int cx, int cy, int mask, float size = 32) =>
        new(1, "t", cx * 32, cy * 32, size, size, mask, (uint)(100 + cx * 10 + cy), 0);

    [Fact]
    public void Standing_in_a_tile_marks_it_visited_once_and_survives_reloads()
    {
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, 0, 0), Tile(0, 1, 0)]);

        Assert.True(map.MarkVisited(16, 16));
        Assert.False(map.MarkVisited(20, 20));
        map.AddOrUpdate([Tile(0, 0, 0)]);

        Assert.True(map.IsVisited(map.TileAt(16, 16)!));
        Assert.False(map.IsVisited(map.TileAt(16, 48)!));
    }

    [Fact]
    public void A_multi_cell_tile_is_visited_from_any_of_its_cells()
    {
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, 0, 0, size: 64)]);

        map.MarkVisited(50, 50);

        Assert.True(map.IsVisited(map.TileAt(5, 5)!));
    }

    [Fact]
    public void An_unloaded_position_is_not_a_visit()
    {
        var map = new DungeonMap();

        Assert.False(map.MarkVisited(16, 16));
    }

    [Fact]
    public void Reset_clears_visited_and_bumps_the_version()
    {
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, 0, 0)]);
        map.MarkVisited(16, 16);
        int before = map.Version;

        map.Reset();
        map.AddOrUpdate([Tile(0, 0, 0)]);

        Assert.False(map.IsVisited(map.TileAt(16, 16)!));
        Assert.True(map.Version > before);
    }

    [Fact]
    public void Version_only_moves_when_something_changed()
    {
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(0, 0, 0)]);
        int version = map.Version;

        map.AddOrUpdate([Tile(0, 0, 0)]);
        Assert.Equal(version, map.Version);

        map.AddOrUpdate([Tile(0, 0, ExitMaskBits.East)]);
        Assert.NotEqual(version, map.Version);
    }

    [Fact]
    public void Engine_tracks_visits_from_the_player_feed_and_forgets_them_on_reset()
    {
        var engine = new NavigationEngine();
        TileInput[] tiles = [Tile(0, 0, ExitMaskBits.East), Tile(0, 1, ExitMaskBits.West)];
        engine.OnTiles(tiles);
        engine.OnPlayer(new PlayerInput(16, 16, 0), TimeSpan.Zero);
        Assert.Equal("tile:0,1", engine.Guide.Step(new PlayerInput(16, 16, 0)).Target!.Id);

        engine.OnPlayer(new PlayerInput(16, 48, 0), TimeSpan.FromSeconds(5));
        Assert.Equal(GuideStatus.AllExplored, engine.Guide.Step(new PlayerInput(16, 48, 0)).Status);

        engine.Reset();
        engine.OnTiles(tiles);
        engine.OnPlayer(new PlayerInput(16, 16, 0), TimeSpan.FromSeconds(9));
        Assert.Equal("tile:0,1", engine.Guide.Step(new PlayerInput(16, 16, 0)).Target!.Id);
    }
}
