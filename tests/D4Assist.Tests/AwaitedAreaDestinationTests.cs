using D4Assist.Game;

namespace D4Assist.Tests;

public class AwaitedAreaDestinationTests
{
    // Live 2026-11-01, S15_Triad_A_Labyrinth phase 28: the Summoner's marker in the area the step waits for.
    private const int BossArea = 2682147, OtherArea = 2, Summoner = 2681765, Pet = 2417957;
    private static readonly MarkedActor Boss = new(Summoner, -557.5f, -464.5f, 0, BossArea, Kind: 8);

    [Fact]
    public void A_marker_in_the_awaited_area_is_a_located_quest_objective()
    {
        var places = new AwaitedAreaDestination();
        places.OnMarkers([Boss, new MarkedActor(Summoner + 1, 1, 1, 0, OtherArea, Kind: 8)]);

        var place = Assert.Single(places.Destinations(new HashSet<int> { BossArea }));

        Assert.Equal((-557.5f, -464.5f), place.Position);
        Assert.Equal(ObjectiveMemory.QuestTier, place.Tier);
        Assert.True(place.Located);
    }

    [Fact]
    public void Nothing_leads_while_no_step_awaits_an_area()
    {
        var places = new AwaitedAreaDestination();
        places.OnMarkers([Boss]);

        Assert.Empty(places.Destinations(new HashSet<int>()));
    }

    [Fact]
    public void The_players_own_markers_in_the_area_do_not_lead()
    {
        var places = new AwaitedAreaDestination();
        places.OnMarkers([new MarkedActor(Pet, 1, 1, 0, BossArea, Kind: 2)]);

        Assert.Empty(places.Destinations(new HashSet<int> { BossArea }));
    }

    [Fact]
    public void A_marked_place_stays_known_once_the_game_stops_listing_it()
    {
        var places = new AwaitedAreaDestination();
        places.OnMarkers([Boss]);

        places.OnMarkers([]);

        Assert.Single(places.Destinations(new HashSet<int> { BossArea }));
    }

    [Fact]
    public void A_marked_monster_counts_only_while_listed()
    {
        var places = new AwaitedAreaDestination();
        places.OnMarkers([Boss with { Kind = D4Memory.Actors.MapMarkerReader.KindBoss }]);
        Assert.Single(places.Destinations(new HashSet<int> { BossArea }));

        places.OnMarkers([]);

        Assert.Empty(places.Destinations(new HashSet<int> { BossArea }));
    }

    [Fact]
    public void A_new_world_forgets_the_markers()
    {
        var places = new AwaitedAreaDestination();
        places.OnMarkers([Boss]);

        places.Reset();

        Assert.Empty(places.Destinations(new HashSet<int> { BossArea }));
    }
}
