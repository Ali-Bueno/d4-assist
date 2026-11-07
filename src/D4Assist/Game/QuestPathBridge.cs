using System.Diagnostics;
using System.Numerics;
using D4Data.Readers;
using D4Assist.Audio;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Game;

/// <summary>A place the game's map marks for a quest: a map marker whose actor and spot a quest's scripts name, or the game's own
/// quest marker.</summary>
/// <param name="GameMarker">The game's own quest marker (Symbol_Quest_Proxy, MarkerLocation): offered before quest objects.</param>
/// <param name="Hidden">Named only by a step the game doesn't show (QuestPhaseObjects.HiddenMarkers): offered after the shown ones.</param>
/// <param name="Entrance">A dungeon or cellar entrance near the player, not a quest's: offered after every quest place, named
/// after the dungeon.</param>
/// <param name="Legion">A legion event's target (LegionTargets), named after its kind: offered before everything else but a chosen place.</param>
/// <param name="Chosen">The place last chosen with Enter in a map menu list (Choose): offered first until reached.</param>
public sealed record QuestMapPlace(int QuestSno, string QuestName, int ActorSno, Vector2 Position, bool GameMarker = false, bool Hidden = false,
    bool Entrance = false, LegionTargetKind? Legion = null, bool Chosen = false);

/// <summary>Where a map marker stands in a quest's current step.</summary>
public enum StepPlace { NotInStep, Shown, Hidden }

public static class QuestStep
{
    /// <summary>Whether the step names the marked actor: a marker of it at its spot (ActiveQuests.MarkerReach), or its SNO alone (any
    /// copy). The map marks every phase's objects of a quest in progress: "In Rathmas Fußstapfen" at "find the untouched forests"
    /// still marked Tharra, its first step, and D-pad left led there as the nearer place (live 2026-10-21).</summary>
    public static StepPlace Of(QuestPhaseObjects step, int actorSno, Vector2 at)
    {
        var named = step.Markers.Where(m => m.Sno == actorSno && Vector2.Distance(new(m.Position.X, m.Position.Y), at) <= ActiveQuests.MarkerReach).ToList();
        if (named.Count > 0) return named.All(m => step.HiddenMarkers?.Contains(m) == true) ? StepPlace.Hidden : StepPlace.Shown;
        return step.ActorSnos.Contains(actorSno) ? StepPlace.Shown : StepPlace.NotInStep;
    }
}

/// <summary>
/// Leads with the guide ping along a computed path (OverworldPaths) to a quest place the game's map marks, in the open
/// world, for when the game's own navigation assist doesn't lead (live 2026-10-19: "Jünger des Verbotenen" until the quest
/// was untracked and tracked again), and in the story areas, which have no exploration guide (tester 2026-10-22, the Sightless
/// Eye). D-pad left there leads to the nearest marked quest place and speaks its quest and
/// distance; each further press moves on to the next nearest, and a press after the last switches the guide off. It leads
/// along the path (PathFollower), finds a new path when the player strays, and ends at the place, on a world change, or
/// when the player gets far away (a teleport). After the quest places come the dungeon entrances near the player: the game's
/// navigation assist ends beside a dungeon, below a ledge or behind a rock from its door (tester 2026-10-31, the Sunken Ruins).
/// Inside a legion event the event's targets come first (LegionTargets; user request 2026-11-04: "where do I go next"; D-pad left
/// led to two cellars in the Dry Steppes legion that morning). At a target the guide chimes once and holds; when the map stops
/// marking it (destroyed, killed, opened) it leads on to the nearest target left by itself, and says when none is left. In a
/// defense legion (user report 2026-11-05, Skovos' island defense) a spot under attack comes first, said with its side; a building
/// to defend gives way to it once the map marks one.
/// </summary>
public sealed class QuestPathBridge
{
    /// <summary>Quest places farther than this aren't offered (OverworldPaths.MaxDistanceUnits).</summary>
    public const float MaxDistanceUnits = OverworldPaths.MaxDistanceUnits;

