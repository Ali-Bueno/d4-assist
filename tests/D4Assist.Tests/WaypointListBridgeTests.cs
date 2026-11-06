using System.Numerics;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Data.Readers;
using D4Memory.Ui;

namespace D4Assist.Tests;

public class WaypointListBridgeTests
{
    private const int OpenWorld = 69068, OtherWorld = 1402640;
    private const int GeaKul = 747179, Zarbinzet = 673509, Bazaar = 2009804, Temis = 1893662, Hideout = 1402640, Farobru = 185234;

    private static readonly IReadOnlyList<Waypoint> Catalog =
    [
        new(GeaKul, "Kehj_Gea_Kul", OpenWorld, new Vector3(680, -418, 0)),
        new(Zarbinzet, "Hawe_Zarbinzet", OpenWorld, new Vector3(-236, 386, 0)),
        new(Bazaar, "Naha_Kurast_Bazaar", OpenWorld, new Vector3(1038, 152, 0)),
        new(Temis, "Skov_Temis", OpenWorld, new Vector3(2581, -482, 0)),
        new(Hideout, "MERC_Hideout", OtherWorld, new Vector3(-36, -41, 0)),
        new(Farobru, "Step_Farobru", OpenWorld, new Vector3(-1185, -884, 0)),
    ];

    // All the way out over Temis (live 2026-10-19): Farobru, ~3800 units off, is beyond the map's view.
    private static readonly MapView OutAtTemis = new(2575f, -478f, MapProjection.MaxZoom);

    private static readonly MapView FarOut = new(681.64f, -410.26f, 1877.56f);

    [Fact]
    public void Shift_F1_works_only_while_the_map_shows()
    {
        var (bridge, keys, _, _) = Create([GeaKul]);

        bridge.OnMenus([MenuScreens.MainMenu]);
        Assert.False(keys.OpenKey);

        bridge.OnMenus([MenuScreens.MainMenu, MenuScreens.Map]);
        Assert.True(keys.OpenKey);

        bridge.OnMenus([]);
        Assert.False(keys.OpenKey);
    }

    [Fact]
    public void The_list_holds_only_activated_waypoints_of_this_world_nearest_first()
    {
        var (bridge, keys, speech, _) = Create([Zarbinzet, GeaKul, Hideout]); // Temis and the bazaar not activated

        keys.Open();

        Assert.True(keys.ListKeys);
        Assert.StartsWith("Waypoints by distance: 2. Kehj_Gea_Kul, 8 meters", speech.Last);
        Assert.True(speech.Brailled); // the list also goes to the braille display
        keys.Down();
        Assert.StartsWith("Hawe_Zarbinzet, ", speech.Last);
        Assert.EndsWith("2 of 2.", speech.Last);
    }

    [Fact]
    public void An_entry_says_its_region_after_its_name_but_never_its_name_twice()
    {
        var regions = new Dictionary<int, string> { [GeaKul] = "Kehjistan", [Zarbinzet] = "Hawe_Zarbinzet" };
        var (bridge, keys, speech, _) = Create([Zarbinzet, GeaKul, Bazaar], regionOf: w => regions.GetValueOrDefault(w.LevelAreaSno));

        keys.Open();
        Assert.StartsWith("Waypoints by distance: 3. Kehj_Gea_Kul, Kehjistan, 8 meters", speech.Last);
        keys.Down();
        Assert.StartsWith("Naha_Kurast_Bazaar, ", speech.Last); // no region known
        Assert.DoesNotContain("Nahantu", speech.Last);
        keys.Down();
        Assert.StartsWith("Hawe_Zarbinzet, ", speech.Last);
        Assert.DoesNotContain("Hawe_Zarbinzet, Hawe_Zarbinzet", speech.Last);
    }

    // Live 2026-10-25, the helltide in Hawezar: tortured gifts across the area, the accursed ritual; the player near the bazaar.
    private const int Gift = 1858869, Ritual = 1833920, Cultist = 1186208;

    private static HelltidePlace? Helltide(MarkedActor m) => m.ActorSno switch
    {
        Gift => new("Tortured Gift of Mysteries", "Lichtschwund", "Auen von Fethis, Hawezar"),
        Ritual => new("Accursed Ritual", "Das Schlingmoor", "Auen von Fethis, Hawezar"),
        _ => null,
    };

    [Fact]
    public void Shift_F2_lists_the_helltide_objects_the_map_marks_nearest_first()
    {
        var (bridge, keys, speech, mouse) = Create([GeaKul], view: new MapView(681.64f, -410.26f, 1100f), helltidePlace: Helltide);
        bridge.OnMapMarkers([
            new MarkedActor(Cultist, 690, -410, 0, 1, 10),
            new MarkedActor(Gift, 700, -410, 0, 1, 11),
            new MarkedActor(Ritual, 681.64f, -440, 0, 1, 11),
            new MarkedActor(Gift, 700.2f, -410.1f, 0, 1, 11), // the same gift twice
        ]);

        keys.OpenHelltide();

        Assert.StartsWith("Helltide in Auen von Fethis, Hawezar: 2 objects by distance. Tortured Gift of Mysteries, Lichtschwund, 18 meters", speech.Last);
        Assert.True(speech.Brailled);
        keys.Down();
        Assert.StartsWith("Accursed Ritual, Das Schlingmoor, 30 meters", speech.Last);
        keys.Enter();
        Assert.NotNull(mouse.Moved);
        Assert.False(keys.ListKeys);
    }

