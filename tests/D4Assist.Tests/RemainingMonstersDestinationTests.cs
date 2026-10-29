using D4Assist.Game;
using D4Memory.Actors;

namespace D4Assist.Tests;

public class RemainingMonstersDestinationTests
{
    private const string DenOfEvil = "S15_Rift_DenOfEvil", Ordinary = "DGN_Frac_LightsWatch";
    private const int Corpsefire = 2658376, Shaman = 174767, Player = 2257570;

    private sealed class Feed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated { add { } remove { } }
        public event Action<PlayerState>? PlayerMoved { add { } remove { } }
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated { add { } remove { } }
        public (string Name, bool IsDungeon)? CurrentWorld { get; set; } = (DenOfEvil, true);
        public string? CurrentInstance => null;

        public void RaiseWorld(string name, bool isDungeon = true) => WorldChanged?.Invoke(0, name, isDungeon);
    }

    private static MarkedActor Mark(int sno, float x, float y, int kind = MapMarkerReader.KindBoss) => new(sno, x, y, 0, 1, kind);

    private static RemainingMonstersDestination New(Feed feed) => new(feed, name => name == DenOfEvil);

    [Fact]
    public void A_kill_every_monster_dungeon_leads_to_the_monsters_its_map_marks_before_the_portals()
    {
        // Live 2026-10-22, the seasonal Den of Evil: markers kind 10 for Corpsefire (22,16) and a shaman (46,18.5).
        var monsters = New(new Feed());

        monsters.OnMarkers([Mark(Corpsefire, 22, 16), Mark(Shaman, 46, 18.5f), Mark(Player, -14.8f, 103.8f, kind: 1), Mark(9, 147.8f, 20, kind: 11)]);

        var spots = monsters.Destinations();
        Assert.Equal([(22f, 16f), (46f, 18.5f)], spots.Select(s => s.Position));
        Assert.All(spots, s => Assert.StartsWith(RemainingMonstersDestination.IdPrefix, s.Id));
        Assert.All(spots, s => Assert.True(s.Tier < BossRoomDestination.Tier));
    }

    [Fact]
    public void A_monster_killed_leaves_the_list_and_one_that_moved_is_a_new_destination()
    {
        var monsters = New(new Feed());
        monsters.OnMarkers([Mark(Corpsefire, 22, 16), Mark(Shaman, 46, 18.5f)]);
        string corpsefire = monsters.Destinations()[0].Id;

        monsters.OnMarkers([Mark(Corpsefire, 40, 16)]);

        Assert.NotEqual(corpsefire, Assert.Single(monsters.Destinations()).Id);
    }

    [Fact]
    public void Other_dungeons_and_endgame_worlds_lead_to_no_monster()
    {
        var feed = new Feed { CurrentWorld = (Ordinary, true) };
        var monsters = New(feed);
        monsters.OnMarkers([Mark(Corpsefire, 22, 16)]);
        Assert.Empty(monsters.Destinations());

        feed.RaiseWorld(DenOfEvil);
        monsters.OnMarkers([Mark(Corpsefire, 22, 16)]);
        Assert.Single(monsters.Destinations());

        feed.RaiseWorld("Sanctuary_Eastern_Continent", isDungeon: false);
        monsters.OnMarkers([Mark(Corpsefire, 22, 16)]);
        Assert.Empty(monsters.Destinations());
    }
}
