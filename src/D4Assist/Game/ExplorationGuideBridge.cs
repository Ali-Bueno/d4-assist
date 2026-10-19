using System.Diagnostics;
using D4Assist.Audio;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Game;

/// <summary>
/// Feeds the exploration guide's waypoint to the guide ping on every player poll and tile update. Toggled by D-pad left (off outside endgame worlds by default);
/// switching it on says where it leads. Outside the endgame it leads first to an objective heard earlier (ObjectiveMemory) and holds,
/// silent, while the player stands at it. Chimes on each target reached, yields to the beacon while an unused portal is in sight, stays off in Undercity's boss room, and
/// once nothing is left to explore leads to the portals seen earlier (PortalMemory) or says so once.
/// </summary>
public sealed class ExplorationGuideBridge
{
    // Undercity's last warp lands in the boss room, where the run timer no longer matters (user, 2026-09-28).
    // CoreTOC scenes: Cave_Bug_, ZigIn_Kurast_ and ProtoDun_Flooded_FLEX_POI_BossRoom_Undercity (research/dungeon-objectives.md §5).
    public const string UndercityBossRoomSuffix = "_FLEX_POI_BossRoom_Undercity";

    // What the guide last announced it leads to; each portal is announced once by its own id.
    private const string ExploreGoal = "explore", QuestGoal = "quest", DungeonGoal = "dungeon", PortalGoal = "portal";

    private static readonly IReadOnlyList<GuideDestination> NoPoints = [];

    private readonly Func<PlayerInput, bool, IReadOnlyList<GuideDestination>, IReadOnlyList<GuideDestination>, GuideStep> _step;
    private readonly Func<IReadOnlyList<GuideDestination>> _destinations;
    private readonly Func<IReadOnlyList<GuideDestination>> _objectives;
    private readonly Action? _rearmObjectives;
    private readonly GuidePingPlayer _ping;
    private readonly ISpeechSink _speech;
    private readonly LanguageSetting _language;
    private readonly Func<bool> _beaconActive;
    private readonly Func<bool> _routeActive;
    private readonly Action<string>? _log;
    private readonly GuideSwitch _switch;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private PlayerInput? _player;
    private string? _loggedTargetId;
    private bool _announcedAllExplored;
    private string? _announcedGoal = ExploreGoal;
    private string? _announcedDestination;
    private int _announcedRound;
    private bool _inBossRoom;
    private bool _muted = true;
    private int _arrivals;
    private string? _heldAt;
    private string? _heightToldFor;

    // An objective is reached inside its radius and on its floor: a height gap up to this (or the radius, if larger) is the
    // same floor. Live 2026-10-01: the Rathma temple's trigger lay 23 units below the walkway the player stood on.
    private const float SameFloorUnits = 3f;
    // Over or under an objective within this flat distance, the guide says once that it is lower or higher.
    private const float HeightHintUnits = ExplorationGuide.DestinationArrivalUnits;

    /// <param name="step">Asks the navigation engine for the next guide step: the flag asks it to re-check exit candidates when nothing is left to explore,
    /// the lists are where to lead after that and the objectives to lead to before exploring (NavigationBridge.NextGuideStep).</param>
    /// <param name="beaconActive">True while an unused portal's beacon is in sight (PortalMemory.FreshInSight).</param>
    /// <param name="pad">D-pad left toggles the guide for the current kind of dungeon; null for no toggle.</param>
    /// <param name="guideInDungeons">Default outside the endgame worlds (config ExplorationGuideInDungeons).</param>
    /// <param name="language">Spoken language; null means Spanish.</param>
    /// <param name="log">Debug sink for target-change and toggle lines; null when not debugging.</param>
    /// <param name="destinations">Portals seen in this dungeon, unused ones in tier 0 (PortalMemory.Destinations); null for none.</param>
    /// <param name="objectives">Objectives heard in this dungeon, quest steps in tier 0 (ObjectiveMemory.Destinations); null for none. Never used in endgame worlds.</param>
    /// <param name="rearmObjectives">Called when the guide is switched on: objectives already reached lead again (ObjectiveMemory.Rearm).</param>
    /// <param name="routeActive">True while a quest route takes D-pad left outside dungeons (QuestRouteBridge.IsActive); null for none.</param>
    public ExplorationGuideBridge(
        IDungeonFeed feed, Func<PlayerInput, bool, IReadOnlyList<GuideDestination>, IReadOnlyList<GuideDestination>, GuideStep> step,
        GuidePingPlayer ping, ISpeechSink speech, Func<bool> beaconActive, Action<string>? log = null, IGuideTogglePad? pad = null,
        bool guideInDungeons = false, LanguageSetting? language = null, Func<IReadOnlyList<GuideDestination>>? destinations = null,
        Func<IReadOnlyList<GuideDestination>>? objectives = null, Action? rearmObjectives = null, Func<bool>? routeActive = null)
    {
        _routeActive = routeActive ?? (() => false);
        _language = language ?? new LanguageSetting();
        _switch = new GuideSwitch(guideInDungeons);
        _step = step;
        _destinations = destinations ?? (() => NoPoints);
        _objectives = objectives ?? (() => NoPoints);
        _rearmObjectives = rearmObjectives;
        _ping = ping;
        _speech = speech;
        _beaconActive = beaconActive;
        _log = log;

        if (feed.CurrentWorld is { } startWorld) _switch.WorldChanged(startWorld.Name, startWorld.IsDungeon); // app started mid-dungeon
        feed.WorldChanged += (_, name, isDungeon) => { lock (_gate) { Reset(); _switch.WorldChanged(name, isDungeon); } };
        if (pad is not null) pad.DpadLeftPressed += OnTogglePressed;
        feed.TilesUpdated += tiles => { lock (_gate) { NoteBossRoom(tiles); Update(); } };
        feed.PlayerMoved += p => { lock (_gate) { _player = new PlayerInput(p.X, p.Y, p.Z); Update(); } };
    }

