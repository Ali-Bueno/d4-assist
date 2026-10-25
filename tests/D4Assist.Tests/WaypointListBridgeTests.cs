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
        var (bridge, keys, speech, mouse) = Create([GeaKul], helltidePlace: Helltide);
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

    [Fact]
    public void Without_helltide_objects_on_the_map_Shift_F2_says_they_show_only_in_the_area()
    {
        var (bridge, keys, speech, _) = Create([GeaKul], helltidePlace: Helltide);
        bridge.OnMapMarkers([new MarkedActor(Cultist, 690, -410, 0, 1, 10)]);

        keys.OpenHelltide();

        Assert.False(keys.ListKeys);
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

        keys.Enter();

        Assert.Null(mouse.Moved);
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

    private static (WaypointListBridge, FakeKeys, FakeSpeech, FakeMouse) Create(
        int[]? activated, MapView? view = null, int world = OpenWorld, Func<bool>? foreground = null, Func<Waypoint, string?>? regionOf = null,
        Func<MarkedActor, HelltidePlace?>? helltidePlace = null)
    {
        var keys = new FakeKeys();
        var speech = new FakeSpeech();
        var mouse = new FakeMouse();
        var bridge = new WaypointListBridge(
            Catalog, w => w.LevelAreaName, _ => activated, () => view ?? FarOut, () => world, speech, new LanguageSetting(SpeechLanguage.English),
            DirectionStyle.Screen, keys, mouse, foreground ?? (() => true), regionOf: regionOf, helltidePlace: helltidePlace);
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
        public event Action? OpenHelltidePressed;
        public event Action? UpPressed;
        public event Action? DownPressed;
        public event Action? EnterPressed;
        public event Action? EscapePressed;

        // A key reaches the app only while it is registered, as with RegisterHotKey.
        public void Open() { if (OpenKey) OpenPressed?.Invoke(); }
        public void OpenHelltide() { if (OpenKey) OpenHelltidePressed?.Invoke(); }
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
        public (int Width, int Height)? ClientSize() => (1920, 1080);
        public bool MoveTo(int clientX, int clientY) { Moved = (clientX, clientY); return true; }
    }
}
