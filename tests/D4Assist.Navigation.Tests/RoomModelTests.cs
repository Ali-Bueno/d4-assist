using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class RoomModelTests
{
    [Fact]
    public void Single_tile_with_two_exits_has_two_openings()
    {
        var map = new DungeonMap();
        map.AddOrUpdate(new[]
        {
            new TileInput(1, "corridor", 0, 0, 32, 32, ExitMaskBits.North | ExitMaskBits.South, 1, 1),
        });

        Assert.True(RoomModel.TryGetRoom(map, 16, 16, out var room));
        Assert.False(room.IsMultiTile);
        Assert.Equal(2, room.Openings.Count);
        Assert.Contains(room.Openings, o => o.Side == ExitSide.North);
        Assert.Contains(room.Openings, o => o.Side == ExitSide.South);
    }

    [Fact]
    public void Two_same_node_id_tiles_that_do_not_touch_stay_separate_rooms()
    {
        // Fallback rule: NodeId alone does not merge unrelated tiles that happen to
        // share a (e.g. default) id but are not grid-adjacent.
        var map = new DungeonMap();
        map.AddOrUpdate(new[]
        {
            new TileInput(1, "a", 0, 0, 32, 32, 0, 0, 0),
            new TileInput(2, "b", 320, 320, 32, 32, 0, 0, 0),
        });

        Assert.True(RoomModel.TryGetRoom(map, 16, 16, out var roomA));
        Assert.True(RoomModel.TryGetRoom(map, 336, 336, out var roomB));
        Assert.False(roomA.IsMultiTile);
        Assert.False(roomB.IsMultiTile);
        Assert.NotEqual(roomA.Key, roomB.Key);
    }

    [Fact]
    public void A_64_unit_tile_places_its_opening_at_the_edge_midpoint()
    {
        var map = new DungeonMap();
        map.AddOrUpdate(new[] { new TileInput(1, "big", 0, 0, 64, 64, ExitMaskBits.East, 1, 1) });

        Assert.True(RoomModel.TryGetRoom(map, 10, 10, out var room));
        var opening = Assert.Single(room.Openings);
        Assert.Equal(ExitSide.East, opening.Side);
        Assert.Equal((32f, 64f), opening.Position); // (OriginX + SizeX/2, OriginY + SizeY)
    }

    [Fact]
    public void Four_tiles_sharing_a_node_id_form_one_multi_tile_room_with_only_outward_openings()
    {
        // Mirrors the observed 2x2 room, node 0xD5 / group 0x14D (client-memory.md §1).
        const uint node = 0xD5, group = 0x14D;
        var map = new DungeonMap();
        map.AddOrUpdate(new[]
        {
            // North + internal East (into the tile at (0,32))
            new TileInput(1, "a", 0, 0, 32, 32, ExitMaskBits.North | ExitMaskBits.East, node, group),
            // South (outward) + internal East (into the tile at (32,32))
            new TileInput(2, "b", 32, 0, 32, 32, ExitMaskBits.South | ExitMaskBits.East, node, group),
            // East (outward) + internal West (into the tile at (0,0))
            new TileInput(3, "c", 0, 32, 32, 32, ExitMaskBits.East | ExitMaskBits.West, node, group),
            // South + East (both outward)
            new TileInput(4, "d", 32, 32, 32, 32, ExitMaskBits.South | ExitMaskBits.East, node, group),
        });

        Assert.True(RoomModel.TryGetRoom(map, 16, 16, out var room));
        Assert.True(room.IsMultiTile);
        Assert.Equal(4, room.Tiles.Count);

        // Only the 5 outward-facing exits become openings; the 3 internal ones
        // (a.East->c, b.East->d, c.West->a) are filtered because their neighbour
        // is a member of the same room.
        Assert.Equal(5, room.Openings.Count);
        Assert.All(room.Openings, o => Assert.True(o.Unexplored)); // no neighbour tile loaded beyond the room
    }

    [Fact]
    public void Opening_is_not_unexplored_once_the_neighbouring_tile_is_loaded()
    {
        var map = new DungeonMap();
        map.AddOrUpdate(new[]
        {
            new TileInput(1, "a", 0, 0, 32, 32, ExitMaskBits.North, 1, 1),
            new TileInput(2, "neighbour", -32, 0, 32, 32, ExitMaskBits.South, 2, 2),
        });

        Assert.True(RoomModel.TryGetRoom(map, 16, 16, out var room));
        var opening = Assert.Single(room.Openings);
        Assert.False(opening.Unexplored);
    }
}