    private void OnTogglePressed()
    {
        lock (_gate)
        {
            var strings = AppStrings.For(_language.Current);
            if (_switch.Toggle() is not { } on)
            {
                // A silent press sounds like a pad the app can't hear (tester, 2026-10-16).
                if (!_routeActive()) _speech.Speak(strings.GuideDungeonsOnly, interrupt: true);
                return;
            }
            _log?.Invoke($"[{_clock.Elapsed.TotalSeconds,7:F1}s] GUIDE toggled {(on ? "on" : "off")}");
            _speech.Speak(on ? strings.GuideOn : strings.GuideOff, interrupt: true);
            if (on)
            {
                // A player who got lost switches the guide on to hear where it leads, objectives walked past included.
                _rearmObjectives?.Invoke();
                _announcedGoal = null;
                _announcedDestination = null;
                _announcedAllExplored = false;
                _announcedRound = 0;
            }
            Update();
        }
    }

    private void Reset()
    {
        _player = null;
        _loggedTargetId = null;
        _announcedAllExplored = false;
        _announcedGoal = ExploreGoal; // a new floor starts exploring without a word
        _announcedDestination = null;
        _announcedRound = 0;
        _inBossRoom = false;
        _arrivals = 0;
        Mute();
    }

    private void NoteBossRoom(IReadOnlyList<TileState> tiles)
    {
        if (_inBossRoom || !tiles.Any(t => t.Name?.EndsWith(UndercityBossRoomSuffix, StringComparison.Ordinal) == true)) return;
        _inBossRoom = true;
        _log?.Invoke($"[{_clock.Elapsed.TotalSeconds,7:F1}s] GUIDE off: Undercity boss room");
    }

    private void Mute()
    {
        _ping.Silence();
        _muted = true;
    }

    private void Update()
    {
        if (!_switch.InDungeon) return; // outside dungeons the shared ping belongs to the quest routes (QuestRouteBridge)
        if (_player is null || !_switch.IsOn || _inBossRoom) { Mute(); return; }

        var destinations = _destinations();
        // Endgame runs keep their tuned order: explore, re-check the dead ends, then the portals.
        var objectives = _switch.IsEndgame ? NoPoints : _objectives();
        // An unused portal in sight leads by its own beacon, unless an objective heard earlier still waits.
        if (_beaconActive() && objectives.Count == 0) { Mute(); return; }

        // An unused portal already seen is the floor exit: lead back to it instead of re-checking dead ends.
        bool exitMissing = _switch.IsEndgame && !destinations.Any(d => d.Tier == 0);
        var step = _step(_player, exitMissing, destinations, objectives);
        if (_beaconActive() && step.Target?.Kind != GuideTargetKind.Objective)
        {
            _arrivals = Math.Max(_arrivals, step.Arrivals);
            Mute();
            return;
        }
        // Targets reached while muted (e.g. beside the exit pad) must not chime late when guiding resumes.
        if (step.Arrivals > _arrivals && !_muted) _ping.PlayArrived();
        _arrivals = Math.Max(_arrivals, step.Arrivals);
        _muted = false;
        switch (step.Status)
        {
            case GuideStatus.Rechecking:
                AnnounceRound(step);
                goto case GuideStatus.Guiding;
            case GuideStatus.Guiding:
                LogTargetChange(step);
                AnnounceGoal(step, destinations, objectives);
                if (HoldsAtObjective(step, objectives)) { _ping.Silence(); break; }
                var (right, up) = ScreenDirections.ToScreen(step.Waypoint.X - _player.X, step.Waypoint.Y - _player.Y);
                _ping.Aim(right, up, step.RemainingLength);
                break;
            case GuideStatus.AllExplored:
                _ping.Silence();
                _loggedTargetId = null;
                if (_announcedAllExplored) break;
                _announcedAllExplored = true;
                _speech.Speak(NavigationPhrases.For(_language.Current).AllExplored, interrupt: false);
                break;
            default:
                _ping.Silence(); // off the known grid: nothing to lead by
                break;
        }
    }

