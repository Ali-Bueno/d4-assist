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
/// the list, Escape closes it. Only waypoints the player's saved data lists as activated are ever offered; when that list is
/// unreadable the list says so instead of guessing.
/// </summary>
/// <param name="waypoints">Every waypoint of the game (WaypointCatalog.All).</param>
/// <param name="nameOf">A waypoint's name in the current language.</param>
/// <param name="readActivated">The activated waypoints' LevelArea SNOs (the session's ReadActivatedWaypoints); null when unreadable.</param>
/// <param name="readMapView">The map's center and zoom; null when unreadable.</param>
/// <param name="currentWorld">The world the player is in (GameSession.CurrentWorldSno): asked on each Shift+F1, since the app can
/// start in a world entered before this bridge listened (live 2026-10-18: "0 in world -1").</param>
public sealed class WaypointListBridge(
    IReadOnlyList<Waypoint> waypoints, Func<Waypoint, string> nameOf, Func<IReadOnlySet<int>, IReadOnlyList<int>?> readActivated,
    Func<MapView?> readMapView, Func<int> currentWorld, ISpeechSink speech, LanguageSetting language, DirectionStyle directionStyle, IMapListKeys keys,
    IGameMouse mouse, Func<bool> isGameForeground, Action<string>? log = null)
{
    private readonly object _gate = new();
    private readonly WaypointList _list = new();
    private readonly IReadOnlySet<int> _levelAreas = waypoints.Select(w => w.LevelAreaSno).ToHashSet();
    private PlayerState? _player;
    private bool _mapOpen, _openKey;

    /// <summary>Hooks the keys; call once.</summary>
    public void Start()
    {
        keys.OpenPressed += Open;
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

    /// <summary>Gives Shift+F1 to this app while the map shows and the game has the focus; the map closing closes the list.</summary>
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
            if (!_mapOpen) CloseLocked(spoken: false);
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
                speech.Speak(Text.Unreadable, interrupt: true);
                return;
            }
            int world = currentWorld();
            var here = waypoints.Where(w => w.WorldSno == world && activated.Contains(w.LevelAreaSno)).DistinctBy(w => w.LevelAreaSno);
            var (fromX, fromY) = _player is { } p ? (p.X, p.Y) : readMapView() is { } view ? (view.CenterX, view.CenterY) : (0f, 0f);
            _list.Open(WaypointList.Build(here.Select(w => (nameOf(w), (double)w.Position.X, (double)w.Position.Y)), fromX, fromY));
            log?.Invoke($"WAYPOINTS {activated.Count} activated, {_list.Count} in world {world}");
            keys.SetListKeys(_list.IsOpen);
            speech.Speak(_list.IsOpen ? $"{string.Format(Text.OpenedFormat, _list.Count)} {EntryText()}" : Text.Empty, interrupt: true);
        }
    }

    // At either end the list stays and says its entry again.
    private void Move(Func<bool> step)
    {
        lock (_gate)
        {
            if (!_list.IsOpen) return;
            step();
            speech.Speak(EntryText(), interrupt: true);
        }
    }

    private void Choose()
    {
        lock (_gate)
        {
            if (_list.Current is not { } entry) return;
            if (readMapView() is not { } view || mouse.ClientSize() is not { } size)
            {
                speech.Speak(Text.MapUnreadable, interrupt: true);
                return;
            }
            if (MapProjection.ToClient(view, entry.X, entry.Y, size.Width, size.Height) is not { } pixel)
            {
                log?.Invoke($"WAYPOINT {entry.Name} off the map view (center {view.CenterX:F0},{view.CenterY:F0} zoom {view.Zoom:F0})");
                if (MapProjection.ToClient(view with { Zoom = Math.Max(view.Zoom, MapProjection.MaxZoom) }, entry.X, entry.Y, size.Width, size.Height) is not null)
                {
                    speech.Speak(string.Format(Text.ZoomOutFormat, entry.Name), interrupt: true);
                    return;
                }
                if (Hop(entry, view, size) is { } hop && mouse.MoveTo(hop.Pixel.X, hop.Pixel.Y))
                {
                    log?.Invoke($"WAYPOINT hop {hop.Entry.Name} -> ({hop.Pixel.X},{hop.Pixel.Y}) on the way to {entry.Name}");
                    CloseLocked(spoken: false);
                    speech.Speak(string.Format(Text.HopFormat, entry.Name, hop.Entry.Name), interrupt: true);
                    return;
                }
                var direction = ScreenDirections.Bin(entry.X - view.CenterX, entry.Y - view.CenterY);
                speech.Speak(string.Format(Text.OffMapFormat, entry.Name, NavigationPhrases.For(language.Current).Direction(direction, directionStyle)),
                    interrupt: true);
                return;
            }
            bool moved = mouse.MoveTo(pixel.X, pixel.Y);
            log?.Invoke($"WAYPOINT jump {entry.Name} -> ({pixel.X},{pixel.Y}) in {size.Width}x{size.Height}, center {view.CenterX:F0},{view.CenterY:F0} zoom {view.Zoom:F0}{(moved ? "" : ": not moved")}");
            if (!moved)
            {
                speech.Speak(Text.MapUnreadable, interrupt: true);
                return;
            }
            CloseLocked(spoken: false);
            speech.Speak(string.Format(Text.CursorFormat, entry.Name), interrupt: true);
        }
    }

    // Even zoomed out all the way the map shows only ~1400 units above and below its center (live 2026-10-19: Farobru from
    // Temis and from Ichorfall). A waypoint farther off is reached in two jumps: to the activated waypoint in view nearest to
    // it, preferring one from which it is in view, and on from there.
    private (WaypointEntry Entry, (int X, int Y) Pixel)? Hop(WaypointEntry target, MapView view, (int Width, int Height) size)
    {
        double Remaining(double x, double y) => Math.Sqrt(Math.Pow(target.X - x, 2) + Math.Pow(target.Y - y, 2));
        double fromHere = Remaining(view.CenterX, view.CenterY);
        return _list.Entries
            .Where(e => e != target && Remaining(e.X, e.Y) < fromHere)
            .Select(e => (Entry: e, Pixel: MapProjection.ToClient(view, e.X, e.Y, size.Width, size.Height)))
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
        if (spoken) speech.Speak(Text.Closed, interrupt: true);
    }

    private string EntryText()
    {
        var entry = _list.Current!;
        string direction = NavigationPhrases.For(language.Current).Direction(entry.Direction, directionStyle);
        return string.Format(Text.EntryFormat, entry.Name, Math.Round(entry.Distance), direction, _list.Index + 1, _list.Count);
    }
}
