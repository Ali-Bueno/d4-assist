using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class RoomClassifierTests
{
    private static Room SingleTileRoom(int openingCount)
    {
        var tile = new MapTile(new TileInput(1, "t", 0, 0, 32, 32, 0, 1, 1));
        var openings = Enumerable.Range(0, openingCount)
            .Select(i => new RoomOpening((ExitSide)(i % 4), (0f, 0f), false))
            .ToList();
        return new Room(new RoomKey(0, 0, true), new[] { tile }, openings);
    }

    private static Room MultiTileRoom(int openingCount)
    {
        var tiles = new[]
        {
            new MapTile(new TileInput(1, "a", 0, 0, 32, 32, 0, 1, 1)),
            new MapTile(new TileInput(2, "b", 32, 0, 32, 32, 0, 1, 1)),
        };
        var openings = Enumerable.Range(0, openingCount)
            .Select(i => new RoomOpening((ExitSide)(i % 4), (0f, 0f), false))
            .ToList();
        return new Room(new RoomKey(0, 0, true), tiles, openings);
    }

    [Fact]
    public void Zero_or_one_opening_is_a_dead_end()
    {
        Assert.Equal(RoomKind.DeadEnd, RoomClassifier.Classify(SingleTileRoom(0)));
        Assert.Equal(RoomKind.DeadEnd, RoomClassifier.Classify(SingleTileRoom(1)));
    }

    [Fact]
    public void Two_openings_is_a_corridor()
    {
        Assert.Equal(RoomKind.Corridor, RoomClassifier.Classify(SingleTileRoom(2)));
    }

    [Fact]
    public void Three_or_more_openings_is_a_junction()
    {
        Assert.Equal(RoomKind.Junction, RoomClassifier.Classify(SingleTileRoom(3)));
        Assert.Equal(RoomKind.Junction, RoomClassifier.Classify(SingleTileRoom(5)));
    }

    [Fact]
    public void A_multi_tile_room_is_always_a_big_room_regardless_of_opening_count()
    {
        Assert.Equal(RoomKind.BigRoom, RoomClassifier.Classify(MultiTileRoom(1)));
        Assert.Equal(RoomKind.BigRoom, RoomClassifier.Classify(MultiTileRoom(4)));
    }
}