    // Live 2026-10-25: at zoom 1934 and 3046 the cursor on a gift's spot got no announcement; at 1100 it did. The wheel zooms
    // in a step at a time (user request 2026-10-27).
    [Fact]
    public void Zoomed_far_out_Enter_on_a_helltide_object_zooms_in_with_the_wheel_then_points_at_it()
    {
        var view = new MapView(681.64f, -410.26f, MapProjection.MaxZoom);
        var (bridge, keys, speech, mouse) = Create([GeaKul], readView: () => view, helltidePlace: Helltide);
        mouse.Scrolled = notches => view = view with { Zoom = view.Zoom == MapProjection.MaxZoom ? 1934f : 1100f };
        bridge.OnMapMarkers([new MarkedActor(Gift, 781, -410, 0, 1, 11)]);

        keys.OpenHelltide();
        keys.Enter();

        Assert.Equal([1, 1], mouse.Notches);
        Assert.Equal(1100f, view.Zoom);
        Assert.Equal(MapProjection.ToClient(view, 781, -410, 1920, 1080), mouse.Moved);
        Assert.StartsWith("Cursor on Tortured Gift of Mysteries", speech.Last);
        Assert.False(keys.ListKeys);
    }

    [Fact]
    public void A_wheel_that_zooms_out_is_turned_the_other_way()
    {
        var view = new MapView(681.64f, -410.26f, 1934f);
        var (bridge, keys, _, mouse) = Create([GeaKul], readView: () => view, helltidePlace: Helltide);
        mouse.Scrolled = notches => view = view with { Zoom = notches > 0 ? MapProjection.MaxZoom : 1100f };
        bridge.OnMapMarkers([new MarkedActor(Gift, 781, -410, 0, 1, 11)]);

        keys.OpenHelltide();
        keys.Enter();

        Assert.Equal([1, -1], mouse.Notches);
        Assert.Equal(1100f, view.Zoom);
        Assert.NotNull(mouse.Moved);
    }

    // Tester 2026-11-01 (720p): a gift 30 units off lay 13 px from the player's icon at zoom 1134, and the game's cursor went
    // back to the player. The wheel zooms on until they part (the steps below 1100 are 417.6 and 125.6).
    [Fact]
    public void A_target_under_the_players_icon_zooms_in_until_they_part()
    {
        var view = new MapView(681.64f, -410.26f, 1100f);
        var (bridge, keys, speech, mouse) = Create([GeaKul], readView: () => view, helltidePlace: Helltide);
        mouse.Scrolled = notches => view = view with { Zoom = view.Zoom == 1100f ? 417.6f : 125.6f };
        bridge.OnMapMarkers([new MarkedActor(Gift, 700, -410, 0, 1, 11)]); // 18 units off

        keys.OpenHelltide();
        keys.Enter();

        Assert.Equal([1, 1], mouse.Notches);
        Assert.Equal(MapProjection.ToClient(view, 700, -410, 1920, 1080), mouse.Moved);
        Assert.StartsWith("Cursor on Tortured Gift of Mysteries", speech.Last);
    }

    [Fact]
    public void A_target_no_zoom_can_part_from_the_player_is_pointed_at_as_it_is()
    {
        var view = new MapView(681.64f, -410.26f, 1100f);
        var (bridge, keys, _, mouse) = Create([GeaKul], readView: () => view, helltidePlace: Helltide);
        bridge.OnMapMarkers([new MarkedActor(Gift, 684, -410, 0, 1, 11)]); // 2 units off: the player stands at it

        keys.OpenHelltide();
        keys.Enter();

        Assert.Empty(mouse.Notches);
        Assert.Equal(MapProjection.ToClient(view, 684, -410, 1920, 1080), mouse.Moved);
    }

    // On a handheld the map still read 1967 on its way to 1934 300 ms after a notch (tester 2026-11-01).
    [Fact]
    public void Each_notch_waits_until_the_map_has_eased_to_its_new_zoom()
    {
        var view = new MapView(681.64f, -410.26f, MapProjection.MaxZoom);
        var easing = new Queue<float>();
        var (bridge, keys, _, mouse) = Create([GeaKul], readView: () =>
        {
            if (easing.Count > 0) view = view with { Zoom = easing.Dequeue() };
            return view;
        }, helltidePlace: Helltide);
        mouse.Scrolled = _ =>
        {
            float[] steps = view.Zoom > 2000f ? [MapProjection.MaxZoom, 2400f, 1967f, 1940f, 1934f] : [1700f, 1134f, 1104f, 1100f];
            foreach (float step in steps) easing.Enqueue(step);
        };
        bridge.OnMapMarkers([new MarkedActor(Gift, 781, -410, 0, 1, 11)]);

        keys.OpenHelltide();
        keys.Enter();

        Assert.Equal([1, 1], mouse.Notches);
        Assert.Equal(1100f, view.Zoom);
        Assert.Equal(MapProjection.ToClient(view, 781, -410, 1920, 1080), mouse.Moved);
    }

