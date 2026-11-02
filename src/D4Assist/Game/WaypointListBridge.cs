using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;
using D4Data.Readers;
using D4Memory.Ui;

namespace D4Assist.Game;

/// <summary>
/// The waypoint list on the world map: Shift+F1 (only while the map shows and the game has the focus) lists the activated
/// waypoints of this world, nearest first; up and down read them, Enter puts the mouse cursor on the chosen one and closes
/// the list, Escape closes it. Everything it says also goes to the braille display (ISpeechSink.Output, user request
/// 2026-10-25). Only waypoints the player's saved data lists as activated are ever offered; when that list is unreadable the
/// list says so instead of guessing.
/// Shift+F2 opens the events menu (user request 2026-11-01): the helltide, the whispers and the events (legions and world bosses). Escape goes back
/// a level; in the menu it closes. The whispers and the events list nearest first with the grim favors each pays and, while readable, the minutes it has left; Enter zooms the map out when
/// that brings the whisper into view and puts the cursor on it (research/map-points.md §4b).
/// The helltide entry lists the helltides the server has active (the session's world states, ActiveHelltides; user request 2026-10-27),
/// each with the minutes it has left (user request 2026-10-27); Enter opens the same list as Shift+F1 over that helltide's objects the map marks
/// (user request 2026-10-25): tortured gifts, the accursed ritual, altars, spires, the doomsayer. The map marks them only while
/// the player is in the helltide's area outside a town (live 2026-10-25, Hawezar), so without them the list asks to go there.
/// The helltide's bounties the map marks from anywhere list at once: the Blood Maiden ("Tötet die Blutjungfrau", user request
/// 2026-10-27) and the others up in the rotation (cultists, rituals, tortured gifts…, user request 2026-10-29).
/// Enter on an object zooms the map in with the wheel until the cursor can point at it (user request 2026-10-27). Without
/// readable world states the helltide entry opens the objects at once.
/// </summary>
/// <param name="waypoints">Every waypoint of the game (WaypointCatalog.All).</param>
/// <param name="nameOf">A waypoint's name in the current language.</param>
/// <param name="regionOf">The region a waypoint lies in, in the current language (WaypointCatalog.Zone), spoken after its name
/// (user request 2026-10-25: "Nevesk, Zersplitterte Gipfel"); null for none.</param>
/// <param name="readActivated">The activated waypoints' LevelArea SNOs (the session's ReadActivatedWaypoints); null when unreadable.</param>
/// <param name="readMapView">The map's center and zoom; null when unreadable.</param>
/// <param name="currentWorld">The world the player is in (GameSession.CurrentWorldSno): asked on each Shift+F1, since the app can
/// start in a world entered before this bridge listened (live 2026-10-18: "0 in world -1").</param>
/// <param name="helltidePlace">A map marker as a helltide object of the list (name, level area, the helltide's area), null for any
/// other marker; null for no helltide list.</param>
/// <param name="readHelltides">The helltides the server has active (ActiveHelltides.From); null when the world states are unreadable.</param>
/// <param name="regionName">A helltide region's name in the current language ("Hawezar"), as the objects' areas end in it.</param>
/// <param name="now">The local time, for the minutes to a helltide's change.</param>
/// <param name="wait">Waits the given milliseconds (for the map's zoom to settle).</param>
/// <param name="helltideEndsEarly">A base-game helltide ends five minutes before the hour (game builds before 3.3.0).</param>
/// <param name="bountiesOf">The helltide's bounties up in a region, by name and spot ("Tötet die Blutjungfrau", HelltideBountyCatalog):
/// the map marks them from anywhere, so they list before the area is entered.</param>
/// <param name="goblinPlace">A map marker as a treasure goblin the map shows the player now (GoblinWatch: name, level area, area),
/// null for any other marker; null for no goblin list (Shift+F3, user request 2026-10-29).</param>
/// <param name="readWhispers">The whispers up now; null when the world states are unreadable. Null for no whispers' entry in the events menu.</param>
/// <param name="eventsReading">True while the map's first read since it opened still runs (MapBountyWatch.Reading): no event listed yet is no answer.</param>
public sealed class WaypointListBridge(
    IReadOnlyList<Waypoint> waypoints, Func<Waypoint, string> nameOf, Func<IReadOnlySet<int>, IReadOnlyList<int>?> readActivated,
    Func<MapView?> readMapView, Func<int> currentWorld, ISpeechSink speech, LanguageSetting language, DirectionStyle directionStyle, IMapListKeys keys,
    IGameMouse mouse, Func<bool> isGameForeground, Action<string>? log = null, Func<Waypoint, string?>? regionOf = null,
    Func<MarkedActor, HelltidePlace?>? helltidePlace = null, Func<IReadOnlyList<HelltideRegion>?>? readHelltides = null,
    Func<HelltideRegion, string>? regionName = null, Func<DateTime>? now = null, Action<int>? wait = null, bool helltideEndsEarly = false,
    Func<HelltideRegion, IReadOnlyList<(string Name, double X, double Y)>>? bountiesOf = null,
    Func<MarkedActor, HelltidePlace?>? goblinPlace = null, Func<IReadOnlyList<WhisperEvent>?>? readWhispers = null, Func<bool>? eventsReading = null)
{
    private readonly object _gate = new();
    private readonly WaypointList _list = new();
    private readonly IReadOnlySet<int> _levelAreas = waypoints.Select(w => w.LevelAreaSno).ToHashSet();
    private PlayerState? _player;
    private IReadOnlyList<MarkedActor> _markers = [];
    private bool _mapOpen, _openKey;
    private Mode _mode;
    private IReadOnlyList<HelltideRegion> _regions = [];
    private int _regionIndex, _menuIndex;
    private IReadOnlyDictionary<(string Name, double X, double Y), WhisperEvent> _whispers = new Dictionary<(string, double, double), WhisperEvent>();

    // What the open list holds: waypoints (Shift+F1); the events menu and from it the active helltides, one helltide's objects or
    // the whispers (Shift+F2); the treasure goblins the map shows (Shift+F3).
    private enum Mode { Waypoints, EventsMenu, Regions, Objects, Whispers, Goblins }

    // Zoomed farther out than this the map shows the helltide's objects as no icon the cursor can point at (live 2026-10-25:
    // gifts said at zoom 1100, not at 1934 or 3046; the steps between are 1100 and 1934).
    private const float HelltideMaxZoom = 1500f;

    // How far out the game still names a whisper under the cursor is not measured yet: no limit until the live test says otherwise.
    private const float WhisperMaxZoom = MapProjection.MaxZoom;

    // The wheel zooms the map a step per notch (3046 → 1934 → 1100 …); the map takes a moment to settle on the new zoom.
    private const int MaxZoomSteps = 4, ZoomSettleMs = 300;

    /// <summary>Hooks the keys; call once.</summary>
    public void Start()
    {
        keys.OpenPressed += Guarded(Open);
        if (helltidePlace is not null) keys.OpenEventsPressed += Guarded(OpenEvents);
        if (goblinPlace is not null) keys.OpenGoblinsPressed += Guarded(OpenGoblins);
        keys.UpPressed += Guarded(() => Move(_list.Previous, -1));
        keys.DownPressed += Guarded(() => Move(_list.Next, 1));
        keys.EnterPressed += Guarded(Choose);
        keys.EscapePressed += Guarded(Back);
        keys.OpenKeyTaken += (key, instead, taken) =>
            speech.Output(string.Format(taken ? EventText.KeyTakenFormat : EventText.KeysTakenFormat, key, instead), interrupt: false);
    }

    // A key reads the game and its data: one that fails after a game update says so and leaves the list as it was.
    private Action Guarded(Action key) => () =>
    {
        try { key(); }
        catch (Exception e)
        {
            log?.Invoke($"WAYPOINTS key failed: {e}");
            speech.Output(EventText.Unreadable, interrupt: true);
        }
    };

    /// <summary>Closes the list and forgets the player's spot: both belong to the world left.</summary>
    public void OnWorldChanged()
    {
        lock (_gate)
        {
            _player = null;
            CloseLocked(spoken: false);
        }
    }

    public void OnPlayerMoved(PlayerState player)
    {
        lock (_gate) _player = player;
    }

    /// <summary>Every actor the game's map marks (GameSession.MapMarkersRead): the helltide list's source.</summary>
    public void OnMapMarkers(IReadOnlyList<MarkedActor> markers)
    {
        lock (_gate) _markers = markers;
    }

    /// <summary>Gives Shift+F1 to this app while the map shows and the game has the focus; the map closing, or the game leaving the
    /// front, closes the list and so frees its keys.</summary>
    public void OnMenus(IReadOnlyList<string> open)
    {
        lock (_gate)
        {
            _mapOpen = open.Contains(MenuScreens.Map);
            bool openKey = _mapOpen && isGameForeground();
            if (openKey != _openKey)
            {
                _openKey = openKey;
                keys.SetOpenKey(openKey);
            }
            if (!openKey) CloseLocked(spoken: false);
        }
    }

    private WaypointStrings Text => WaypointStrings.For(language.Current);

    private void Open()
    {
        lock (_gate)
        {
            if (!_mapOpen) return;
            if (readActivated(_levelAreas) is not { } activated)
            {
                log?.Invoke("WAYPOINTS activated list unreadable");
                CloseLocked(spoken: false);
                speech.Output(Text.Unreadable, interrupt: true);
                return;
            }
            int world = currentWorld();
            var here = waypoints.Where(w => w.WorldSno == world && activated.Contains(w.LevelAreaSno)).DistinctBy(w => w.LevelAreaSno);
            var (fromX, fromY) = _player is { } p ? (p.X, p.Y) : readMapView() is { } view ? (view.CenterX, view.CenterY) : (0f, 0f);
            _list.Open(WaypointList.Build(here.Select(w => (nameOf(w), regionOf?.Invoke(w), (double)w.Position.X, (double)w.Position.Y)), fromX, fromY));
            _mode = Mode.Waypoints;
            log?.Invoke($"WAYPOINTS {activated.Count} activated, {_list.Count} in world {world}");
            keys.SetListKeys(_list.IsOpen);
            speech.Output(_list.IsOpen ? $"{string.Format(Text.OpenedFormat, _list.Count)} {EntryText()}" : Text.Empty, interrupt: true);
        }
    }

    private void OpenEvents()
    {
        lock (_gate)
        {
            if (!_mapOpen) return;
            CloseLocked(spoken: false);
            ShowMenu();
        }
    }

    private void ShowMenu()
    {
        _list.Close();
        (_regions, _mode) = ([], Mode.EventsMenu);
        _menuIndex = Math.Clamp(_menuIndex, 0, MenuCount - 1);
        keys.SetListKeys(true);
        speech.Output($"{EventText.MenuOpened} {MenuText()}", interrupt: true);
    }

    private EventStrings EventText => EventStrings.For(language.Current);

    // Without a source of whispers the menu holds the helltide alone.
    private int MenuCount => readWhispers is null ? 1 : 3;

    private string MenuText() =>
        $"{_menuIndex switch { 0 => EventText.Helltide, 1 => EventText.Whispers, _ => EventText.Events }}, {string.Format(HelltideStrings.For(language.Current).PositionFormat, _menuIndex + 1, MenuCount)}";

    // A menu entry with nothing to show says so and the menu stays.
    private void ChooseMenu()
    {
        if (_menuIndex == 0) OpenHelltide();
        else OpenWhispers(events: _menuIndex == 2);
    }

    private void OpenHelltide()
    {
        var text = HelltideStrings.For(language.Current);
        if (readHelltides?.Invoke() is not { } active)
        {
            log?.Invoke("HELLTIDE world states unreadable: the objects the map marks");
            if (!OpenObjectsLocked(null)) speech.Output(text.NoneOnMap, interrupt: true);
            return;
        }
        log?.Invoke($"HELLTIDE active: {(active.Count == 0 ? "none" : string.Join(", ", active))}");
        if (active.Count == 0)
        {
            speech.Output(string.Format(text.NoneActiveFormat, ActiveHelltides.MinutesToNextBase(Now())), interrupt: true);
            return;
        }
        (_regions, _regionIndex, _mode) = (active, 0, Mode.Regions);
        speech.Output($"{text.RegionsOpened} {RegionText()}", interrupt: true);
    }

    private void OpenWhispers(bool events)
    {
        var text = EventText;
        if (readWhispers!() is not { } whispers)
        {
            log?.Invoke("WHISPERS world states unreadable");
            speech.Output(text.Unreadable, interrupt: true);
            return;
        }
        log?.Invoke($"WHISPERS {whispers.Count} up");
        whispers = [.. whispers.Where(w => w.IsEvent == events)];
        if (whispers.Count == 0)
        {
            speech.Output(!events ? text.NoWhispers : eventsReading?.Invoke() == true ? text.EventsReading : text.NoEvents, interrupt: true);
            return;
        }
        // Their spots lie in the open world: from inside a dungeon the player's own spot says nothing, so the map's center counts.
        int world = currentWorld();
        var (fromX, fromY) = _player is { } p && whispers.Any(w => w.WorldSno == world) ? (p.X, p.Y)
            : readMapView() is { } view ? (view.CenterX, view.CenterY) : (0f, 0f);
        _whispers = whispers.DistinctBy(w => (w.Name, w.X, w.Y)).ToDictionary(w => (w.Name, w.X, w.Y));
        _list.Open(WaypointList.Build(whispers.Select(w => (w.Name, (string?)WhisperDetails(w, text), w.X, w.Y)), fromX, fromY));
        _mode = Mode.Whispers;
        speech.Output($"{string.Format(events ? text.EventsOpenedFormat : text.WhispersOpenedFormat, _list.Count)} {EntryText()}", interrupt: true);
    }

    // Said after a whisper's name, where a waypoint says its region.
    private static string WhisperDetails(WhisperEvent whisper, EventStrings text)
    {
        var parts = new List<string>();
        if (whisper.Region is { } region) parts.Add(region);
        parts.Add(whisper.Favors == 1 ? text.OneFavor : string.Format(text.FavorsFormat, whisper.Favors));
        if (whisper.MinutesToStart?.Invoke() is { } start) parts.Add(start == 1 ? text.StartsInOneMinute : string.Format(text.StartsInFormat, start));
        if (whisper.MinutesLeft?.Invoke() is { } minutes) parts.Add(minutes == 1 ? text.OneMinuteLeft : string.Format(text.MinutesLeftFormat, minutes));
        return string.Join(", ", parts);
    }

    // Escape: back a level; in the menu, or in a list no menu opened, it closes.
    private void Back()
    {
        lock (_gate)
        {
            switch (_mode)
            {
                case Mode.Objects when _regions.Count > 0:
                    _list.Close();
                    _mode = Mode.Regions;
                    speech.Output($"{HelltideStrings.For(language.Current).RegionsOpened} {RegionText()}", interrupt: true);
                    break;
                case Mode.Objects or Mode.Regions or Mode.Whispers:
                    ShowMenu();
                    break;
                default:
                    CloseLocked(spoken: true);
                    break;
            }
        }
    }

    // The treasure goblins the map shows, nearest first, each with the level area it runs in.
    private void OpenGoblins()
    {
        lock (_gate)
        {
            if (!_mapOpen || goblinPlace is null) return;
            CloseLocked(spoken: false);
            var text = GoblinStrings.For(language.Current);
            var goblins = Goblins();
            log?.Invoke($"GOBLINS {goblins.Count} shown among {_markers.Count} map markers");
            if (goblins.Count == 0)
            {
                speech.Output(text.NoneOnMap, interrupt: true);
                return;
            }
            var (fromX, fromY) = _player is { } p ? (p.X, p.Y) : readMapView() is { } view ? (view.CenterX, view.CenterY) : (0f, 0f);
            _list.Open(WaypointList.Build(goblins.Select(g => (g.Place.Name, g.Place.Region ?? g.Place.Area, (double)g.Marker.X, (double)g.Marker.Y)),
                fromX, fromY));
            _mode = Mode.Goblins;
            keys.SetListKeys(true);
            speech.Output($"{string.Format(text.OpenedFormat, _list.Count)} {EntryText()}", interrupt: true);
        }
    }

    private List<(MarkedActor Marker, HelltidePlace Place)> Goblins() =>
        [.. _markers.Select(m => (Marker: m, Place: goblinPlace!(m))).Where(g => g.Place is not null).Select(g => (g.Marker, g.Place!))];

    // A goblin runs: Enter takes its spot from the latest markers, the one of its name nearest to where the list had it.
    private WaypointEntry Fresh(WaypointEntry entry)
    {
        var now = Goblins().Where(g => g.Place.Name == entry.Name)
            .OrderBy(g => Math.Pow(g.Marker.X - entry.X, 2) + Math.Pow(g.Marker.Y - entry.Y, 2)).FirstOrDefault();
        return now.Place is null ? entry : entry with { X = now.Marker.X, Y = now.Marker.Y };
    }

    // The objects the map marks of the helltide in region (null: of any), nearest first; false when it marks none.
    private bool OpenObjectsLocked(HelltideRegion? region)
    {
        var places = _markers.Select(m => (Marker: m, Place: helltidePlace!(m)))
            .Where(p => p.Place is not null)
            .DistinctBy(p => (p.Marker.ActorSno, MathF.Round(p.Marker.X), MathF.Round(p.Marker.Y)))
            .ToList();
        // With two helltides up, an object belongs to the one whose region its area names ("Auen von Fethis, Hawezar").
        if (region is { } r && _regions.Count > 1 && regionName?.Invoke(r) is { } name)
            places = places.Where(p => p.Place!.Area?.EndsWith(name, StringComparison.Ordinal) == true).ToList();
        var bounties = region is { } up && bountiesOf is not null ? bountiesOf(up) : [];
        log?.Invoke($"HELLTIDE {places.Count} object(s) among {_markers.Count} map markers, {bounties.Count} bounty(ies)" +
            (region is { } shown ? $" for {shown}" : ""));
        if (places.Count == 0 && bounties.Count == 0) return false;
        var (fromX, fromY) = _player is { } p ? (p.X, p.Y) : readMapView() is { } view ? (view.CenterX, view.CenterY) : (0f, 0f);
        _list.Open(WaypointList.Build(
            places.Select(h => (h.Place!.Name, h.Place.Region, (double)h.Marker.X, (double)h.Marker.Y))
                .Concat(bounties.Select(b => (b.Name, (string?)null, b.X, b.Y))), fromX, fromY));
        _mode = Mode.Objects;
        // The area most of them lie in names the helltide, else its region; one with no known area still lists.
        string? area = places.Select(h => h.Place!.Area).OfType<string>().GroupBy(a => a).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key
            ?? (region is { } named ? RegionName(named) : null);
        keys.SetListKeys(true);
        speech.Output($"{string.Format(HelltideStrings.For(language.Current).OpenedFormat, area ?? "?", _list.Count)} {EntryText()}", interrupt: true);
        return true;
    }

    private DateTime Now() => now?.Invoke() ?? DateTime.Now;

    private string RegionText()
    {
        var region = _regions[_regionIndex];
        var text = HelltideStrings.For(language.Current);
        string entry = string.Format(text.RegionEntryFormat, RegionName(region), ActiveHelltides.MinutesLeft(region, Now(), helltideEndsEarly));
        return _regions.Count > 1 ? $"{entry} {string.Format(text.PositionFormat, _regionIndex + 1, _regions.Count)}" : entry;
    }

    private string RegionName(HelltideRegion region) => regionName?.Invoke(region) ?? region.ToString();

    // At either end the list stays and says its entry again.
    private void Move(Func<bool> step, int regionStep)
    {
        lock (_gate)
        {
            if (_mode == Mode.EventsMenu)
            {
                _menuIndex = Math.Clamp(_menuIndex + regionStep, 0, MenuCount - 1);
                speech.Output(MenuText(), interrupt: true);
                return;
            }
            if (_mode == Mode.Regions)
            {
                _regionIndex = Math.Clamp(_regionIndex + regionStep, 0, _regions.Count - 1);
                speech.Output(RegionText(), interrupt: true);
                return;
            }
            if (!_list.IsOpen) return;
            step();
            speech.Output(EntryText(), interrupt: true);
        }
    }

    private void Choose()
    {
        lock (_gate)
        {
            if (_mode == Mode.EventsMenu)
            {
                ChooseMenu();
                return;
            }
            if (_mode == Mode.Regions)
            {
                ChooseRegion();
                return;
            }
            if (_list.Current is not { } entry) return;
            if (_mode == Mode.Goblins) entry = Fresh(entry);
            if (readMapView() is not { } view || mouse.ClientSize() is not { } size)
            {
                speech.Output(Text.MapUnreadable, interrupt: true);
                return;
            }
            // Helltide objects and goblins are small icons the cursor can point at only zoomed in.
            float farthestZoom = _mode switch { Mode.Objects or Mode.Goblins => HelltideMaxZoom, Mode.Whispers => WhisperMaxZoom, _ => MapProjection.MaxZoom };
            // A whisper may lie anywhere: zooming out brings it into view when the map is only zoomed in too far; one out of view
            // even zoomed out all the way leaves the zoom alone and says its direction (below).
            if (_mode == Mode.Whispers && MapProjection.ToClient(view, entry.X, entry.Y, size.Width, size.Height, TargetInset) is null
                && MapProjection.ToClient(view with { Zoom = WhisperMaxZoom }, entry.X, entry.Y, size.Width, size.Height, TargetInset) is not null)
                view = ZoomOut(entry, view, size);
            if (view.Zoom > farthestZoom)
            {
                log?.Invoke($"WAYPOINT {entry.Name}: zoom {view.Zoom:F0} too far out for the helltide's icons");
                if (ZoomIn(entry, view, size) is not { } zoomed)
                {
                    speech.Output(string.Format(HelltideStrings.For(language.Current).ZoomInFormat, entry.Name), interrupt: true);
                    return;
                }
                view = zoomed;
            }
            if (MapProjection.ToClient(view, entry.X, entry.Y, size.Width, size.Height, TargetInset) is not { } pixel)
            {
                log?.Invoke($"WAYPOINT {entry.Name} off the map view (center {view.CenterX:F0},{view.CenterY:F0} zoom {view.Zoom:F0})");
                if (MapProjection.ToClient(view with { Zoom = Math.Max(view.Zoom, farthestZoom) }, entry.X, entry.Y, size.Width, size.Height,
                        TargetInset) is not null)
                {
                    speech.Output(string.Format(Text.ZoomOutFormat, entry.Name), interrupt: true);
                    return;
                }
                // A stopover is a waypoint to travel to; from another whisper the map wouldn't move.
                if (_mode != Mode.Whispers && Hop(entry, view, size) is { } hop && mouse.MoveTo(hop.Pixel.X, hop.Pixel.Y))
                {
                    log?.Invoke($"WAYPOINT hop {hop.Entry.Name} -> ({hop.Pixel.X},{hop.Pixel.Y}) on the way to {entry.Name}");
                    CloseLocked(spoken: false);
                    speech.Output(string.Format(Text.HopFormat, entry.Name, hop.Entry.Name), interrupt: true);
                    return;
                }
                var direction = ScreenDirections.Bin(entry.X - view.CenterX, entry.Y - view.CenterY);
                speech.Output(string.Format(Text.OffMapFormat, entry.Name, NavigationPhrases.For(language.Current).Direction(direction, directionStyle)),
                    interrupt: true);
                return;
            }
            bool moved = mouse.MoveTo(pixel.X, pixel.Y);
            log?.Invoke($"WAYPOINT jump {entry.Name} at ({entry.X:F0},{entry.Y:F0}) -> ({pixel.X},{pixel.Y}) in {size.Width}x{size.Height}, center {view.CenterX:F0},{view.CenterY:F0} zoom {view.Zoom:F0}{(moved ? "" : ": not moved")}");
            if (!moved)
            {
                speech.Output(Text.MapUnreadable, interrupt: true);
                return;
            }
            CloseLocked(spoken: false);
            speech.Output(string.Format(Text.CursorFormat, entry.Name), interrupt: true);
        }
    }

    // Enter on an active helltide: its objects, or, when the map marks none yet, where to go for them; the list stays for another.
    private void ChooseRegion()
    {
        var region = _regions[_regionIndex];
        if (OpenObjectsLocked(region)) return;
        speech.Output(string.Format(HelltideStrings.For(language.Current).NotEnteredFormat, RegionName(region)), interrupt: true);
    }

    // Zoomed out past HelltideMaxZoom the cursor can't point at a helltide object: the wheel zooms in a step at a time, with the
    // cursor kept on the object in case the map zooms about it. A wheel turn that zooms out reverses the direction once. Null when
    // the map didn't get close enough.
    private MapView? ZoomIn(WaypointEntry entry, MapView view, (int Width, int Height) size) =>
        Zoom(entry, view, size, inward: true, v => v.Zoom <= HelltideMaxZoom, out var zoomed) ? zoomed : null;

    // Out until the entry is in view or the map is out all the way (or stops answering); the view it ended on.
    private MapView ZoomOut(WaypointEntry entry, MapView view, (int Width, int Height) size)
    {
        Zoom(entry, view, size, inward: false,
            v => MapProjection.ToClient(v, entry.X, entry.Y, size.Width, size.Height, TargetInset) is not null || v.Zoom >= MapProjection.MaxZoom, out var zoomed);
        return zoomed;
    }

    private bool Zoom(WaypointEntry entry, MapView view, (int Width, int Height) size, bool inward, Func<MapView, bool> done, out MapView zoomed)
    {
        zoomed = view;
        if (done(view)) return true;
        int notch = inward ? 1 : -1;
        bool reversed = false;
        for (int step = 0; step < MaxZoomSteps; step++)
        {
            if (MapProjection.ToClient(zoomed, entry.X, entry.Y, size.Width, size.Height, TargetInset) is { } pixel) mouse.MoveTo(pixel.X, pixel.Y);
            if (!mouse.Scroll(notch)) return false;
            (wait ?? Thread.Sleep)(ZoomSettleMs);
            if (readMapView() is not { } next) return false;
            log?.Invoke($"WAYPOINT zoom {zoomed.Zoom:F0} -> {next.Zoom:F0} (wheel {notch:+0;-0})");
            bool wrongWay = inward ? next.Zoom > zoomed.Zoom : next.Zoom < zoomed.Zoom;
            if (wrongWay && !reversed) (notch, reversed) = (-notch, true);
            zoomed = next;
            if (done(zoomed)) return true;
        }
        return false;
    }

    // Even zoomed out all the way the map shows only ~1400 units above and below its center (live 2026-10-19: Farobru from
    // Temis and from Ichorfall). A waypoint farther off is reached in two jumps: to the activated waypoint in view nearest to
    // it, preferring one from which it is in view, and on from there.
    // A stopover stays this far (pixels at 1080 px) inside the map's edges: at its very top the cursor sat on Wejinhani at
    // y 104 and the trip didn't happen (live 2026-10-19), likely under the tab bar.
    private const double StopoverInset = 120;

    // The cursor's target stays this far inside the map's edges: from Ichorfall Wejinhani shows at y 102 zoomed out all the
    // way, so the trip failed again (user, 2026-10-25, from Skovos via the stopover Ichorfall). Closer to the edge it counts as
    // out of view and a stopover from which it is farther in takes over (Temis -> Kurast Bazaar, Wejinhani at y 139).
    private const double TargetInset = 60;

    private (WaypointEntry Entry, (int X, int Y) Pixel)? Hop(WaypointEntry target, MapView view, (int Width, int Height) size)
    {
        double Remaining(double x, double y) => Math.Sqrt(Math.Pow(target.X - x, 2) + Math.Pow(target.Y - y, 2));
        double fromHere = Remaining(view.CenterX, view.CenterY);
        return _list.Entries
            .Where(e => e != target && Remaining(e.X, e.Y) < fromHere)
            .Select(e => (Entry: e, Pixel: MapProjection.ToClient(view, e.X, e.Y, size.Width, size.Height, StopoverInset)))
            .Where(c => c.Pixel is not null)
            .OrderBy(c => MapProjection.ToClient(view with { CenterX = (float)c.Entry.X, CenterY = (float)c.Entry.Y }, target.X, target.Y,
                size.Width, size.Height, TargetInset) is null ? 1 : 0)
            .ThenBy(c => Remaining(c.Entry.X, c.Entry.Y))
            .Select(c => ((WaypointEntry, (int, int))?)(c.Entry, c.Pixel!.Value))
            .FirstOrDefault();
    }

    private void Close(bool spoken)
    {
        lock (_gate) CloseLocked(spoken);
    }

    private void CloseLocked(bool spoken)
    {
        if (_mode is not (Mode.Regions or Mode.EventsMenu) && !_list.IsOpen) return;
        _list.Close();
        _mode = Mode.Waypoints;
        keys.SetListKeys(false);
        if (spoken) speech.Output(Text.Closed, interrupt: true);
    }

    private string EntryText()
    {
        var entry = _list.Current!;
        string direction = NavigationPhrases.For(language.Current).Direction(entry.Direction, directionStyle);
        // A hub that is its own region (Die Torhalle) says its name once.
        // A whisper's details are said anew: the list can stay open for minutes while its time runs.
        string? details = _mode == Mode.Whispers && _whispers.TryGetValue((entry.Name, entry.X, entry.Y), out var whisper) ? WhisperDetails(whisper, EventText) : entry.Region;
        string label = details is { } region && region != entry.Name ? $"{entry.Name}, {region}" : entry.Name;
        return string.Format(Text.EntryFormat, label, Math.Round(entry.Distance), direction, _list.Index + 1, _list.Count);
    }
}