    // Once per goal: an objective (quest step or dungeon), each portal (a used one leads back the way the player came), and
    // exploring again after an objective or when switched on. A re-check round is spoken by AnnounceRound.
    private void AnnounceGoal(GuideStep step, IReadOnlyList<GuideDestination> destinations, IReadOnlyList<GuideDestination> objectives)
    {
        if (step.Target is not { } target) return;
        var phrases = NavigationPhrases.For(_language.Current);
        if (target.Kind == GuideTargetKind.Destination)
        {
            _announcedGoal = PortalGoal;
            if (target.Id == _announcedDestination) return;
            _announcedDestination = target.Id;
            bool back = destinations.FirstOrDefault(d => d.Id == target.Id)?.Tier > 0;
            var strings = AppStrings.For(_language.Current);
            _speech.Speak(back ? strings.GuideToUsedPortal : strings.GuideToPortal, interrupt: false);
            return;
        }

        // Exploring towards an objective the map knows of is leading to that objective.
        string? goal = target.Kind switch
        {
            GuideTargetKind.Objective => GoalOf(objectives.FirstOrDefault(o => o.Id == target.Id)),
            GuideTargetKind.UnvisitedTile or GuideTargetKind.Frontier => step.Toward is { } toward ? GoalOf(toward) : ExploreGoal,
            _ => null,
        };
        if (goal is null || goal == _announcedGoal) return;
        bool speak = goal != ExploreGoal || _announcedGoal is null or QuestGoal or DungeonGoal;
        _announcedGoal = goal;
        if (speak) _speech.Speak(goal switch { QuestGoal => phrases.ToQuestObjective, DungeonGoal => phrases.ToDungeonObjective, _ => phrases.ToUnexplored }, interrupt: false);
    }

    private static string GoalOf(GuideDestination? objective) => objective?.Tier == ObjectiveMemory.QuestTier ? QuestGoal : DungeonGoal;

    private void AnnounceRound(GuideStep step)
    {
        if (step.Round == _announcedRound) return;
        _announcedRound = step.Round;
        _log?.Invoke($"[{_clock.Elapsed.TotalSeconds,7:F1}s] GUIDE recheck round {step.Round}");
        _speech.Speak(NavigationPhrases.For(_language.Current).Rechecking, interrupt: false);
    }

    // An objective stays the target until it is done or its phase moves on (ObjectiveMemory): at it (its radius, its floor), one
    // arrival chime, then silence while its own loop sounds; over or under it, the guide says once which way it is and keeps pointing.
    private bool HoldsAtObjective(GuideStep step, IReadOnlyList<GuideDestination> objectives)
    {
        if (step.Target is not { Kind: GuideTargetKind.Objective } target || objectives.FirstOrDefault(o => o.Id == target.Id) is not { Radius: > 0 } place)
        {
            _heldAt = null;
            return false;
        }
        var player = _player!;
        float flat = MathF.Sqrt(MathF.Pow(target.Position.X - player.X, 2) + MathF.Pow(target.Position.Y - player.Y, 2));
        float radius = place.Radius;
        float rise = place.Z is { } z ? z - player.Z : 0;
        bool sameFloor = MathF.Abs(rise) <= MathF.Max(radius, SameFloorUnits);
        if (flat > HeightHintUnits * 2) _heightToldFor = null;
        if (!sameFloor && flat <= HeightHintUnits && _heightToldFor != target.Id)
        {
            _heightToldFor = target.Id;
            var phrases = NavigationPhrases.For(_language.Current);
            _speech.Speak(rise < 0 ? phrases.QuestObjectiveBelow : phrases.QuestObjectiveAbove, interrupt: false);
        }
        if (sameFloor && flat <= (_heldAt == target.Id ? radius * 1.5f : radius))
        {
            if (_heldAt == target.Id) return true;
            _heldAt = target.Id;
            _ping.PlayArrived();
            _log?.Invoke($"[{_clock.Elapsed.TotalSeconds,7:F1}s] GUIDE holding at {target.Id}");
            return true;
        }
        _heldAt = null;
        return false;
    }

    private void LogTargetChange(GuideStep step)
    {
        if (_log is null || step.Target is not { } target || target.Id == _loggedTargetId) return;
        _loggedTargetId = target.Id;
        _log($"[{_clock.Elapsed.TotalSeconds,7:F1}s] GUIDE target {target.Kind} {target.Id} at ({target.Position.X:F1},{target.Position.Y:F1}) route={step.RemainingLength:F1}");
    }
}
