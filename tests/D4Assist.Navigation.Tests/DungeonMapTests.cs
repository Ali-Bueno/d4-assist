using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class DungeonMapTests
{
    [Fact]
    public void Tile_persists_after_dropping_out_of_a_later_window()
    {
        var map = new DungeonMap();
        var first = new TileInput(1, "a", 0, 0, 32, 32, ExitMaskBits.North, 1, 1);
        var second = new TileInput(2, "b", 32, 0, 32, 32, ExitMaskBits.South, 2, 2);

        map.AddOrUpdate(new[] { first }); // window 1: only the first tile loaded
        map.AddOrUpdate(new[] { second }); // window 2: first tile scrolled out, only second loaded

        Assert.Equal(2, map.Tiles.Count);
        Assert.NotNull(map.TileAt(16, 16)); // still remembered
        Assert.NotNull(map.TileAt(48, 16));
    }

    [Fact]
    public void Re_seeing_the_same_tile_updates_it_in_place()
    {
        var map = new DungeonMap();
        map.AddOrUpdate(new[] { new TileInput(1, "a", 0, 0, 32, 32, ExitMaskBits.North, 1, 1) });
        map.AddOrUpdate(new[] { new TileInput(1, "a", 0, 0, 32, 32, ExitMaskBits.North | ExitMaskBits.East, 1, 1) });

        Assert.Single(map.Tiles);
        Assert.Equal(ExitMaskBits.North | ExitMaskBits.East, map.TileAt(16, 16)!.Input.ExitMask);
    }

    [Fact]
    public void A_64_unit_tile_covers_all_four_32_unit_cells_it_spans()
    {
        var map = new DungeonMap();
        map.AddOrUpdate(new[] { new TileInput(1, "big", 0, 0, 64, 64, 0, 1, 1) });

        Assert.NotNull(map.TileAt(0, 0));
        Assert.NotNull(map.TileAt(40, 0));
        Assert.NotNull(map.TileAt(0, 40));
        Assert.NotNull(map.TileAt(40, 40));
        Assert.Null(map.TileAt(64, 40)); // just past the tile's far edge
    }

    [Fact]
    public void Reset_clears_every_accumulated_tile()
    {
        var map = new DungeonMap();
        map.AddOrUpdate(new[] { new TileInput(1, "a", 0, 0, 32, 32, 0, 1, 1) });
        map.Reset();

        Assert.Empty(map.Tiles);
        Assert.Null(map.TileAt(16, 16));
    }
}
