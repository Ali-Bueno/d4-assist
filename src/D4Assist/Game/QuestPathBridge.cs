using System.Diagnostics;
using System.Numerics;
using D4Assist.Audio;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Game;

/// <summary>
/// Leads with the guide ping along a computed path (OverworldPaths) to an open-world quest place of
/// <see cref="QuestDestinationCatalog"/>. It starts when the quest's current step names the place within
/// <see cref="StartRangeUnits"/>, or on D-pad left there (the step isn't always read in the open world: quests are found
/// by the scenes and actors around the player). It leads point by point, finds a new path when the player strays from it,
/// and ends at the place, on a world change, or when the player gets far away (a teleport). While it leads, D-pad left mutes
/// and unmutes the ping.
/// </summary>
public sealed class QuestPathBridge
{
    /// <summary>Within this distance of a destination the quest's step starts the guide, and D-pad left does.</summary>
    public const float StartRangeUnits = 600f;

    /// <summary>Farther than this from the destination the player has left (a teleport, a waypoint).</summary>
    public const float LeaveRangeUnits = 900f;

    /// <summary>The ping aims this far along the path ahead of the player's spot on it (PathFollower).</summary>
    public const float LookaheadUnits = 8f;

    /// <summary>The destination counts as reached this close (an 8 m trigger sphere: its radius).</summary>
    public const float ArriveUnits = 4f;

    /// <summary>Farther than this from the leg being walked, the player has strayed and a new path is found.</summary>
    public const float OffPathUnits = 12f;

    /// <summary>A new path is found at most this often while the player strays (a fight drags them around).</summary>
    public static readonly TimeSpan RepathInterval = TimeSpan.FromSeconds(3);

    // How often --debug logs where the ping aims.
    private static readonly TimeSpan AimLogInterval = TimeSpan.FromSeconds(5);

    /// <summary>A quest place within this of the destination's marker is it.</summary>
    private const float SamePlaceUnits = 3f;

    private readonly IReadOnlyList<LoadedQuestDestination> _destinations;
    private readonly Func<string, Vector3, Vector2, IReadOnlyList<Vector2>?> _findPath;
    private readonly GuidePingPlayer _ping;
    private readonly ISpeechSink _speech;
    private readonly LanguageSetting _language;
    private readonly Action<Action> _runInBackground;
    private readonly Action<string>? _log;
    private readonly Func<TimeSpan> _clock;
    // Feed events arrive on different poll threads, the path on a worker.
    private readonly object _gate = new();
    private readonly HashSet<string> _finished = [];
    private string? _world;
    private IReadOnlyList<LoadedQuestDestination> _worldDestinations = [];
    private LoadedQuestDestination? _active;
    private Vector3? _player;
    private IReadOnlyList<Vector2>? _points;
    private PathFollower? _follower;
    private TimeSpan _lastAimLog;
    private int _request;
    private TimeSpan _lastPath;
    private bool _muted;

    /// <summary>True while it leads, or while D-pad left would start it: the exploration guide then leaves the press to it.</summary>
    public bool TakesPad { get { lock (_gate) return _active is not null || Startable() is not null; } }