    /// <summary>Farther than this from the place the player has left (a teleport, a waypoint).</summary>
    public const float LeaveRangeUnits = MaxDistanceUnits + 300f;

    /// <summary>A player this far off the path was moved, not walking (a teleport to a town: live 2026-11-07, 1021 units off,
    /// a search every 3 s over a 1000-unit box): the guide stops and says so rather than search again.</summary>
    public const float MovedAwayUnits = 150f;

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

    /// <summary>How long after entering a world a press waits for the map's first read: a handheld's list took 24 s (log of 2026-11-02).</summary>
    public static readonly TimeSpan MapReadWait = TimeSpan.FromSeconds(30);

    // Marked places closer than this are one place (a quest marks a spot with several markers).
    private const float SamePlaceUnits = 3f;

    /// <summary>A legion target's marker this far from where it was is still the same target (a captain walks while it fights).</summary>
    public const float TrackUnits = 25f;

    /// <summary>At a legion target the ping stays silent while the player is this close: they fight there.</summary>
    public const float HoldUnits = 10f;

    /// <summary>A legion target the map stops marking for this long is done: one read without it may be torn.</summary>
    public static readonly TimeSpan GoneAfter = TimeSpan.FromSeconds(1.5);

    /// <summary>Places this close to the player aren't offered: they're here already. Standing at Hattar, his marker was always the
    /// nearest, reached at once, and the next press started over with it (live 2026-10-19).</summary>
    public const float HereUnits = 8f;

    /// <summary>Dungeon entrances this near are offered: where the game's navigation assist leaves the player, the dungeon it led to
    /// is within a few tens of units, and a farther entrance is another dungeon's.</summary>
    public const float EntranceRangeUnits = 150f;

    private readonly Func<string, bool> _leadsIn;
    private readonly Func<IReadOnlyList<MarkedActor>, IReadOnlyList<QuestMapPlace>> _questPlaces;
    private readonly Func<string, Vector2, float, IReadOnlyList<QuestMapPlace>> _entrances;
    private readonly Func<string, Vector3, Vector2, IReadOnlyList<Vector2>?> _findPath;
    private readonly GuidePingPlayer _ping;
    private readonly ISpeechSink _speech;
    private readonly LanguageSetting _language;
    private readonly Action<Action> _runInBackground;
    private readonly Action<string>? _log;
    private readonly Func<TimeSpan> _clock;
    private readonly Func<bool> _routeActive;
    private readonly Func<int> _wallsVersion;
    private readonly Func<IReadOnlyList<MarkedActor>, Vector2, IReadOnlyList<LegionTarget>> _legionTargets;
    private readonly Func<IReadOnlyList<MarkedActor>, Vector2, string?> _legionMarkers;
    private readonly DirectionStyle _directionStyle;
    // Feed events arrive on different poll threads, the path on a worker.
    private readonly object _gate = new();
    private string? _world;
    private bool _leads;
    private IReadOnlyList<MarkedActor> _markers = [];
    private bool _markersRead;
    private TimeSpan _worldEntered;
    private QuestMapPlace? _active;
    private Vector3? _player;
    private PathFollower? _follower;
    private bool _pathFound;
    private int _request;
    private bool _searching; // a search runs: the next waits for it (searches over a far target overlapped and took 2.6 GB live)
    private TimeSpan _lastPath;
    private int _pathWalls;
    private TimeSpan _lastAimLog;
    // Which start the active place is: a legion target's place moves with its marker, so the place itself can't tell.
    private int _activeId;
    private Vector2 _pathTo;
    private bool _held;
    // The place chosen in a map menu list, and its world's name: offered first until reached (user request 2026-11-06).
    private (string World, QuestMapPlace Place)? _chosen;
    private TimeSpan? _missingSince;

    /// <summary>True where D-pad left leads to quest places: the exploration guide then leaves the press to it.</summary>
    public bool TakesPad { get { lock (_gate) return _leads; } }

