using System.Diagnostics;
using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Turns live actor and player snapshots into positional loops (SpatialLoopPlayer) at the actors the
/// <see cref="LoopTargetSelector"/> picks, the nearest few per kind. The feed fires only while a memory session is open (dungeon, radar-only world, overworld); every world change stops every loop.
/// </summary>
public sealed class ActorLoopBridge
{
    // d2r-access src/ground_items.h:42-44: MaxContainerVoices 3, MaxDoorVoices 2; beacons and objectives may use the whole loop budget.
    private const int MaxContainerVoices = 3, MaxDoorVoices = 2;

    private readonly SpatialLoopPlayer _player;
    private readonly LoopTargetSelector _selector;
    private readonly Func<ActorState, bool> _isUsed;
    private readonly Func<SoundCue, CueLevel> _levelOf;
    private readonly GizmoUsedSignal? _used;
    private readonly Func<int, string?> _nameOf;
    private readonly Action<string>? _log;
    private readonly ActorSightingLog? _sightings;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private Dictionary<uint, (ActorState Actor, LoopTarget Target)> _targets = [];
    private readonly HashSet<uint> _loggedUnusable = [];
    private readonly HashSet<uint> _sheltered = [];
    private PlayerState? _lastPlayer;

    /// <param name="isUsed">True once an actor was used (lever pulled, chest opened): its loop stops if its target says so.
    /// Default: <see cref="GizmoUsedSignal"/> (chest glow, else the gizmo state word, remembered by spot).</param>
    /// <param name="log">Debug sink for loop start/stop lines; null when not debugging.</param>
    /// <param name="sightings">Debug log of gizmo and portal-like actors; null when not debugging.</param>
    /// <param name="usedSignal">The default "used" signal when <paramref name="isUsed"/> is null; null builds one with no known chest kinds.</param>
    /// <param name="levelOf">Live gain and repeat rate of each settings cue; a zero gain silences that cue. Null = full.</param>
    public ActorLoopBridge(
        IDungeonFeed feed, SpatialLoopPlayer player, LoopTargetSelector selector, Func<int, string?> nameOf,
        Func<ActorState, bool>? isUsed = null, Action<string>? log = null, ActorSightingLog? sightings = null,
        GizmoUsedSignal? usedSignal = null, Func<SoundCue, CueLevel>? levelOf = null)
    {
        _player = player;
        _levelOf = levelOf ?? (_ => CueLevel.Full);
        _selector = selector;
        _nameOf = nameOf;
        _used = isUsed is null ? usedSignal ?? new GizmoUsedSignal(nameOf) : null;
        _isUsed = isUsed ?? _used!.IsUsed;
        _log = log;
        _sightings = sightings;

        feed.WorldChanged += (sno, _, isDungeon) => { lock (_gate) Reset(sno, isDungeon); };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
        feed.PlayerMoved += p => { lock (_gate) OnPlayer(p); };
    }

    /// <summary>Raised on every actor poll with the objective actors looping now, whatever their volume (ObjectiveMemory).</summary>
    public event Action<IReadOnlyList<ActorState>>? ObjectivesLooping;

    /// <summary>True while an actor of <paramref name="kind"/> has been detected in the current dungeon.</summary>
    public bool HasTarget(LoopKind kind) { lock (_gate) return _targets.Values.Any(t => t.Target.Kind == kind); }

    private void Reset(int worldSno, bool isDungeon)
    {
        _targets = [];
        _loggedUnusable.Clear();
        _sheltered.Clear();
        _lastPlayer = null;
        _sightings?.Reset();
        _used?.Reset();
        _player.StopAll();
        _selector.SetWorld(worldSno, isDungeon);
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        actors = _selector.WithCarryGoals(actors);
        _sightings?.Note(actors, _lastPlayer);
        _used?.Observe(actors, _lastPlayer);
        var selected = actors
            .Select(a => (Actor: a, Target: _selector.Select(a)))
            .Where(x => x.Target is not null)
            .GroupBy(x => x.Actor.Id).ToDictionary(g => g.Key, g => g.First());
        // An unusable gizmo (quest-restricted, script-disabled) offers no interaction: silent until the game allows it.
        var next = selected.Where(kv => !Silent(kv.Value.Actor, kv.Value.Target!))
            .ToDictionary(kv => kv.Key, kv => (kv.Value.Actor, Target: kv.Value.Target!));
        if (_log is not null)
            foreach (var (actor, target) in selected.Values.Where(x =>
                         x.Target!.SilentWhileUnusable && x.Actor.Unusable && !_targets.ContainsKey(x.Actor.Id) && _loggedUnusable.Add(x.Actor.Id)))
                Log("silent", actor, target!, "unusable");
        if (_log is not null) LogChanges(_targets, next, selected.ToDictionary(kv => kv.Key, kv => kv.Value.Actor));
        _targets = next;
        ObjectivesLooping?.Invoke([.. next.Values.Where(t => t.Target.Kind == LoopKind.Objective).Select(t => t.Actor)]);
    }

