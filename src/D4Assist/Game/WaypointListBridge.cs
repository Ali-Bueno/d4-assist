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
/// Shift+F2 opens the same list over the helltide's objects the map marks (user request 2026-10-25): tortured gifts, the accursed
/// ritual, Hell's prize. The map marks them only while the player is in the helltide's area outside a town (live 2026-10-25,
/// Hawezar), so an empty list says so.
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
public sealed class WaypointListBridge(
    IReadOnlyList<Waypoint> waypoints, Func<Waypoint, string> nameOf, Func<IReadOnlySet<int>, IReadOnlyList<int>?> readActivated,
    Func<MapView?> readMapView, Func<int> currentWorld, ISpeechSink speech, LanguageSetting language, DirectionStyle directionStyle, IMapListKeys keys,
    IGameMouse mouse, Func<bool> isGameForeground, Action<string>? log = null, Func<Waypoint, string?>? regionOf = null,
    Func<MarkedActor, HelltidePlace?>? helltidePlace = null)
{
    private readonly object _gate = new();
    private readonly WaypointList _list = new();
    private readonly IReadOnlySet<int> _levelAreas = waypoints.Select(w => w.LevelAreaSno).ToHashSet();
    private PlayerState? _player;
    private IReadOnlyList<MarkedActor> _markers = [];
    private bool _mapOpen, _openKey, _helltide;

    // Zoomed farther out than this the map shows the helltide's objects as no icon the cursor can point at (live 2026-10-25:
    // gifts said at zoom 1100, not at 1934 or 3046; the steps between are 1100 and 1934).
    private const float HelltideMaxZoom = 1500f;

    /// <summary>Hooks the keys; call once.</summary>
    public void Start()
    {
        keys.OpenPressed += Open;
        if (helltidePlace is not null) keys.OpenHelltidePressed += OpenHelltide;
        keys.UpPressed += () => Move(_list.Previous);
        keys.DownPressed += () => Move(_list.Next);
        keys.EnterPressed += Choose;
        keys.EscapePressed += () => Close(spoken: true);
    }

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
            _helltide = false;
            log?.Invoke($"WAYPOINTS {activated.Count} activated, {_list.Count} in world {world}");
            keys.SetListKeys(_list.IsOpen);
            speech.Output(_list.IsOpen ? $"{string.Format(Text.OpenedFormat, _list.Count)} {EntryText()}" : Text.Empty, interrupt: true);
        }
    }

    private void OpenHelltide()
    {
        lock (_gate)
        {
            if (!_mapOpen || helltidePlace is null) return;
            var places = _markers.Select(m => (Marker: m, Place: helltidePlace(m)))
                .Where(p => p.Place is not null)
                .DistinctBy(p => (p.Marker.ActorSno, MathF.Round(p.Marker.X), MathF.Round(p.Marker.Y)))
                .ToList();
            var (fromX, fromY) = _player is { } p ? (p.X, p.Y) : readMapView() is { } view ? (view.CenterX, view.CenterY) : (0f, 0f);
            _list.Open(WaypointList.Build(places.Select(h => (h.Place!.Name, h.Place.Region, (double)h.Marker.X, (double)h.Marker.Y)), fromX, fromY));
            _helltide = true;
            // The area most of them lie in names the helltide; one with no known area still lists.
            string? area = places.Select(h => h.Place!.Area).OfType<string>().GroupBy(a => a).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
            log?.Invoke($"HELLTIDE {_list.Count} object(s) among {_markers.Count} map markers, area {area ?? "unknown"}");
            keys.SetListKeys(_list.IsOpen);
            var text = HelltideStrings.For(language.Current);
            speech.Output(_list.IsOpen ? $"{string.Format(text.OpenedFormat, area ?? "?", _list.Count)} {EntryText()}" : text.NoneOnMap, interrupt: true);
        }
    }

    // At either end the list stays and says its entry again.
    private void Move(Func<bool> step)
    {
        lock (_gate)
        {
            if (!_list.IsOpen) return;
            step();
            speech.Output(EntryText(), interrupt: true);
        }
    }

    private void Choose()
    {
        lock (_gate)
        {
            if (_list.Current is not { } entry) return;
            if (readMapView() is not { } view || mouse.ClientSize() is not { } size)
            {
                speech.Output(Text.MapUnreadable, interrupt: true);
                return;
            }
            if (_helltide && view.Zoom > HelltideMaxZoom)
            {
                log?.Invoke($"WAYPOINT {entry.Name}: zoom {view.Zoom:F0} too far out for the helltide's icons");
                speech.Output(string.Format(HelltideStrings.For(language.Current).ZoomInFormat, entry.Name), interrupt: true);
                return;
            }
            float farthestZoom = _helltide ? HelltideMaxZoom : MapProjection.MaxZoom;
            if (MapProjection.ToClient(view, entry.X, entry.Y, size.Width, size.Height) is not { } pixel)
            {
                log?.Invoke($"WAYPOINT {entry.Name} off the map view (center {view.CenterX:F0},{view.CenterY:F0} zoom {view.Zoom:F0})");
                if (MapProjection.ToClient(view with { Zoom = Math.Max(view.Zoom, farthestZoom) }, entry.X, entry.Y, size.Width, size.Height) is not null)
                {
                    speech.Output(string.Format(Text.ZoomOutFormat, entry.Name), interrupt: true);
                    return;
                }
                if (Hop(entry, view, size) is { } hop && mouse.MoveTo(hop.Pixel.X, hop.Pixel.Y))
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

    // Even zoomed out all the way the map shows only ~1400 units above and below its center (live 2026-10-19: Farobru from
    // Temis and from Ichorfall). A waypoint farther off is reached in two jumps: to the activated waypoint in view nearest to
    // it, preferring one from which it is in view, and on from there.
    // A stopover stays this far (pixels at 1080 px) inside the map's edges: at its very top the cursor sat on Wejinhani at
    // y 104 and the trip didn't happen (live 2026-10-19), likely under the tab bar.
    private const double StopoverInset = 120;

    private (WaypointEntry Entry, (int X, int Y) Pixel)? Hop(WaypointEntry target, MapView view, (int Width, int Height) size)
    {
        double Remaining(double x, double y) => Math.Sqrt(Math.Pow(target.X - x, 2) + Math.Pow(target.Y - y, 2));
        double fromHere = Remaining(view.CenterX, view.CenterY);
        return _list.Entries
            .Where(e => e != target && Remaining(e.X, e.Y) < fromHere)
            .Select(e => (Entry: e, Pixel: MapProjection.ToClient(view, e.X, e.Y, size.Width, size.Height, StopoverInset)))
            .Where(c => c.Pixel is not null)
            .OrderBy(c => MapProjection.ToClient(view with { CenterX = (float)c.Entry.X, CenterY = (float)c.Entry.Y }, target.X, target.Y,
                size.Width, size.Height) is null ? 1 : 0)
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
        if (!_list.IsOpen) return;
        _list.Close();
        keys.SetListKeys(false);
        if (spoken) speech.Output(Text.Closed, interrupt: true);
    }

    private string EntryText()
    {
        var entry = _list.Current!;
        string direction = NavigationPhrases.For(language.Current).Direction(entry.Direction, directionStyle);
        // A hub that is its own region (Die Torhalle) says its name once.
        string label = entry.Region is { } region && region != entry.Name ? $"{entry.Name}, {region}" : entry.Name;
        return string.Format(Text.EntryFormat, label, Math.Round(entry.Distance), direction, _list.Index + 1, _list.Count);
    }
}