    /// <param name="leadsIn">True for a world it leads in, by name (the open world and the story areas).</param>
    /// <param name="questPlaces">The quest places among the map's markers, named in the current language.</param>
    /// <param name="findPath">Path points from the player to a place in a world (OverworldPaths.Find); null for none.</param>
    /// <param name="pad">D-pad left: the next quest place, then off; null for none.</param>
    /// <param name="runInBackground">Runs the path search off the poll thread; tests run it inline.</param>
    /// <param name="log">Debug sink for start, path and stop lines; null when not debugging.</param>
    /// <param name="clock">Time since start; null for a stopwatch.</param>
    /// <param name="routeActive">True while a quest route leads (QuestRouteBridge.IsActive): D-pad left then mutes that route only.</param>
    /// <param name="entrances">The dungeon entrances of a world (by name) within a distance of a spot, named in the current language;
    /// null for none.</param>
    /// <param name="wallsVersion">Changes when a locked door starts or stops being a wall (LockedDoors.Version): the path is found
    /// again; null for never.</param>
    /// <param name="legionTargets">The legion targets among the map's markers near a spot, in the order offered (LegionTargets.Near);
    /// null for none.</param>
    /// <param name="legionMarkers">Every map marker in a legion's area near a spot, for the debug log (LegionTargets.Describe); null
    /// for none.</param>
    /// <param name="directionStyle">How a spot under attack's side is said (screen or compass).</param>
    public QuestPathBridge(
        IDungeonFeed feed, Func<string, bool> leadsIn, Func<IReadOnlyList<MarkedActor>, IReadOnlyList<QuestMapPlace>> questPlaces,
        Func<string, Vector3, Vector2, IReadOnlyList<Vector2>?> findPath, GuidePingPlayer ping, ISpeechSink speech,
        LanguageSetting? language = null, IGuideTogglePad? pad = null, Action<Action>? runInBackground = null,
        Action<string>? log = null, Func<TimeSpan>? clock = null, Func<bool>? routeActive = null,
        Func<string, Vector2, float, IReadOnlyList<QuestMapPlace>>? entrances = null, Func<int>? wallsVersion = null,
        Func<IReadOnlyList<MarkedActor>, Vector2, IReadOnlyList<LegionTarget>>? legionTargets = null,
        Func<IReadOnlyList<MarkedActor>, Vector2, string?>? legionMarkers = null, DirectionStyle directionStyle = DirectionStyle.Screen)
    {
        _legionTargets = legionTargets ?? ((_, _) => []);
        _legionMarkers = legionMarkers ?? ((_, _) => null);
        _directionStyle = directionStyle;
        _routeActive = routeActive ?? (() => false);
        _wallsVersion = wallsVersion ?? (() => 0);
        _leadsIn = leadsIn;
        _questPlaces = questPlaces;
        _entrances = entrances ?? ((_, _, _) => []);
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
        lock (_gate)
        {
            (_markers, _markersRead) = (markers, true);
            if (_active?.Legion is not null) TrackLegionTarget();
        }
    }

    /// <summary>The place chosen with Enter in a map menu list (a dungeon, cellar or stronghold, WaypointListBridge): the next D-pad
    /// left leads there, from up to <see cref="MaxDistanceUnits"/> away, until it is reached. A guide running stops, so that press
    /// starts with it.</summary>
    public void Choose(string worldName, string name, Vector2 position)
    {
        lock (_gate)
        {
            _chosen = (worldName, new QuestMapPlace(0, name, 0, position, Chosen: true));
            Log($"PATH chosen in the map list: {name} at ({position.X:F0},{position.Y:F0}) in {worldName}");
            Stop();
        }
    }

