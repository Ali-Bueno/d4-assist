using System.Diagnostics;
using D4Assist.Audio;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Turns live actor and player snapshots into positional loops (SpatialLoopPlayer) at the actors the
/// <see cref="LoopTargetSelector"/> picks. The feed fires only inside a dungeon; leaving it stops every loop.
/// </summary>
public sealed class ActorLoopBridge
{
    private readonly SpatialLoopPlayer _player;
    private readonly LoopTargetSelector _selector;
    private readonly Func<ActorState, bool> _isUsed;
    private readonly Func<int, string?> _nameOf;
    private readonly Action<string>? _log;
    private readonly ActorSightingLog? _sightings;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private Dictionary<uint, (ActorState Actor, LoopTarget Target)> _targets = [];
    private PlayerState? _lastPlayer;

    /// <param name="isUsed">True once an actor was used (prisoner freed, lever pulled): its loop stops. Default: never.</param>
    /// <param name="log">Debug sink for loop start/stop lines; null when not debugging.</param>
    /// <param name="sightings">Debug log of gizmo and portal-like actors; null when not debugging.</param>
    public ActorLoopBridge(
        IDungeonFeed feed, SpatialLoopPlayer player, LoopTargetSelector selector, Func<int, string?> nameOf,
        Func<ActorState, bool>? isUsed = null, Action<string>? log = null, ActorSightingLog? sightings = null)
    {
        _player = player;
        _selector = selector;
        _nameOf = nameOf;
        _isUsed = isUsed ?? (_ => false);
        _log = log;
        _sightings = sightings;

        feed.WorldChanged += (sno, _, _) => { lock (_gate) Reset(sno); };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
        feed.PlayerMoved += p => { lock (_gate) OnPlayer(p); };
    }

    /// <summary>True while an actor of <paramref name="kind"/> has been detected in the current dungeon.</summary>
    public bool HasTarget(LoopKind kind) { lock (_gate) return _targets.Values.Any(t => t.Target.Kind == kind); }

    private void Reset(int worldSno)
    {
        _targets = [];
        _lastPlayer = null;
        _sightings?.Reset();
        _player.StopAll();
        _selector.SetWorld(worldSno);
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        _sightings?.Note(actors, _lastPlayer);
        var selected = actors
            .Select(a => (Actor: a, Target: _selector.Select(a)))
            .Where(x => x.Target is not null)
            .GroupBy(x => x.Actor.Id).ToDictionary(g => g.Key, g => g.First());
        var next = selected.Where(kv => !_isUsed(kv.Value.Actor)).ToDictionary(kv => kv.Key, kv => (kv.Value.Actor, kv.Value.Target!));
        if (_log is not null) LogChanges(_targets, next, selected.Keys.ToHashSet());
        _targets = next;
    }

    private void OnPlayer(PlayerState p)
    {
        _lastPlayer = p;
        _player.Update([.. _targets.Values.Select(t => ToSource(t.Actor, t.Target, p))]);
    }

    private static LoopSource ToSource(ActorState actor, LoopTarget target, PlayerState p)
    {
        var (right, up) = ScreenDirections.ToScreen(actor.X - p.X, actor.Y - p.Y);
        return new LoopSource(actor.Id.ToString(), target.Sound, right, up, target.Profile);
    }

    private void LogChanges(
        Dictionary<uint, (ActorState Actor, LoopTarget Target)> before, Dictionary<uint, (ActorState Actor, LoopTarget Target)> after,
        HashSet<uint> stillPresent)
    {
        foreach (var t in after.Where(t => !before.ContainsKey(t.Key)).Select(t => t.Value)) Log("started", t.Actor, t.Target, null);
        foreach (var t in before.Where(t => !after.ContainsKey(t.Key)).Select(t => t.Value))
            Log("stopped", t.Actor, t.Target, stillPresent.Contains(t.Actor.Id) ? "used" : "gone");
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