    [Fact]
    public void When_the_wheel_does_not_zoom_in_it_asks_to_zoom_in_and_keeps_the_list()
    {
        var (bridge, keys, speech, mouse) = Create([GeaKul], view: new MapView(681.64f, -410.26f, 1934f), helltidePlace: Helltide);
        bridge.OnMapMarkers([new MarkedActor(Gift, 700, -410, 0, 1, 11)]);

        keys.OpenHelltide();
        keys.Enter();

        Assert.NotEmpty(mouse.Notches);
        Assert.StartsWith("Zoom in further and choose Tortured Gift of Mysteries again", speech.Last);
        Assert.True(keys.ListKeys); // the list stays open for the next try
    }

    // 18:37 local: from patch 3.3.0 the base game's helltide ends on the hour, in 23 minutes.
    private static readonly DateTime Evening = new(2026, 10, 27, 18, 37, 10);

    [Fact]
    public void Shift_F2_names_the_active_helltide_and_Enter_lists_its_objects()
    {
        var (bridge, keys, speech, _) = Create([GeaKul], view: new MapView(681.64f, -410.26f, 1100f), helltidePlace: Helltide,
            helltides: [HelltideRegion.Hawezar]);
        bridge.OnMapMarkers([new MarkedActor(Gift, 700, -410, 0, 1, 11)]);

        keys.OpenHelltide();
        Assert.Equal("Helltide: Hawezar, 23 minutes left.", speech.Last);
        Assert.True(keys.ListKeys);

        keys.Enter();
        Assert.StartsWith("Helltide in Auen von Fethis, Hawezar: 1 objects by distance. Tortured Gift of Mysteries", speech.Last);
        keys.Enter();
        Assert.False(keys.ListKeys);
    }

    [Fact]
    public void Before_the_helltide_area_is_entered_Enter_says_to_go_there_and_keeps_the_list()
    {
        var (bridge, keys, speech, mouse) = Create([GeaKul], helltidePlace: Helltide, helltides: [HelltideRegion.Hawezar]);
        bridge.OnMapMarkers([new MarkedActor(Cultist, 690, -410, 0, 1, 10)]);

        keys.OpenHelltide();
        keys.Enter();

        Assert.Equal("The map shows the objects of the helltide in Hawezar only once you enter its area, outside towns.", speech.Last);
        Assert.True(keys.ListKeys);
        Assert.Null(mouse.Moved);
        keys.Escape(); // back to the events menu
        Assert.True(keys.ListKeys);
        keys.Escape();
        Assert.False(keys.ListKeys);
    }

    // Live 2026-10-27, Scosglen: the map marks the Blood Maiden's bounty from the inn, far outside the helltide's area.
    [Fact]
    public void The_Blood_Maiden_lists_before_the_area_is_entered_and_takes_the_cursor()
    {
        var (bridge, keys, speech, mouse) = Create([GeaKul], view: new MapView(681.64f, -410.26f, 1100f), helltidePlace: Helltide,
            helltides: [HelltideRegion.Hawezar], bounties: [("Kill the Blood Maiden", 690, -430)]);
        bridge.OnMapMarkers([new MarkedActor(Cultist, 690, -410, 0, 1, 10)]);

        keys.OpenHelltide();
        keys.Enter();
        Assert.StartsWith("Helltide in Hawezar: 1 objects by distance. Kill the Blood Maiden, ", speech.Last);

        keys.Enter();
        Assert.Equal(MapProjection.ToClient(new MapView(681.64f, -410.26f, 1100f), 690, -430, 1920, 1080), mouse.Moved);
    }

    [Fact]
    public void Between_helltides_Shift_F2_says_when_the_next_starts()
    {
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, helltides: []);

        keys.OpenHelltide();