    // Follows the active legion target's marker; once the map stops marking it, leads on to the nearest target left.
    private void TrackLegionTarget()
    {
        if (_active is not { } place) return;
        var from = _player is { } p ? new Vector2(p.X, p.Y) : place.Position;
        var targets = _legionTargets(_markers, from);
        var same = targets.Where(t => t.ActorSno == place.ActorSno && Vector2.Distance(t.Position, place.Position) <= TrackUnits)
            .OrderBy(t => Vector2.Distance(t.Position, place.Position)).FirstOrDefault();
        if (same is not null)
        {
            _missingSince = null;
            _active = place with { Position = same.Position };
            if (!_searching && Vector2.Distance(same.Position, _pathTo) > OffPathUnits && _clock() - _lastPath >= RepathInterval && _player is { } player)
            {
                Log($"PATH legion target moved to ({same.Position.X:F1},{same.Position.Y:F1}), finding a new path");
                FindPath(player);
            }
            return;
        }
        _missingSince ??= _clock();
        if (_clock() - _missingSince < GoneAfter) return;
        Log($"PATH legion target done: {place.QuestName} at ({place.Position.X:F1},{place.Position.Y:F1}); {targets.Count} left");
        if (targets.Count == 0)
        {
            Stop();
            _speech.Speak(LegionStrings.For(_language.Current).Done, interrupt: true);
            return;
        }
        Start(LegionPlace(targets[0]), from, "legion target done, the nearest left", 1, 1);
    }

    private QuestMapPlace LegionPlace(LegionTarget target)
    {
        string name = LegionStrings.For(_language.Current).Name(target.Kind);
        if (target.Side is { } side) name = $"{name}, {NavigationPhrases.For(_language.Current).Direction(side, _directionStyle)}";
        return new(0, name, target.ActorSno, target.Position, Legion: target.Kind);
    }