    /// <param name="findPath">Path points from the player to a place in a world (OverworldPaths.Find); null for none.</param>
    /// <param name="pad">D-pad left starts the guide near a destination and mutes it while it leads; null for neither.</param>
    /// <param name="runInBackground">Runs the path search off the poll thread; tests run it inline.</param>
    /// <param name="log">Debug sink for start, path and stop lines; null when not debugging.</param>
    /// <param name="clock">Time since start; null for a stopwatch.</param>
    public QuestPathBridge(
        IDungeonFeed feed, IReadOnlyList<LoadedQuestDestination> destinations, Func<string, Vector3, Vector2, IReadOnlyList<Vector2>?> findPath,
        GuidePingPlayer ping, ISpeechSink speech, LanguageSetting? language = null, IGuideTogglePad? pad = null,
        Action<Action>? runInBackground = null, Action<string>? log = null, Func<TimeSpan>? clock = null)
    {
        _destinations = destinations;
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

    /// <summary>The places the active quests' current steps name (GameSession.QuestPlacesRead).</summary>
    public void OnQuestPlaces(IReadOnlyList<QuestPlace> places)
    {
        lock (_gate)
        {
            if (_active is not null || _player is not { } player) return;
            foreach (var destination in _worldDestinations.Where(d => !_finished.Contains(d.Destination.Name) && InRange(d, player, StartRangeUnits)))
            {
                if (!places.Any(p => p.ActorSno == destination.TargetSno && Vector2.Distance(new(p.X, p.Y), destination.Target) <= SamePlaceUnits)) continue;
                Start(destination, "the quest's step names it");
                return;
            }
        }
    }

    private void EnterWorld(string worldName)
    {
        Stop();
        _finished.Clear();
        _player = null;
        _world = worldName;
        _worldDestinations = [.. _destinations.Where(d => d.Destination.WorldName == worldName)];
    }

    private LoadedQuestDestination? Startable() =>
        _player is { } player ? _worldDestinations.FirstOrDefault(d => InRange(d, player, StartRangeUnits)) : null;

    private static bool InRange(LoadedQuestDestination destination, Vector3 player, float range) =>
        Vector2.Distance(new(player.X, player.Y), destination.Target) <= range;

    private void OnPlayer(Vector3 player)
    {
        _player = player;
        Lead(player);
    }

    private void Start(LoadedQuestDestination destination, string reason)
    {
        _active = destination;
        Log($"PATH {destination.Destination.Name} started ({reason}), target ({destination.Target.X:F1},{destination.Target.Y:F1})");
        _speech.Speak(AppStrings.For(_language.Current).PathGuide, interrupt: false);
        if (_player is { } player) FindPath(player);
    }

    private void FindPath(Vector3 player)
    {
        if (_active is not { } destination || _world is not { } world) return;
        int request = ++_request;
        _lastPath = _clock();
        var from = new Vector2(player.X, player.Y);
        _runInBackground(() =>
        {
            var started = _clock();
            IReadOnlyList<Vector2>? points;
            try { points = _findPath(world, player, destination.Target); }
            catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException)
            {
                Log($"PATH {destination.Destination.Name}: search failed: {e.Message}");
                points = null;
            }
            lock (_gate)
            {
                if (request != _request || _active != destination) return; // a newer search, or the guide stopped
                var took = (_clock() - started).TotalMilliseconds;
                if (points is null)
                {
                    Log($"PATH {destination.Destination.Name}: no path from ({from.X:F1},{from.Y:F1}) ({took:F0} ms); leading straight");
                    if (_points is null) _speech.Speak(AppStrings.For(_language.Current).PathNotFound, interrupt: false);
                    points = [destination.Target];
                }
                else Log($"PATH {destination.Destination.Name}: {points.Count} points from ({from.X:F1},{from.Y:F1}), {Length(from, points):F0} units ({took:F0} ms)");
                _points = points;
                _follower = new PathFollower([from, .. points]);
                if (_player is { } now) Lead(now);
            }
        });
    }

    private void Lead(Vector3 player)
    {
        if (_active is not { } destination) return;
        var here = new Vector2(player.X, player.Y);
        if (!InRange(destination, player, LeaveRangeUnits))
        {
            Log($"PATH {destination.Destination.Name} stopped: player left at ({here.X:F1},{here.Y:F1})");
            Stop();
            return;
        }
        if (Vector2.Distance(here, destination.Target) <= ArriveUnits)
        {
            Log($"PATH {destination.Destination.Name} reached at ({here.X:F1},{here.Y:F1})");
            if (!_muted) _ping.PlayArrived();
            _speech.Speak(AppStrings.For(_language.Current).PathEnd, interrupt: false);
            _finished.Add(destination.Destination.Name);
            Stop();
            return;
        }
        if (_follower is not { } follower) { _ping.Silence(); return; } // the path is being found

        var (aim, off, remaining) = follower.Follow(here, LookaheadUnits);
        if (off > OffPathUnits && _clock() - _lastPath >= RepathInterval)
        {
            Log($"PATH {destination.Destination.Name}: off the path at ({here.X:F1},{here.Y:F1}), {off:F0} units, finding a new one");
            FindPath(player); // the ping keeps following the old path meanwhile
        }
        if (_clock() - _lastAimLog >= AimLogInterval)
        {
            _lastAimLog = _clock();
            Log($"PATH {destination.Destination.Name}: at ({here.X:F1},{here.Y:F1}) aiming ({aim.X:F1},{aim.Y:F1}), {off:F1} off the path, {remaining:F0} to go");
        }
        if (_muted) { _ping.Silence(); return; }
        var (right, up) = ScreenDirections.ToScreen(aim.X - here.X, aim.Y - here.Y);
        _ping.Aim(right, up, remaining + off);
    }

    private void Stop()
    {
        if (_active is not null) _ping.Silence();
        _active = null;
        _points = null;
        _follower = null;
        _request++;
        _muted = false; // the mute is for this walk: the next one starts audible, as its announcement says
    }

    private void OnTogglePressed()
    {
        lock (_gate)
        {
            if (_active is null)
            {
                if (Startable() is { } destination) Start(destination, "D-pad left");
                return;
            }
            _muted = !_muted;
            var strings = AppStrings.For(_language.Current);
            Log($"PATH {_active.Destination.Name} {(_muted ? "muted" : "unmuted")}");
            _speech.Speak(_muted ? strings.GuideOff : strings.GuideOn, interrupt: true);
            if (_muted) _ping.Silence();
            else if (_player is { } player) Lead(player);
        }
    }

    private static float Length(Vector2 from, IReadOnlyList<Vector2> points)
    {
        float length = 0;
        foreach (var p in points) (length, from) = (length + Vector2.Distance(from, p), p);
        return length;
    }

    private void Log(string message) => _log?.Invoke($"[{_clock().TotalSeconds,7:F1}s] {message}");
}