        Assert.Equal("No helltide right now. The next one starts in 23 minutes.", speech.Last);
        Assert.True(keys.ListKeys); // the events menu stays for the whispers
    }

    // Tester, reported 2026-11-04: a war plan led into Nahantu's helltide; before build 3.3 the world state doesn't count, the map's objects do.
    private static HelltidePlace? NahantuGift(MarkedActor m) => new("Tortured Gift of Mysteries", "Zarbinzet", "Seven Stones, Nahantu");

    [Fact]
    public void A_helltide_only_the_map_shows_lists_first_with_no_minutes_and_takes_its_objects()
    {
        var (bridge, keys, speech, _) = Create([GeaKul], helltidePlace: NahantuGift, helltides: [HelltideRegion.Scosglen]);
        bridge.OnMapMarkers([new MarkedActor(Gift, 700, -410, 0, 1, 11)]);

        keys.OpenHelltide();
        Assert.EndsWith("Nahantu. 1 of 2.", speech.Last);
        keys.Enter();
        Assert.StartsWith("Helltide in Seven Stones, Nahantu: 1 objects", speech.Last);
    }

    [Fact]
    public void A_helltide_the_map_shows_lists_though_the_world_states_name_none()
    {
        var (bridge, keys, speech, _) = Create([GeaKul], helltidePlace: NahantuGift, helltides: []);
        bridge.OnMapMarkers([new MarkedActor(Gift, 700, -410, 0, 1, 11)]);

        keys.OpenHelltide();

        Assert.EndsWith("Nahantu.", speech.Last);
    }

    // Patch 3.3.0: an expansion's helltide rises on the half hour beside the base game's; each takes the objects in its region.
    [Fact]
    public void Two_helltides_list_one_after_the_other_and_each_takes_its_own_objects()
    {
        var (bridge, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, helltides: [HelltideRegion.Hawezar, HelltideRegion.Skovos]);
        bridge.OnMapMarkers([new MarkedActor(Gift, 700, -410, 0, 1, 11)]);

        keys.OpenHelltide();
        keys.Down();
        Assert.Equal("Skovos, 53 minutes left. 2 of 2.", speech.Last);
        keys.Down(); // stays at the end
        Assert.StartsWith("Skovos, ", speech.Last);
        keys.Enter();
        Assert.StartsWith("The map shows the objects of the helltide in Skovos only", speech.Last); // the gift is Hawezar's
        keys.Up();
        keys.Enter();
        Assert.StartsWith("Helltide in Auen von Fethis, Hawezar: 1 objects", speech.Last);
    }

    [Fact]
    public void Without_helltide_objects_on_the_map_Shift_F2_says_they_show_only_in_the_area()
    {
        var (bridge, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide);
        bridge.OnMapMarkers([new MarkedActor(Cultist, 690, -410, 0, 1, 10)]);

        keys.OpenHelltide();

        Assert.True(keys.ListKeys); // the events menu stays for the whispers
        Assert.StartsWith("No helltide objects on the map.", speech.Last);
    }

    // Live 2026-10-18: the app started in the open world, entered before the bridge listened; the list said "none in this world".
    [Fact]
    public void The_list_asks_for_the_world_the_player_is_in_now()
    {
        var (bridge, keys, speech, _) = Create([GeaKul, Hideout], world: OtherWorld);

        keys.Open();

        Assert.StartsWith("Waypoints by distance: 1. MERC_Hideout", speech.Last);
    }

    [Fact]
    public void An_unreadable_activated_list_offers_nothing()
    {
        var (bridge, keys, speech, _) = Create(activated: null);

        keys.Open();

        Assert.False(keys.ListKeys);
        Assert.Equal("Can't read the activated waypoints.", speech.Last);
    }

    [Fact]
    public void Enter_puts_the_cursor_on_the_waypoint_and_closes_the_list()
    {
        var (bridge, keys, speech, mouse) = Create([GeaKul, Zarbinzet]);
        keys.Open();
        keys.Down(); // Zarbinzet

        keys.Enter();

        Assert.NotNull(mouse.Moved);
        Assert.InRange(mouse.Moved.Value.X, 1582, 1598); // its icon at (1590, 494) far out
        Assert.InRange(mouse.Moved.Value.Y, 486, 502);
        Assert.False(keys.ListKeys);
        Assert.Equal("Cursor on Hawe_Zarbinzet.", speech.Last);
    }

    [Fact]
    public void A_waypoint_off_the_map_view_moves_nothing()
    {
        var (bridge, keys, speech, mouse) = Create([GeaKul, Temis], view: new MapView(681.64f, -410.26f, 417.6f));
        keys.Open();
        keys.Down(); // Temis, 1900 units away

        keys.Enter(); // the map doesn't zoom out here: the wheel turns, the view stays

        Assert.True(keys.ListKeys);
        Assert.StartsWith("Skov_Temis is outside the map view", speech.Last);
    }

    [Fact]
    public void A_waypoint_too_far_for_the_map_puts_the_cursor_on_a_stopover_in_view_on_the_way()
    {
        // Gea Kul is nearer Farobru but at the map's top edge (y 99): the stopover is the Bazaar, well inside.
        var (_, keys, speech, mouse) = Create([Temis, GeaKul, Bazaar, Farobru], view: OutAtTemis);
        keys.Open();
        keys.Down();
        keys.Down();
        keys.Down(); // Farobru, the farthest from the player

        keys.Enter();

        Assert.NotNull(mouse.Moved);
        Assert.Equal(MapProjection.ToClient(OutAtTemis, 1038, 152, 1920, 1080), mouse.Moved);
        Assert.False(keys.ListKeys);
        Assert.Equal("Step_Farobru is too far for the map. Stopover: cursor on Naha_Kurast_Bazaar. After the trip, choose Step_Farobru again.", speech.Last);
    }

    [Fact]
    public void With_no_stopover_it_says_which_way_to_move_the_map()
    {
        var (_, keys, speech, mouse) = Create([Temis, Farobru], view: OutAtTemis);
        keys.Open();
        keys.Down(); // Farobru

        keys.Enter();

        Assert.Null(mouse.Moved);
        Assert.True(keys.ListKeys);
        string direction = NavigationPhrases.For(SpeechLanguage.English).Direction(ScreenDirections.Bin(-1185 - 2575f, -884 + 478f), DirectionStyle.Screen);
        Assert.Equal($"Step_Farobru is outside the map view, {direction}. Move the map that way and choose it again.", speech.Last);
    }

    [Fact]
    public void Closing_the_map_closes_the_list()
    {
        var (bridge, keys, _, _) = Create([GeaKul]);
        keys.Open();

        bridge.OnMenus([]);

        Assert.False(keys.ListKeys);
    }

    [Fact]
    public void The_game_leaving_the_front_closes_the_list_and_frees_its_keys()
    {
        bool front = true;
        var (bridge, keys, _, _) = Create([GeaKul], foreground: () => front);
        keys.Open();
        Assert.True(keys.ListKeys);

        front = false;
        bridge.OnMenus([MenuScreens.MainMenu, MenuScreens.Map]);

        Assert.False(keys.ListKeys);
        Assert.False(keys.OpenKey);
    }

    // Treasure goblins the map shows (GoblinWatch decides which; here every goblin marker counts as shown).
    private const int Goblin = 384484, GoldGoblin = 2170341;

    private static HelltidePlace? GoblinPlace(MarkedActor marker) => marker.ActorSno switch
    {
        Goblin => new HelltidePlace("Treasure Goblin", "Lichtschwund", "Auen von Fethis, Hawezar"),
        GoldGoblin => new HelltidePlace("Gilded Goblin", null, "Auen von Fethis, Hawezar"),
        _ => null,
    };

    [Fact]
    public void Shift_F3_lists_the_treasure_goblins_the_map_shows_nearest_first_with_where_they_run()
    {
        var (bridge, keys, speech, _) = Create([GeaKul], view: new MapView(681.64f, -410.26f, 1100f), goblinPlace: GoblinPlace);
        bridge.OnMapMarkers([
            new MarkedActor(Cultist, 690, -410, 0, 1, 10),
            new MarkedActor(GoldGoblin, 681.64f, -450, 0, 1, 10),
            new MarkedActor(Goblin, 700, -410, 0, 1, 10),
        ]);

        keys.OpenGoblins();

        Assert.StartsWith("Treasure goblins on the map: 2. Treasure Goblin, Lichtschwund, 18 meters", speech.Last);
        Assert.True(speech.Brailled);
        keys.Down();
        Assert.StartsWith("Gilded Goblin, Auen von Fethis, Hawezar, 40 meters", speech.Last);
    }

    [Fact]
    public void Without_a_goblin_on_the_map_Shift_F3_says_so_and_opens_nothing()
    {
        var (bridge, keys, speech, _) = Create([GeaKul], goblinPlace: GoblinPlace);
        bridge.OnMapMarkers([new MarkedActor(Cultist, 690, -410, 0, 1, 10)]);

        keys.OpenGoblins();

        Assert.Equal("No treasure goblin on the map.", speech.Last);
        Assert.False(keys.ListKeys);
    }

    [Fact]
    public void Enter_points_at_where_the_goblin_runs_now()
    {
        var view = new MapView(681.64f, -410.26f, 1100f);
        var (bridge, keys, speech, mouse) = Create([GeaKul], view: view, goblinPlace: GoblinPlace);
        bridge.OnMapMarkers([new MarkedActor(Goblin, 700, -410, 0, 1, 10)]);
        keys.OpenGoblins();

        bridge.OnMapMarkers([new MarkedActor(Goblin, 710, -400, 0, 1, 10)]); // it ran on

        keys.Enter();
        Assert.Equal(MapProjection.ToClient(view, 710, -400, 1920, 1080), mouse.Moved);
        Assert.StartsWith("Cursor on Treasure Goblin", speech.Last);
    }

    [Fact]
    public void Shift_F3_works_only_while_the_map_shows()
    {
        var (bridge, keys, speech, _) = Create([GeaKul], goblinPlace: GoblinPlace);
        bridge.OnMapMarkers([new MarkedActor(Goblin, 700, -410, 0, 1, 10)]);
        bridge.OnMenus([MenuScreens.MainMenu]);

        keys.OpenGoblins();

        Assert.Null(speech.Last);
    }

    private static readonly WhisperEvent NearWhisper = new("Harvest: Caldeum", "Kehjistan", 700, -410, OpenWorld, 3);
    private static readonly WhisperEvent FarWhisper = new("Lut Bahadur", null, 681.64, 100, OpenWorld, 1, MinutesLeft: () => 12);

    [Fact]
    public void Shift_F2_opens_the_events_menu_and_up_and_down_move_through_it()
    {
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, helltides: [HelltideRegion.Hawezar], readWhispers: () => [NearWhisper]);

        keys.OpenEvents();
        Assert.Equal("Events menu: Helltide, 1 of 3.", speech.Last);
        Assert.True(keys.ListKeys);
        keys.Up();
        Assert.Equal("Helltide, 1 of 3.", speech.Last);
        keys.Down();
        Assert.Equal("Whispers, 2 of 3.", speech.Last);
        keys.Down();
        Assert.Equal("Events, 3 of 3.", speech.Last);
        keys.Down();
        Assert.Equal("Events, 3 of 3.", speech.Last);
        keys.Up();
        keys.Up();
        keys.Enter();
        Assert.StartsWith("Helltide: Hawezar", speech.Last);
        keys.Escape();
        Assert.Equal("Events menu: Helltide, 1 of 3.", speech.Last);
        keys.Escape();
        Assert.False(keys.ListKeys);
    }

    [Fact]
    public void Legions_and_world_bosses_list_apart_from_the_whispers()
    {
        var boss = new WhisperEvent("World Boss", "Dry Steppes", 700, -400, OpenWorld, 5, IsEvent: true);
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, readWhispers: () => [NearWhisper, boss]);

        keys.OpenEvents();
        keys.Down();
        keys.Enter();
        Assert.StartsWith("1 whispers by distance. Harvest: Caldeum", speech.Last);
        keys.Escape();
        Assert.Equal("Events menu: Whispers, 2 of 3.", speech.Last);
        keys.Down();
        keys.Enter();
        Assert.StartsWith("1 events by distance. World Boss, Dry Steppes, 5 Grim Favors", speech.Last);
        keys.Escape();
        Assert.Equal("Events menu: Events, 3 of 3.", speech.Last);
    }

    [Fact]
    public void No_event_listed_says_they_show_shortly_before_they_start_and_keeps_the_menu()
    {
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, readWhispers: () => [NearWhisper]);

        keys.OpenEvents();
        keys.Down();
        keys.Down();
        keys.Enter();

        Assert.Equal("No events right now. They show shortly before they start.", speech.Last);
        Assert.True(keys.ListKeys);
    }

    [Fact]
    public void The_whispers_list_nearest_first_with_their_favors_and_the_minutes_left_when_known()
    {
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, readWhispers: () => [FarWhisper, NearWhisper]);

        keys.OpenEvents();
        keys.Down();
        keys.Enter();
        Assert.StartsWith("2 whispers by distance. Harvest: Caldeum, Kehjistan, 3 Grim Favors, 18 meters", speech.Last);
        Assert.EndsWith("1 of 2.", speech.Last);
        Assert.True(speech.Brailled);
        keys.Down();
        Assert.StartsWith("Lut Bahadur, 1 Grim Favor, 12 minutes left, 510 meters", speech.Last);
        keys.Escape();
        Assert.Equal("Events menu: Whispers, 2 of 3.", speech.Last); // back on the entry it came from
    }

    [Fact]
    public void A_coming_event_says_when_it_starts_and_nothing_after_it_has()
    {
        int? minutes = 58;
        var boss = new WhisperEvent("World Boss", "Scosglen", 681.64, -410.26, OpenWorld, 5, IsEvent: true, MinutesToStart: () => minutes);
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, readWhispers: () => [boss]);

        keys.OpenEvents();
        keys.Down();
        keys.Down();
        keys.Enter();
        Assert.Contains("World Boss, Scosglen, 5 Grim Favors, starts in 58 minutes", speech.Last);
        keys.Escape();

        minutes = 1;
        keys.Enter();
        Assert.Contains("5 Grim Favors, starts in 1 minute,", speech.Last);
        keys.Escape();

        minutes = null;
        keys.Enter();
        Assert.DoesNotContain("starts in", speech.Last);
    }

    [Fact]
    public void A_whispers_minutes_are_those_of_the_moment_it_is_said_not_of_the_lists_opening()
    {
        int minutes = 2;
        WhisperEvent ticking = FarWhisper with { MinutesLeft = () => minutes };
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, readWhispers: () => [NearWhisper, ticking]);

        keys.OpenEvents();
        keys.Down();
        keys.Enter();
        keys.Down();
        Assert.StartsWith("Lut Bahadur, 1 Grim Favor, 2 minutes left", speech.Last);
        minutes = 1;
        keys.Up();
        keys.Down();
        Assert.StartsWith("Lut Bahadur, 1 Grim Favor, 1 minute left", speech.Last);
    }

    [Fact]
    public void Enter_puts_the_cursor_on_a_whisper_in_view_without_touching_the_zoom()
    {
        var view = new MapView(681.64f, -410.26f, 200f); // close enough in that the whisper lies clear of the player's icon
        var (_, keys, speech, mouse) = Create([GeaKul], view: view, helltidePlace: Helltide, readWhispers: () => [NearWhisper]);

        keys.OpenEvents();
        keys.Down();
        keys.Enter();
        keys.Enter();

        Assert.Equal(MapProjection.ToClient(view, 700, -410, 1920, 1080, 60), mouse.Moved);
        Assert.Empty(mouse.Notches);
        Assert.Equal("Cursor on Harvest: Caldeum.", speech.Last);
        Assert.False(keys.ListKeys);
    }

    [Fact]
    public void Enter_zooms_out_until_a_whisper_comes_into_view()
    {
        var view = new MapView(681.64f, -410.26f, 500f);
        var (_, keys, _, mouse) = Create([GeaKul], readView: () => view, helltidePlace: Helltide, readWhispers: () => [FarWhisper]);
        mouse.Scrolled = notches => view = view with { Zoom = notches < 0 ? 3046f : 100f };

        keys.OpenEvents();
        keys.Down();
        keys.Enter();
        keys.Enter();

        Assert.Equal([-1], mouse.Notches);
        Assert.Equal(MapProjection.ToClient(view, 681.64, 100, 1920, 1080, 60), mouse.Moved);
        Assert.False(keys.ListKeys);
    }

    [Fact]
    public void A_whisper_out_of_view_even_zoomed_out_says_where_it_lies_and_keeps_the_list()
    {
        var far = new WhisperEvent("Far", null, 681.64, 5000, OpenWorld, 1);
        var (_, keys, speech, mouse) = Create([GeaKul], view: FarOut, helltidePlace: Helltide, readWhispers: () => [NearWhisper, far]);

        keys.OpenEvents();
        keys.Down();
        keys.Enter();
        keys.Down();
        keys.Enter();

        Assert.StartsWith("Far is outside the map view", speech.Last);
        Assert.DoesNotContain("Stopover", speech.Last); // no activated waypoint lies nearer to it
        Assert.True(keys.ListKeys);
    }

    [Fact]
    public void No_whisper_up_or_unreadable_says_so_and_keeps_the_menu()
    {
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, readWhispers: () => []);
        keys.OpenEvents();
        keys.Down();
        keys.Enter();
        Assert.Equal("No whispers are up.", speech.Last);
        Assert.True(keys.ListKeys);

        var (_, keys2, speech2, _) = Create([GeaKul], helltidePlace: Helltide, readWhispers: () => null);
        keys2.OpenEvents();
        keys2.Down();
        keys2.Enter();
        Assert.Equal("Can't read what is up.", speech2.Last);
        Assert.True(keys2.ListKeys);
    }

    // The agent's review of PR #3: "no events" was said while the map's first read still ran.
    [Fact]
    public void No_event_yet_while_the_map_is_still_read_says_so()
    {
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, readWhispers: () => [], eventsReading: () => true);
        keys.OpenEvents();
        keys.Down();
        keys.Down();
        keys.Enter();

        Assert.Equal("Still reading the map's events; try again in a few seconds.", speech.Last);
    }

    // Tester 2026-11-01: another program held Shift+F2.
    [Fact]
    public void A_key_another_program_holds_is_said_with_the_one_to_use()
    {
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide);

        keys.Taken("Shift+F2", "Ctrl+Shift+F2", true);
        Assert.Equal("Another program holds Shift+F2; use Ctrl+Shift+F2.", speech.Last);

        keys.Taken("Shift+F2", "Ctrl+Shift+F2", false);
        Assert.Equal("Other programs hold Shift+F2 and Ctrl+Shift+F2; that list can't be opened.", speech.Last);
    }

    // Same review: a read that throws in a key's handler ended the app.
    [Fact]
    public void A_key_whose_read_fails_says_so_instead_of_throwing()
    {
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide, readWhispers: () => throw new InvalidDataException("schema"));
        keys.OpenEvents();
        keys.Down();

        keys.Enter();

        Assert.Equal("Can't read what is up.", speech.Last);
    }

    [Fact]
    public void Without_a_source_of_whispers_the_menu_holds_the_helltide_alone()
    {
        var (_, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide);
        keys.OpenEvents();
        keys.Down();
        Assert.Equal("Helltide, 1 of 1.", speech.Last);
    }

    // The map menu (user request 2026-11-06): waypoints, dungeons, cellars, boss lairs only while offered.
    private static readonly MapPlace NearDungeon = new("Den of Near", "Kehjistan", OpenWorld, 700, -400);
    private static readonly MapPlace MidDungeon = new("Mid Halls", null, OpenWorld, 681.64, 250);
    private static readonly MapPlace FarobruDungeon = new("Farobru Pit", null, OpenWorld, -1180, -880);
    private static readonly MapPlace OtherWorldDungeon = new("Hideout Hole", null, OtherWorld, 0, 0);

    private static IReadOnlyList<MapPlaceList> Lists(bool lairs = false, params MapPlace[] dungeons) =>
    [
        new(MapListKind.Dungeons, () => dungeons),
        new(MapListKind.Cellars, () => []),
        new(MapListKind.Lairs, () => [NearDungeon], () => lairs),
    ];

    [Fact]
    public void Shift_F1_opens_the_map_menu_and_escape_goes_back_a_level()
    {
        var (_, keys, speech, _) = Create([GeaKul], placeLists: Lists(false, MidDungeon, NearDungeon, OtherWorldDungeon));

        keys.Open();
        Assert.Equal("Map menu. Waypoints, 1 of 3.", speech.Last);
        keys.Down();
        Assert.Equal("Dungeons, 2 of 3.", speech.Last);
        keys.Enter();
        Assert.StartsWith("Dungeons by distance: 2. Den of Near, Kehjistan, ", speech.Last);
        keys.Down();
        Assert.StartsWith("Mid Halls, 660 meters", speech.Last);

        keys.Escape();
        Assert.Equal("Map menu. Dungeons, 2 of 3.", speech.Last);
        keys.Escape();
        Assert.Equal("List closed.", speech.Last);
        Assert.False(keys.ListKeys);
    }

    [Fact]
    public void The_lairs_entry_shows_only_while_offered_and_an_empty_list_keeps_the_menu()
    {
        var (_, keys, speech, _) = Create([GeaKul], placeLists: Lists(true, NearDungeon));
        keys.Open();
        Assert.Equal("Map menu. Waypoints, 1 of 4.", speech.Last);
        keys.Down();
        keys.Down();
        Assert.Equal("Cellars, 3 of 4.", speech.Last);
        keys.Enter();
        Assert.Equal("Cellars: none in this world.", speech.Last);
        Assert.True(keys.ListKeys);
        keys.Down();
        Assert.Equal("Boss lairs, 4 of 4.", speech.Last);

        keys.Up();
        keys.Up();
        keys.Up();
        keys.Enter();
        Assert.StartsWith("Waypoints by distance: 1.", speech.Last);
        keys.Escape();
        Assert.Equal("Map menu. Waypoints, 1 of 4.", speech.Last);
    }

    [Fact]
    public void Enter_zooms_out_until_a_place_is_in_view()
    {
        var view = new MapView(681.64f, -410.26f, 417.6f);
        var (_, keys, speech, mouse) = Create([GeaKul], readView: () => view, placeLists: Lists(false, MidDungeon));
        mouse.Scrolled = notches => view = view with { Zoom = notches < 0 ? MapProjection.MaxZoom : 417.6f };
        keys.Open();
        keys.Down();
        keys.Enter();

        keys.Enter();

        Assert.Equal(MapProjection.MaxZoom, view.Zoom);
        Assert.Equal(MapProjection.ToClient(view, MidDungeon.X, MidDungeon.Y, 1920, 1080), mouse.Moved);
        Assert.Equal("Cursor on Mid Halls.", speech.Last);
    }

    [Fact]
    public void A_place_too_far_puts_the_cursor_on_the_activated_waypoint_nearest_to_it()
    {
        var (_, keys, speech, mouse) = Create([Temis, GeaKul, Bazaar], view: OutAtTemis, placeLists: Lists(false, FarobruDungeon));
        keys.Open();
        keys.Down();
        keys.Enter();

        keys.Enter();

        Assert.Equal(MapProjection.ToClient(OutAtTemis, 1038, 152, 1920, 1080), mouse.Moved);
        Assert.Equal("Farobru Pit is too far for the map. Stopover: cursor on Naha_Kurast_Bazaar. After the trip, choose Farobru Pit again.", speech.Last);
    }

    private static (WaypointListBridge, FakeKeys, FakeSpeech, FakeMouse) Create(
        int[]? activated, MapView? view = null, int world = OpenWorld, Func<bool>? foreground = null, Func<Waypoint, string?>? regionOf = null,
        Func<MarkedActor, HelltidePlace?>? helltidePlace = null, IReadOnlyList<HelltideRegion>? helltides = null,
        Func<MapView?>? readView = null, IReadOnlyList<(string Name, double X, double Y)>? bounties = null,
        Func<MarkedActor, HelltidePlace?>? goblinPlace = null, Func<IReadOnlyList<WhisperEvent>?>? readWhispers = null, Func<bool>? eventsReading = null,
        IReadOnlyList<MapPlaceList>? placeLists = null)
    {
        var keys = new FakeKeys();
        var speech = new FakeSpeech();
        var mouse = new FakeMouse();
        var bridge = new WaypointListBridge(
            Catalog, w => w.LevelAreaName, _ => activated, readView ?? (() => view ?? FarOut), () => world, speech,
            new LanguageSetting(SpeechLanguage.English), DirectionStyle.Screen, keys, mouse, foreground ?? (() => true), regionOf: regionOf,
            helltidePlace: helltidePlace, readHelltides: () => helltides, regionName: r => r.ToString(), now: () => Evening, wait: _ => { },
            bountiesOf: _ => bounties ?? [], goblinPlace: goblinPlace, readWhispers: readWhispers, eventsReading: eventsReading,
            placeLists: placeLists);
        bridge.Start();
        bridge.OnPlayerMoved(new PlayerState(681.64f, -410.26f, 0, 0));
        bridge.OnMenus([MenuScreens.MainMenu, MenuScreens.Map]);
        return (bridge, keys, speech, mouse);
    }

    private sealed class FakeKeys : IMapListKeys
    {
        public bool OpenKey, ListKeys;
        public void SetOpenKey(bool active) => OpenKey = active;
        public void SetListKeys(bool active) => ListKeys = active;
        public event Action? OpenPressed;
        public event Action? OpenEventsPressed;
        public event Action? OpenGoblinsPressed;
        public event Action? UpPressed;
        public event Action? DownPressed;
        public event Action? EnterPressed;
        public event Action? EscapePressed;
        public event Action<string, string, bool>? OpenKeyTaken;

        public void Taken(string key, string instead, bool taken) => OpenKeyTaken?.Invoke(key, instead, taken);

        // A key reaches the app only while it is registered, as with RegisterHotKey.
        public void Open() { if (OpenKey) OpenPressed?.Invoke(); }
        public void OpenEvents() { if (OpenKey) OpenEventsPressed?.Invoke(); }
        // The events menu opens on the helltide.
        public void OpenHelltide() { OpenEvents(); Enter(); }
        public void OpenGoblins() { if (OpenKey) OpenGoblinsPressed?.Invoke(); }
        public void Up() { if (ListKeys) UpPressed?.Invoke(); }
        public void Down() { if (ListKeys) DownPressed?.Invoke(); }
        public void Enter() { if (ListKeys) EnterPressed?.Invoke(); }
        public void Escape() { if (ListKeys) EscapePressed?.Invoke(); }
    }

    private sealed class FakeSpeech : ISpeechSink
    {
        public string? Last;
        public bool Brailled;
        public string BackendName => "test";
        public void Speak(string text, bool interrupt) => (Last, Brailled) = (text, false);
        public void Output(string text, bool interrupt) => (Last, Brailled) = (text, true);
        public void Dispose() { }
    }

    private sealed class FakeMouse : IGameMouse
    {
        public (int X, int Y)? Moved;
        public readonly List<int> Notches = [];
        public Action<int>? Scrolled;
        public (int Width, int Height)? ClientSize() => (1920, 1080);
        public bool MoveTo(int clientX, int clientY) { Moved = (clientX, clientY); return true; }
        public bool Scroll(int notches) { Notches.Add(notches); Scrolled?.Invoke(notches); return true; }
    }
}
