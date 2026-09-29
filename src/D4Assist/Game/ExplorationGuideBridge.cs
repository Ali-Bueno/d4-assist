using System.Diagnostics;
using D4Assist.Audio;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Game;

/// <summary>
/// Feeds the exploration guide's waypoint to the guide ping on every player poll and tile update. Toggled by D-pad left (off outside endgame worlds by default). Chimes on
/// each target reached, yields to the beacon while it has a target, stays off in Undercity's boss room, and
/// speaks once when nothing is left to explore.
/// </summary>
public sealed class ExplorationGuideBridge
{
    // Undercity's last warp lands in the boss room, where the run timer no longer matters (user, 2026-09-28).
    // CoreTOC scenes: Cave_Bug_, ZigIn_Kurast_ and ProtoDun_Flooded_FLEX_POI_BossRoom_Undercity (research/dungeon-objectives.md §5).
    public const string UndercityBossRoomSuffix = "_FLEX_POI_BossRoom_Undercity";

    private readonly Func<PlayerInput, GuideStep> _step;
    private readonly GuidePingPlayer _ping;
    private readonly ISpeechSink _speech;
    private readonly LanguageSetting _language;
    private readonly Func<bool> _beaconActive;
    private readonly Action<string>? _log;
    private readonly GuideSwitch _switch;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private PlayerInput? _player;
    private string? _loggedTargetId;
    private bool _announcedAllExplored;
    private bool _inBossRoom;
    private bool _muted = true;
    private int _arrivals;

    /// <param name="step">Asks the navigation engine for the next guide step (NavigationBridge.NextGuideStep).</param>
    /// <param name="beaconActive">True while the beacon has a target (ActorLoopBridge.HasTarget(LoopKind.ExitBeacon)).</param>
    /// <param name="pad">D-pad left toggles the guide for the current kind of dungeon; null for no toggle.</param>
    /// <param name="guideInDungeons">Default outside the endgame worlds (config ExplorationGuideInDungeons).</param>
    /// <param name="language">Spoken language; null means Spanish.</param>
    /// <param name="log">Debug sink for target-change and toggle lines; null when not debugging.</param>
    public ExplorationGuideBridge(
        IDungeonFeed feed, Func<PlayerInput, GuideStep> step, GuidePingPlayer ping, ISpeechSink speech,
        Func<bool> beaconActive, Action<string>? log = null, IGuideTogglePad? pad = null, bool guideInDungeons = false,
        LanguageSetting? language = null)
    {
        _language = language ?? new LanguageSetting();
        _switch = new GuideSwitch(guideInDungeons);
        _step = step;
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
            if (_switch.Toggle() is not { } on) return;
            var strings = AppStrings.For(_language.Current);
            _log?.Invoke($"[{_clock.Elapsed.TotalSeconds,7:F1}s] GUIDE toggled {(on ? "on" : "off")}");
            _speech.Speak(on ? strings.GuideOn : strings.GuideOff, interrupt: true);
            Update();
        }
    }

    private void Reset()
    {
        _player = null;
        _loggedTargetId = null;
        _announcedAllExplored = false;
        _inBossRoom = false;
        _muted = true;
        _arrivals = 0;
        _ping.Silence();
    }

    private void NoteBossRoom(IReadOnlyList<TileState> tiles)
    {
        if (_inBossRoom || !tiles.Any(t => t.Name?.EndsWith(UndercityBossRoomSuffix, StringComparison.Ordinal) == true)) return;
        _inBossRoom = true;
        _log?.Invoke($"[{_clock.Elapsed.TotalSeconds,7:F1}s] GUIDE off: Undercity boss room");
    }

    private void Update()
    {
        if (_player is null || !_switch.IsOn || _inBossRoom || _beaconActive()) { _ping.Silence(); _muted = true; return; }

        var step = _step(_player);
        // Targets reached while muted (e.g. beside the exit pad) must not chime late when guiding resumes.
        if (step.Arrivals > _arrivals && !_muted) _ping.PlayArrived();
        _arrivals = Math.Max(_arrivals, step.Arrivals);
        _muted = false;
        switch (step.Status)
        {
            case GuideStatus.Guiding:
                LogTargetChange(step);
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

    private void LogTargetChange(GuideStep step)
    {
        if (_log is null || step.Target is not { } target || target.Id == _loggedTargetId) return;
        _loggedTargetId = target.Id;
        _log($"[{_clock.Elapsed.TotalSeconds,7:F1}s] GUIDE target {target.Kind} {target.Id} at ({target.Position.X:F1},{target.Position.Y:F1}) route={step.RemainingLength:F1}");
    }
}