    private void EnterWorld(string worldName)
    {
        Stop();
        _player = null;
        _markers = [];
        _markersRead = false;
        _worldEntered = _clock();
        _world = worldName;
        _leads = _leadsIn(worldName);
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
            if (!_leads || (_active is null && _routeActive())) return;
            var strings = AppStrings.For(_language.Current);
            // A press before the map's first read knows no quest place, and would take a nearby entrance for the goal.
            if (_player is not { } player || (!_markersRead && _clock() - _worldEntered < MapReadWait))
            {
                _speech.Speak(strings.NoQuestPlace, interrupt: true);
                return;
            }
            var here = new Vector2(player.X, player.Y);
            if (_log is not null && _legionMarkers(_markers, here) is { } inLegion) Log($"PATH legion area markers: {inLegion}");
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
                Log($"PATH no quest place among {_markers.Count} map markers, no dungeon entrance within {EntranceRangeUnits:F0}");
                _speech.Speak(strings.NoQuestPlace, interrupt: true);
                return;
            }
            Start(places[next], here, $"D-pad left, {next + 1} of {places.Count}", next + 1, places.Count);
        }
    }

    // The place chosen in a map list first (once the player stands at it, it is done), then a legion's targets (LegionTargets.Near's
    // order, also the one the player stands at), then the marked quest places in reach, the game's own quest markers first, hidden
    // steps last, else nearest first, then the dungeon entrances near the player, nearest first; one per spot.
    private List<QuestMapPlace> Places(Vector2 here)
    {
        var places = new List<QuestMapPlace>();
        if (_chosen is { } chosen && chosen.World == _world)
        {
            float distance = Vector2.Distance(here, chosen.Place.Position);
            if (distance <= HereUnits) _chosen = null;
            else if (distance <= MaxDistanceUnits) places.Add(chosen.Place);
        }
        foreach (var target in _legionTargets(_markers, here))
            if (!places.Any(p => Vector2.Distance(p.Position, target.Position) <= SamePlaceUnits))
                places.Add(LegionPlace(target));
        var quests = _questPlaces(_markers).OrderBy(p => !p.GameMarker).ThenBy(p => p.Hidden).ThenBy(p => Vector2.Distance(here, p.Position));
        IEnumerable<QuestMapPlace> entrances = _world is { } world ? _entrances(world, here, EntranceRangeUnits).OrderBy(p => Vector2.Distance(here, p.Position)) : [];
        foreach (var place in quests.Concat(entrances))
            if (Vector2.Distance(here, place.Position) is > HereUnits and <= MaxDistanceUnits
                && !places.Any(p => Vector2.Distance(p.Position, place.Position) <= SamePlaceUnits))
                places.Add(place);
        return places;
    }

    // Several places marked: says which one of how many, so further presses are known to reach the others (live 2026-10-21: the
    // last siege tower of "Verwundete Seelen" was the fifth of five, and one press each time led elsewhere).
    private void Start(QuestMapPlace place, Vector2 here, string reason, int number, int count)
    {
        Stop();
        _active = place;
        float distance = Vector2.Distance(here, place.Position);
        string legion = place.Legion is { } kind ? $", legion {kind}" : "";
        Log($"PATH to {place.QuestName} (quest {place.QuestSno}{legion}) at ({place.Position.X:F1},{place.Position.Y:F1}), {distance:F0} units ({reason})");
        var strings = AppStrings.For(_language.Current);
        string format = place.Chosen ? MapMenuStrings.For(_language.Current).ChosenGuideFormat
            : place.Legion is not null ? LegionStrings.For(_language.Current).TargetFormat
            : place.Entrance ? strings.EntranceGuideFormat : strings.PathGuideFormat;
        string text = string.Format(format, place.QuestName, MathF.Round(distance));
        _speech.Speak(count > 1 ? $"{text} {string.Format(strings.PlaceCountFormat, number, count)}" : text, interrupt: true);
        if (_player is { } player) FindPath(player);
    }

    private void FindPath(Vector3 player)
    {
        if (_active is not { } place || _world is not { } world) return;
        int request = ++_request, active = _activeId;
        _lastPath = _clock();
        _pathTo = place.Position;
        _pathWalls = _wallsVersion();
        _searching = true;
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
                if (request == _request) _searching = false;
                if (request != _request || active != _activeId) return; // a newer search, or the guide stopped or moved on
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
        if (place.Legion is not null)
        {
            float to = Vector2.Distance(here, place.Position);
            if (!_held && to <= ArriveUnits)
            {
                Log($"PATH reached legion target {place.QuestName} at ({here.X:F1},{here.Y:F1}); holding until the map stops marking it");
                _ping.PlayArrived();
                _speech.Speak(LegionStrings.For(_language.Current).Reached, interrupt: false);
                _held = true;
            }
            else if (_held && to > HoldUnits) _held = false; // stepped away: lead back to it
            if (_held) { _ping.Silence(); return; }
        }
        else if (Vector2.Distance(here, place.Position) <= ArriveUnits)
        {
            Log($"PATH reached {place.QuestName} at ({here.X:F1},{here.Y:F1})");
            _ping.PlayArrived();
            var strings = AppStrings.For(_language.Current);
            if (place.Chosen) _chosen = null;
            _speech.Speak(place.Chosen ? MapMenuStrings.For(_language.Current).ChosenReached
                : place.Entrance ? strings.EntranceReached : strings.PathEnd, interrupt: false);
            Stop();
            return;
        }
        if (_follower is not { } follower) { _ping.Silence(); return; } // the path is being found

        var (aim, off, remaining) = follower.Follow(here, LookaheadUnits);
        if (off > MovedAwayUnits)
        {
            Log($"PATH stopped: player moved {off:F0} units off the path, to ({here.X:F1},{here.Y:F1})");
            _speech.Speak(AppStrings.For(_language.Current).GuideOff, interrupt: true);
            Stop();
            return;
        }
        if (_searching) { } // the next search waits for the running one; the ping keeps following the old path
        else if (off > OffPathUnits && _clock() - _lastPath >= RepathInterval)
        {
            Log($"PATH off the path at ({here.X:F1},{here.Y:F1}), {off:F0} units, finding a new one");
            FindPath(player); // the ping keeps following the old path meanwhile
        }
        else if (_wallsVersion() != _pathWalls && _clock() - _lastPath >= RepathInterval)
        {
            Log("PATH doors changed, finding a new one"); // a door listed locked only once the player came near
            FindPath(player);
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
        _searching = false;
        _activeId++;
        _held = false;
        _missingSince = null;
    }

    private static float Length(Vector2 from, IReadOnlyList<Vector2> points)
    {
        float length = 0;
        foreach (var p in points) (length, from) = (length + Vector2.Distance(from, p), p);
        return length;
    }

    private void Log(string message) => _log?.Invoke($"[{_clock().TotalSeconds,7:F1}s] {message}");
}
