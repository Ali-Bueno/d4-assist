using System.Diagnostics;
using System.Numerics;
using D4Assist.Audio;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Game;

/// <summary>A place the game's map marks for a quest: a map marker whose actor and spot a quest's scripts name.</summary>
public sealed record QuestMapPlace(int QuestSno, string QuestName, int ActorSno, Vector2 Position);

/// <summary>
/// Leads with the guide ping along a computed path (OverworldPaths) to a quest place the game's map marks, in the open
/// world, for when the game's own navigation assist doesn't lead (live 2026-10-19: "Jünger des Verbotenen" until the quest
/// was untracked and tracked again). D-pad left there leads to the nearest marked quest place and speaks its quest and
/// distance; each further press moves on to the next nearest, and a press after the last switches the guide off. It leads
/// along the path (PathFollower), finds a new path when the player strays, and ends at the place, on a world change, or
/// when the player gets far away (a teleport).
/// </summary>
public sealed class QuestPathBridge
{
    /// <summary>Quest places farther than this aren't offered (OverworldPaths.MaxDistanceUnits).</summary>
    public const float MaxDistanceUnits = OverworldPaths.MaxDistanceUnits;

    /// <summary>Farther than this from the place the player has left (a teleport, a waypoint).</summary>
    public const float LeaveRangeUnits = MaxDistanceUnits + 300f;

    /// <summary>The ping aims this far along the path ahead of the player's spot on it (PathFollower).</summary>
    public const float LookaheadUnits = 8f;

    /// <summary>The place counts as reached this close (an 8 m trigger sphere: its radius).</summary>
    public const float ArriveUnits = 4f;

    /// <summary>Farther than this from the path, the player has strayed and a new path is found.</summary>
    public const float OffPathUnits = 12f;

    /// <summary>A new path is found at most this often while the player strays (a fight drags them around).</summary>
    public static readonly TimeSpan RepathInterval = TimeSpan.FromSeconds(3);

    // How often --debug logs where the ping aims.
    private static readonly TimeSpan AimLogInterval = TimeSpan.FromSeconds(5);

    // Marked places closer than this are one place (a quest marks a spot with several markers).
    private const float SamePlaceUnits = 3f;

    /// <summary>Places this close to the player aren't offered: they're here already. Standing at Hattar, his marker was always the
    /// nearest, reached at once, and the next press started over with it (live 2026-10-19).</summary>
    public const float HereUnits = 8f;

    private readonly string _worldName;
    private readonly Func<IReadOnlyList<MarkedActor>, IReadOnlyList<QuestMapPlace>> _questPlaces;
    private readonly Func<string, Vector3, Vector2, IReadOnlyList<Vector2>?> _findPath;
    private readonly GuidePingPlayer _ping;
    private readonly ISpeechSink _speech;
    private readonly LanguageSetting _language;
    private readonly Action<Action> _runInBackground;
    private readonly Action<string>? _log;
    private readonly Func<TimeSpan> _clock;
    private readonly Func<bool> _routeActive;
    // Feed events arrive on different poll threads, the path on a worker.
    private readonly object _gate = new();
    private string? _world;
    private IReadOnlyList<MarkedActor> _markers = [];
    private QuestMapPlace? _active;
    private Vector3? _player;
    private PathFollower? _follower;
    private bool _pathFound;
    private int _request;
    private TimeSpan _lastPath;
    private TimeSpan _lastAimLog;

    /// <summary>True in the open world, where D-pad left leads to quest places: the exploration guide then leaves the press to it.</summary>
    public bool TakesPad { get { lock (_gate) return _world == _worldName; } }

