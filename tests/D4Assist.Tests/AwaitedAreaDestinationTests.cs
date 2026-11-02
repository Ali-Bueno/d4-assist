using D4Assist.Game;
using D4Memory.Actors;

namespace D4Assist.Tests;

public class AwaitedAreaDestinationTests
{
    // Live 2026-11-01, S15_Triad_A_Labyrinth phase 28: the Summoner's marker in the area the step waits for.
    private const int BossArea = 2682147, OtherArea = 2, Summoner = 2681765;
    private static readonly MarkedActor Boss = new(Summoner, -557.5f, -464.5f, 0, BossArea, Kind: AwaitedAreaDestination.KindQuestCharacter);
    private static readonly HashSet<int> Awaited = [BossArea];

    private const int TriggerBox = 44459;
    private static readonly MarkedActor Box = new(TriggerBox, -1133.3f, -1018.8f, 0, BossArea, Kind: MapMarkerReader.KindGizmo);

    private static AwaitedAreaDestination Create() => new(sno => sno == TriggerBox ? "Trigger_Box_2m" : "S15_Portal_ToLibrary");

    private static MarkedActor Player(float x, float y) => new(2257570, x, y, 0, BossArea, Kind: AwaitedAreaDestination.KindLocalPlayer);

    [Fact]
    public void A_quest_character_in_the_awaited_area_is_a_located_quest_objective()
    {
        var places = Create();
        places.OnMarkers([Boss, Boss with { ActorSno = Summoner + 1, LevelAreaSno = OtherArea }]);

        var place = Assert.Single(places.Destinations(Awaited));

        Assert.Equal((-557.5f, -464.5f), place.Position);
        Assert.Equal(ObjectiveMemory.QuestTier, place.Tier);
        Assert.True(place.Located);
    }

    [Fact]
    public void Nothing_leads_while_no_step_awaits_an_area()
    {
        var places = Create();
        places.OnMarkers([Boss]);

        Assert.Empty(places.Destinations(new HashSet<int>()));
    }

    [Fact]
    public void The_player_companions_and_portals_in_the_area_do_not_lead()
    {
        // Live 2026-11-01, Diablo's arena: the guide led to the player's own marker.
        var places = Create();
        places.OnMarkers([Player(0, 0), Boss with { Kind = 2 }, Boss with { Kind = MapMarkerReader.KindGizmo }]);

        Assert.Empty(places.Destinations(Awaited));
    }

    [Fact]
    public void A_quest_character_stays_known_once_the_game_stops_listing_it()
    {
        var places = Create();
        places.OnMarkers([Boss]);

        places.OnMarkers([]);

        Assert.Single(places.Destinations(Awaited));
    }

    [Fact]
    public void A_monster_leads_only_while_listed_and_while_no_quest_character_is_known()
    {
        var places = Create();
        var monster = Boss with { ActorSno = 7, X = 0, Y = 0, Kind = MapMarkerReader.KindBoss };
        places.OnMarkers([monster]);
        Assert.Equal((0f, 0f), Assert.Single(places.Destinations(Awaited)).Position);

        places.OnMarkers([monster, Boss]);
        Assert.Equal((-557.5f, -464.5f), Assert.Single(places.Destinations(Awaited)).Position);

        var without = Create();
        without.OnMarkers([monster]);
        without.OnMarkers([]);
        Assert.Empty(without.Destinations(Awaited));
    }

    [Fact]
    public void A_place_the_player_reached_leads_no_more()
    {
        var places = Create();
        places.OnMarkers([Boss, Player(-500, -464)]);
        Assert.Single(places.Destinations(Awaited));

        places.OnMarkers([Boss, Player(-555, -464)]);
        places.OnMarkers([Boss, Player(-500, -464)]);

        Assert.Empty(places.Destinations(Awaited));
    }

    [Fact]
    public void A_trigger_leads_until_the_walk_crossed_it_even_between_two_reads()
    {
        // Live 2026-11-01: S15_TriadA_Boss_Intro's boxes, which the straight route to the character missed.
        var places = Create();
        places.OnMarkers([Box, Player(-1133.3f, -1014f)]);
        var place = Assert.Single(places.Destinations(Awaited));
        Assert.Equal(AwaitedAreaDestination.TriggerUnits, place.Radius);

        places.OnMarkers([Box, Player(-1133.3f, -1016.5f)]);
        Assert.Single(places.Destinations(Awaited));

        places.OnMarkers([Box, Player(-1133.3f, -1024f)]);
        Assert.Empty(places.Destinations(Awaited));
    }

    [Fact]
    public void Triggers_side_by_side_lead_one_by_one()
    {
        var places = Create();
        places.OnMarkers([Box, Box with { X = -1125.9f }, Box with { X = -1130f }]);

        Assert.Equal(3, places.Destinations(Awaited).Count);
    }

    // The agent's review of PR #3: each spot an escort walked through became a destination of its own.
    [Fact]
    public void A_character_walking_through_the_area_is_one_place_where_it_stands_now()
    {
        var places = Create();
        places.OnMarkers([Boss]);
        places.OnMarkers([Boss with { X = Boss.X + 10 }]);
        places.OnMarkers([Boss with { X = Boss.X + 20 }]);

        Assert.Equal((Boss.X + 20, Boss.Y), Assert.Single(places.Destinations(Awaited)).Position);
    }

    // Same review: a character reached by the door left nothing to lead to, though the map marked the boss.
    [Fact]
    public void Once_the_characters_are_reached_the_monster_leads()
    {
        var places = Create();
        var monster = Boss with { ActorSno = 7, X = 0, Y = 0, Kind = MapMarkerReader.KindBoss };
        places.OnMarkers([monster, Boss, Player(-555, -464)]);
        places.OnMarkers([monster, Boss, Player(-500, -464)]);

        Assert.Equal((0f, 0f), Assert.Single(places.Destinations(Awaited)).Position);
    }

    [Fact]
    public void A_new_world_forgets_the_markers()
    {
        var places = Create();
        places.OnMarkers([Boss]);

        places.Reset();

        Assert.Empty(places.Destinations(Awaited));
    }
}
