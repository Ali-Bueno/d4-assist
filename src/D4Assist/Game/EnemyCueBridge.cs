using D4Assist.Audio;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Aims D2R's enemy cue (EnemyCuePlayer) at the nearest living enemy of each class within reach, on every player poll: one per
/// class, as D2R keeps one representative per class (d2r-access src/entity_cues.cpp). Everywhere, quest or not. An invulnerable
/// one (a boss between phases, SteadyInvulnerability) is silent, so what must be killed meanwhile stands out, unless the player
/// carries what binds him (CarryGoals.IsBossToBind). Silent on a world change.
/// </summary>
public sealed class EnemyCueBridge
{
    private readonly EnemyCuePlayer _player;
    private readonly Func<ActorState, EnemyClass?> _classOf;
    private readonly Func<int, string?> _nameOf;
    private readonly Action<string>? _log;
    private readonly Func<IReadOnlyList<ActorState>, IReadOnlyList<ActorState>> _withGoals;
    // Feed events arrive on different poll threads.
    private readonly object _gate = new();
    private IReadOnlyList<(ActorState Actor, EnemyClass Class)> _enemies = [];
    private HashSet<uint> _cued = [];

    /// <param name="classOf">The enemy class of an actor, null for none (EnemyClassifier.Of).</param>
    /// <param name="log">Debug sink for target changes; null when not debugging.</param>
    /// <param name="withGoals">Rewrites one actor poll before it is read: a boss to bind with what the player carries reads as
    /// vulnerable (CarryGoals.IsBossToBind). Null for none.</param>
    public EnemyCueBridge(
        IDungeonFeed feed, EnemyCuePlayer player, Func<ActorState, EnemyClass?> classOf, Func<int, string?> nameOf, Action<string>? log = null,
        Func<IReadOnlyList<ActorState>, IReadOnlyList<ActorState>>? withGoals = null)
    {
        _withGoals = withGoals ?? (actors => actors);
        _player = player;
        _classOf = classOf;
        _nameOf = nameOf;
        _log = log;
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _enemies = []; _cued = []; _player.Silence(); } };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) Aim(p); };
    }

    // A dead enemy's remains have no game record (ClientOnly).
    private void OnActors(IReadOnlyList<ActorState> actors) =>
        _enemies = [.. _withGoals(actors).Where(a => !a.ClientOnly && !a.Invulnerable)
            .Select(a => (Actor: a, Class: _classOf(a)))
            .Where(e => e.Class is not null)
            .Select(e => (e.Actor, e.Class!.Value))];

    private void Aim(PlayerState p)
    {
        var nearest = _enemies.Select(e => (e.Actor, e.Class, Distance: Distance(e.Actor, p)))
            .Where(e => e.Distance <= EnemyCuePlayer.RangeUnits)
            .GroupBy(e => e.Class).Select(g => g.MinBy(e => e.Distance)).ToList();
        foreach (var (enemy, kind, distance) in nearest.Where(e => !_cued.Contains(e.Actor.Id)))
            _log?.Invoke($"ENEMY cue ({kind}): {_nameOf(enemy.ActorSno) ?? enemy.ActorSno.ToString()} id={enemy.Id} " +
                         $"at ({enemy.X:F1},{enemy.Y:F1}) distance={distance:F1}");
        _cued = [.. nearest.Select(e => e.Actor.Id)];
        _player.Aim([.. nearest.Select(e =>
        {
            var (right, up) = ScreenDirections.ToScreen(e.Actor.X - p.X, e.Actor.Y - p.Y);
            return new EnemyAim(e.Actor.Id, e.Class, right, up, e.Distance);
        })]);
    }

    private static float Distance(ActorState a, PlayerState p) => MathF.Sqrt((a.X - p.X) * (a.X - p.X) + (a.Y - p.Y) * (a.Y - p.Y));
}
