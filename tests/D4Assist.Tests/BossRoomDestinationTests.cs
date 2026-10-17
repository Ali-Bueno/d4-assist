using System.Numerics;
using D4Assist.Game;

namespace D4Assist.Tests;

public class BossRoomDestinationTests
{
    private const int BossSno = 20, PlainSno = 21, BareBossSno = 22;

    // Live 2026-10-17, the Light's Watch: Pale_Frac_FLEX_POI_BossRoom at (-32,-128), 64x64; its Trigger_BossRoom_Entrance at
    // (45.3, 49.2) in the tile, (13.3, -78.8) in the world as the log saw it.
    private static readonly TileState BossTile = new(BossSno, "Pale_Frac_FLEX_POI_BossRoom", -32, -128, 64, 64, 6, 6, 0xD);

    private sealed class Feed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated;
        public event Action<PlayerState>? PlayerMoved { add { } remove { } }
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated { add { } remove { } }
        public (string Name, bool IsDungeon)? CurrentWorld { get; set; } = ("DGN_Frac_LightsWatch", true);
        public string? CurrentInstance => null;

        public void RaiseWorld(string name) => WorldChanged?.Invoke(0, name, true);
        public void RaiseTiles(params TileState[] tiles) => TilesUpdated?.Invoke(tiles);
    }

    private static IReadOnlyList<TileActor> Entrances(int scene) =>
        scene == BossSno ? [new TileActor(9, new Vector3(45.3f, 49.2f, 0.1f))] : [];

    [Fact]
    public void A_boss_room_tile_leads_to_its_entrance_trigger()
    {
        var feed = new Feed();
        var boss = new BossRoomDestination(feed, Entrances);

        feed.RaiseTiles(new TileState(PlainSno, "Pale_Frac_NE_01", 0, -64, 32, 32, 5, 1, 1), BossTile);

        var room = Assert.Single(boss.Destinations());
        Assert.StartsWith(BossRoomDestination.IdPrefix, room.Id);
        Assert.Equal(BossRoomDestination.Tier, room.Tier);
        Assert.Equal(13.3f, room.Position.X, 0.01f);
        Assert.Equal(-78.8f, room.Position.Y, 0.01f);
    }

    [Fact]
    public void A_boss_room_without_an_entrance_trigger_leads_to_its_middle()
    {
        var feed = new Feed();
        var boss = new BossRoomDestination(feed, Entrances);

        feed.RaiseTiles(new TileState(BareBossSno, "ProtoDun_Keep_FLEX_POI_BossRoom", 100, 200, 64, 64, 1, 2, 2));

        Assert.Equal((132f, 232f), Assert.Single(boss.Destinations()).Position);
    }

    [Fact]
    public void Undercity_and_endgame_worlds_and_other_tiles_lead_nowhere()
    {
        var feed = new Feed();
        var boss = new BossRoomDestination(feed, Entrances);
        feed.RaiseTiles(new TileState(BossSno, "Cave_Bug" + ExplorationGuideBridge.UndercityBossRoomSuffix, 0, 0, 64, 64, 1, 1, 1),
            new TileState(PlainSno, "Pale_Frac_E_Arena_01", 32, -96, 32, 32, 1, 1, 1));
        Assert.Empty(boss.Destinations());

        feed.RaiseWorld("PIT_Test");
        feed.RaiseTiles(BossTile);
        Assert.Empty(boss.Destinations());
    }

    [Fact]
    public void A_new_world_forgets_the_boss_room_and_a_repeated_tile_poll_adds_it_once()
    {
        var feed = new Feed();
        var boss = new BossRoomDestination(feed, Entrances);
        feed.RaiseTiles(BossTile);
        feed.RaiseTiles(BossTile);
        Assert.Single(boss.Destinations());

        feed.RaiseWorld("DGN_Frac_DerelictLodge");

        Assert.Empty(boss.Destinations());
    }
}
