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
        keys.Down();
        Assert.StartsWith("Hawe_Zarbinzet, ", speech.Last);
        Assert.EndsWith("2 of 2.", speech.Last);
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
        var (_, keys, speech, mouse) = Create([Temis, GeaKul, Farobru], view: OutAtTemis);
        keys.Open();
        keys.Down();
        keys.Down(); // Farobru, the farthest from the player

        keys.Enter();

        Assert.NotNull(mouse.Moved);
        Assert.Equal(MapProjection.ToClient(OutAtTemis, 680, -418, 1920, 1080), mouse.Moved);
        Assert.False(keys.ListKeys);
        Assert.Equal("Step_Farobru is too far for the map. Stopover: cursor on Kehj_Gea_Kul. After the trip, choose Step_Farobru again.", speech.Last);
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

    private static (WaypointListBridge, FakeKeys, FakeSpeech, FakeMouse) Create(int[]? activated, MapView? view = null, int world = OpenWorld)
    {
        var keys = new FakeKeys();
        var speech = new FakeSpeech();
        var mouse = new FakeMouse();
        var bridge = new WaypointListBridge(
            Catalog, w => w.LevelAreaName, _ => activated, () => view ?? FarOut, () => world, speech, new LanguageSetting(SpeechLanguage.English),
            DirectionStyle.Screen, keys, mouse, () => true);
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
        public event Action? UpPressed;
        public event Action? DownPressed;
        public event Action? EnterPressed;
        public event Action? EscapePressed;

        // A key reaches the app only while it is registered, as with RegisterHotKey.
        public void Open() { if (OpenKey) OpenPressed?.Invoke(); }
        public void Up() { if (ListKeys) UpPressed?.Invoke(); }
        public void Down() { if (ListKeys) DownPressed?.Invoke(); }
        public void Enter() { if (ListKeys) EnterPressed?.Invoke(); }
        public void Escape() { if (ListKeys) EscapePressed?.Invoke(); }
    }

    private sealed class FakeSpeech : ISpeechSink
    {
        public string? Last;
        public string BackendName => "test";
        public void Speak(string text, bool interrupt) => Last = text;
        public void Dispose() { }
    }

    private sealed class FakeMouse : IGameMouse
    {
        public (int X, int Y)? Moved;
        public (int Width, int Height)? ClientSize() => (1920, 1080);
        public bool MoveTo(int clientX, int clientY) { Moved = (clientX, clientY); return true; }
    }
}