    // Switched-off cues are dropped before the per-kind cap, so a silent chest never takes a corpse's voice.
    private void OnPlayer(PlayerState p)
    {
        _lastPlayer = p;
        _player.Update([.. _targets.Values
            .Where(t => !Sheltered(t.Actor, t.Target, p))
            .Select(t => (t.Actor, t.Target, Level: _levelOf(t.Target.Cue)))
            .Where(t => t.Level.Gain > 0)
            .GroupBy(t => t.Target.Kind)
            .SelectMany(kind => kind.OrderBy(t => Sq(t.Actor.X - p.X) + Sq(t.Actor.Y - p.Y)).Take(MaxVoices(kind.Key)))
            .Select(t => ToSource(t.Actor, t.Target, t.Level, p))]);
    }

    private bool Silent(ActorState actor, LoopTarget target) =>
        (target.SilentWhileUnusable && actor.Unusable) || (target.StopsWhenUsed && _isUsed(actor));

    // Inside a safe zone its loop has done its job: silent until the player steps out.
    private bool Sheltered(ActorState actor, LoopTarget target, PlayerState p)
    {
        bool inside = target.QuietWithin > 0 && Sq(actor.X - p.X) + Sq(actor.Y - p.Y) <= Sq(target.QuietWithin);
        if (_log is not null && (inside ? _sheltered.Add(actor.Id) : _sheltered.Remove(actor.Id)))
            Log(inside ? "inside" : "outside", actor, target, null);
        return inside;
    }

    /// <summary>How many loops of one kind may sound at once (the nearest ones).</summary>
    public static int MaxVoices(LoopKind kind) => kind switch
    {
        LoopKind.Door => MaxDoorVoices,
        LoopKind.Container => MaxContainerVoices,
        _ => AudioMixer.MaxLoopVoices,
    };

    private static LoopSource ToSource(ActorState actor, LoopTarget target, CueLevel level, PlayerState p)
    {
        var (right, up) = ScreenDirections.ToScreen(actor.X - p.X, actor.Y - p.Y);
        var profile = target.Profile with { NearGain = target.Profile.NearGain * level.Gain };
        return new LoopSource(actor.Id.ToString(), target.Sound, right, up, profile, Priority: (int)target.Kind, Rate: level.Rate);
    }

    private void LogChanges(
        Dictionary<uint, (ActorState Actor, LoopTarget Target)> before, Dictionary<uint, (ActorState Actor, LoopTarget Target)> after,
        Dictionary<uint, ActorState> present)
    {
        foreach (var t in after.Where(t => !before.ContainsKey(t.Key)).Select(t => t.Value)) Log("started", t.Actor, t.Target, null);
        foreach (var t in before.Where(t => !after.ContainsKey(t.Key)).Select(t => t.Value))
            Log("stopped", t.Actor, t.Target, !present.TryGetValue(t.Actor.Id, out var now) ? "gone" : now.Unusable ? "unusable" : "used");
    }

    private void Log(string what, ActorState a, LoopTarget target, string? cause)
    {
        string distance = _lastPlayer is { } p ? $"{Math.Sqrt(Sq(a.X - p.X) + Sq(a.Y - p.Y)):F1}" : "?";
        string because = cause is null ? "" : $" cause={cause}";
        _log!($"[{_clock.Elapsed.TotalSeconds,7:F1}s] LOOP {what} ({target.Kind}): {_nameOf(a.ActorSno) ?? a.ActorSno.ToString()} id={a.Id} " +
              $"at ({a.X:F1},{a.Y:F1},{a.Z:F1}) distance={distance} reason={target.Reason}{because}");
    }

    private static double Sq(double v) => v * v;
}