    /// <param name="worldName">The world it leads in (the open world, WorldClassifier.OpenWorldName).</param>
    /// <param name="questPlaces">The quest places among the map's markers, named in the current language.</param>
    /// <param name="findPath">Path points from the player to a place in a world (OverworldPaths.Find); null for none.</param>
    /// <param name="pad">D-pad left: the next quest place, then off; null for none.</param>
    /// <param name="runInBackground">Runs the path search off the poll thread; tests run it inline.</param>
    /// <param name="log">Debug sink for start, path and stop lines; null when not debugging.</param>
    /// <param name="clock">Time since start; null for a stopwatch.</param>
    /// <param name="routeActive">True while a quest route leads (QuestRouteBridge.IsActive): D-pad left then mutes that route only.</param>
    public QuestPathBridge(
        IDungeonFeed feed, string worldName, Func<IReadOnlyList<MarkedActor>, IReadOnlyList<QuestMapPlace>> questPlaces,
        Func<string, Vector3, Vector2, IReadOnlyList<Vector2>?> findPath, GuidePingPlayer ping, ISpeechSink speech,
        LanguageSetting? language = null, IGuideTogglePad? pad = null, Action<Action>? runInBackground = null,
        Action<string>? log = null, Func<TimeSpan>? clock = null, Func<bool>? routeActive = null)
    {
        _routeActive = routeActive ?? (() => false);
        _worldName = worldName;
        _questPlaces = questPlaces;
        _findPath = findPath;
        _ping = ping;
        _speech = speech;
        _language = language ?? new LanguageSetting();
        _runInBackground = runInBackground ?? (work => Task.Run(work));
        _log = log;
        var stopwatch = Stopwatch.StartNew();
        _clock = clock ?? (() => stopwatch.Elapsed);

        if (feed.CurrentWorld is { } startWorld) EnterWorld(startWorld.Name); // app started mid-world
        feed.WorldChanged += (_, name, _) => { lock (_gate) EnterWorld(name); };
        // A torn read gives positions like (-431602080, -431602080) (live 2026-10-19): it ended the guide as "left".
        feed.PlayerMoved += p => { if (ActorFilter.InWorld(new Vector3(p.X, p.Y, p.Z))) lock (_gate) OnPlayer(new Vector3(p.X, p.Y, p.Z)); };
        if (pad is not null) pad.DpadLeftPressed += OnTogglePressed;
    }

    /// <summary>Every actor the game's map marks (GameSession.MapMarkersRead).</summary>
    public void OnMapMarkers(IReadOnlyList<MarkedActor> markers)
    {
        lock (_gate) _markers = markers;
    }

    private void EnterWorld(string worldName)
    {
        Stop();
        _player = null;
        _markers = [];
        _world = worldName;
    }

    private void OnPlayer(Vector3 player)
    {
        _player = player;
        Lead(player);
    }

    private void OnTogglePressed()
    {
        lock (_gate)
        {
            if (_world != _worldName || (_active is null && _routeActive())) return;
            var strings = AppStrings.For(_language.Current);
            if (_player is not { } player) { _speech.Speak(strings.NoQuestPlace, interrupt: true); return; }
            var here = new Vector2(player.X, player.Y);
            var places = Places(here);
            Log($"PATH places: {string.Join("; ", places.Select(p => $"{p.QuestName} ({p.Position.X:F0},{p.Position.Y:F0}) {Vector2.Distance(here, p.Position):F0}"))}");
            int next = _active is { } current ? places.FindIndex(p => Vector2.Distance(p.Position, current.Position) <= SamePlaceUnits) + 1 : 0;
            if (_active is not null && (next == 0 || next >= places.Count))
            {
                Log($"PATH off ({places.Count} quest places marked)");
                Stop();
                _speech.Speak(strings.GuideOff, interrupt: true);
                return;
            }
            if (places.Count == 0)
            {
                Log($"PATH no quest place among {_markers.Count} map markers");
                _speech.Speak(strings.NoQuestPlace, interrupt: true);
                return;
            }
            Start(places[next], here, $"D-pad left, {next + 1} of {places.Count}");
        }
    }

    // The marked quest places in reach, nearest first, one per spot.
    private List<QuestMapPlace> Places(Vector2 here)
    {
        var places = new List<QuestMapPlace>();
        foreach (var place in _questPlaces(_markers).OrderBy(p => Vector2.Distance(here, p.Position)))
            if (Vector2.Distance(here, place.Position) is > HereUnits and <= MaxDistanceUnits
                && !places.Any(p => Vector2.Distance(p.Position, place.Position) <= SamePlaceUnits))
                places.Add(place);
        return places;
    }

    private void Start(QuestMapPlace place, Vector2 here, string reason)
    {
        Stop();
        _active = place;
        float distance = Vector2.Distance(here, place.Position);
        Log($"PATH to {place.QuestName} (quest {place.QuestSno}) at ({place.Position.X:F1},{place.Position.Y:F1}), {distance:F0} units ({reason})");
        _speech.Speak(string.Format(AppStrings.For(_language.Current).PathGuideFormat, place.QuestName, MathF.Round(distance)), interrupt: true);
        if (_player is { } player) FindPath(player);
    }

    private void FindPath(Vector3 player)
    {
        if (_active is not { } place || _world is not { } world) return;
        int request = ++_request;
        _lastPath = _clock();
        var from = new Vector2(player.X, player.Y);
        _runInBackground(() =>
        {
            var started = _clock();
            IReadOnlyList<Vector2>? points;
            try { points = _findPath(world, player, place.Position); }
            catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException)
            {
                Log($"PATH search failed: {e.Message}");
                points = null;
            }
            lock (_gate)
            {
                if (request != _request || _active != place) return; // a newer search, or the guide stopped or moved on
                var took = (_clock() - started).TotalMilliseconds;
                if (points is null)
                {
                    Log($"PATH no path from ({from.X:F1},{from.Y:F1}) ({took:F0} ms); leading straight");
                    if (!_pathFound) _speech.Speak(AppStrings.For(_language.Current).PathNotFound, interrupt: false);
                    points = [place.Position];
                }
                else
                {
                    Log($"PATH {points.Count} points from ({from.X:F1},{from.Y:F1}), {Length(from, points):F0} units ({took:F0} ms)");
                    _pathFound = true;
                }
                _follower = new PathFollower([from, .. points]);
                if (_player is { } now) Lead(now);
            }
        });
    }

    private void Lead(Vector3 player)
    {
        if (_active is not { } place) return;
        var here = new Vector2(player.X, player.Y);
        if (Vector2.Distance(here, place.Position) > LeaveRangeUnits)
        {
            Log($"PATH stopped: player left at ({here.X:F1},{here.Y:F1})");
            Stop();
            return;
        }
        if (Vector2.Distance(here, place.Position) <= ArriveUnits)
        {
            Log($"PATH reached {place.QuestName} at ({here.X:F1},{here.Y:F1})");
            _ping.PlayArrived();
            _speech.Speak(AppStrings.For(_language.Current).PathEnd, interrupt: false);
            Stop();
            return;
        }
        if (_follower is not { } follower) { _ping.Silence(); return; } // the path is being found

        var (aim, off, remaining) = follower.Follow(here, LookaheadUnits);
        if (off > OffPathUnits && _clock() - _lastPath >= RepathInterval)
        {
            Log($"PATH off the path at ({here.X:F1},{here.Y:F1}), {off:F0} units, finding a new one");
            FindPath(player); // the ping keeps following the old path meanwhile
        }
        if (_clock() - _lastAimLog >= AimLogInterval)
        {
            _lastAimLog = _clock();
            Log($"PATH at ({here.X:F1},{here.Y:F1}) aiming ({aim.X:F1},{aim.Y:F1}), {off:F1} off the path, {remaining:F0} to go");
        }
        var (right, up) = ScreenDirections.ToScreen(aim.X - here.X, aim.Y - here.Y);
        _ping.Aim(right, up, remaining + off);
    }

    private void Stop()
    {
        if (_active is not null) _ping.Silence();
        _active = null;
        _follower = null;
        _pathFound = false;
        _request++;
    }

    private static float Length(Vector2 from, IReadOnlyList<Vector2> points)
    {
        float length = 0;
        foreach (var p in points) (length, from) = (length + Vector2.Distance(from, p), p);
        return length;
    }

    private void Log(string message) => _log?.Invoke($"[{_clock().TotalSeconds,7:F1}s] {message}");
}
