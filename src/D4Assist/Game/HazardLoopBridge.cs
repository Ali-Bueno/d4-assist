using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// In a fight with a boss or an elite, a loop on every special or area attack from the moment it is telegraphed until it is gone,
/// placed where the attack is and moving with it: the warning on the ground, the guardian's wind-up, its wave wall at the segment
/// nearest the player, a tornado as it drifts (user, 2026-10-24: spoken directions came too late; the player dodges by ear).
/// Telegraphs are told apart by TelegraphClassifier; one attack's effects (a warning's swirl, volume and light; a wall's 13
/// segments) sound as one loop.
/// </summary>
public sealed class HazardLoopBridge
{
    /// <summary>Sound id of the attack loop: Sounds/hazard.wav (embedded, or the file of that name in the Sounds folder beside the
    /// app, which wins).</summary>
    public const string HazardSound = "hazard";

    /// <summary>Audible to this distance, loudest on the player; linear fade.</summary>
    public static readonly LoopProfile Profile = new(Reach: TelegraphWarner.WarnRangeUnits, NearGain: 1.0, Falloff: 1);

    /// <summary>At most this many attacks sound at once (the nearest ones), leaving the objects' loops their budget.</summary>
    public const int MaxLoops = 4;

    // Effects of one attack: the same kind within this of each other.
    private const float SameAttackUnits = TelegraphWarner.SameWarningUnits;

    // Loops sound before the objects' loops.
    private const int HazardPriority = -1;

    private readonly TelegraphClassifier _telegraphs;
    private readonly Func<ActorState, EnemyClass?> _enemyClass;
    private readonly Func<int, bool> _isProjectile;
    private readonly Action<IReadOnlyCollection<LoopSource>> _update;
    private readonly Func<CueLevel> _level;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private IReadOnlyList<Attack> _attacks = [];
    private readonly HashSet<string> _logged = [];
    private PlayerState? _player;

    /// <param name="update">Applies this moment's loops (a SpatialLoopPlayer of its own).</param>
    /// <param name="level">The telegraph cue's volume and switch (the settings' Danger group).</param>
    public HazardLoopBridge(
        IDungeonFeed feed, TelegraphClassifier telegraphs, Func<ActorState, EnemyClass?> enemyClass, Func<int, bool> isProjectile,
        Action<IReadOnlyCollection<LoopSource>> update, Func<CueLevel>? level = null, Action<string>? log = null)
    {
        (_telegraphs, _enemyClass, _isProjectile, _update, _log) = (telegraphs, enemyClass, isProjectile, update, log);
        _level = level ?? (() => CueLevel.Full);
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _attacks = []; _player = null; _logged.Clear(); _update([]); } };
        feed.PlayerMoved += p =>
        {
            if (p.IsWorldUnloaded) return;
            lock (_gate) { _player = p; Apply(); }
        };
        feed.ActorsUpdated += actors => { lock (_gate) { _attacks = Attacks(actors); Apply(); } };
    }

    /// <summary>One attack: its kind and the spots it covers (a wall's segments, a warning's effects).</summary>
    private sealed record Attack(string Key, string Kind, List<(float X, float Y)> Points, float Yaw);

    private IReadOnlyList<Attack> Attacks(IReadOnlyList<ActorState> actors)
    {
        if (_player is not { } player) return [];
        bool fight = actors.Any(a => !a.ClientOnly && _enemyClass(a) is not null && Distance(a.X, a.Y, player) <= TelegraphWarner.FightRangeUnits);
        if (!fight) return [];
        var attacks = new List<Attack>();
        foreach (var actor in actors.OrderBy(a => a.Id))
        {
            if (Kind(actor) is not { } kind) continue;
            // A wall's segments share its yaw and rise in a row; a warning's effects share a spot.
            var same = attacks.FirstOrDefault(a => a.Kind == kind && (kind == nameof(TelegraphShape.Wave)
                ? SameWay(a.Yaw, actor.Yaw) && a.Points.Any(p => Distance(p.X, p.Y, actor.X, actor.Y) <= 3 * SameAttackUnits)
                : a.Points.Any(p => Distance(p.X, p.Y, actor.X, actor.Y) <= SameAttackUnits)));
            if (same is not null) same.Points.Add((actor.X, actor.Y));
            else attacks.Add(new Attack($"hazard:{actor.Id}", kind, [(actor.X, actor.Y)], actor.Yaw));
        }
        if (_log is not null)
        {
            foreach (var a in attacks.Where(a => _logged.Add(a.Key)))
                _log($"HAZARD LOOP {a.Kind} at ({a.Points[0].X:F1},{a.Points[0].Y:F1}) distance={Distance(a.Points[0].X, a.Points[0].Y, player):F1} parts={a.Points.Count}");
            _logged.IntersectWith(attacks.Select(a => a.Key));
        }
        return attacks;
    }

    // What attack an actor is part of, or null: a warning by its marked shape, the wall's wind-up, a projectile on the move.
    private string? Kind(ActorState actor)
    {
        if (actor.ClientOnly)
            return _telegraphs.IsWallWindup(actor.ActorSno) ? "WallWindup" : _telegraphs.Of(actor.ActorSno)?.ToString();
        return _isProjectile(actor.ActorSno) ? _telegraphs.OfProjectile(actor.ActorSno)?.ToString() : null;
    }

    private void Apply()
    {
        if (_player is not { } player) return;
        var level = _level();
        if (level.Gain <= 0) { _update([]); return; }
        var profile = Profile with { NearGain = Profile.NearGain * level.Gain };
        var sources = _attacks
            .Select(a => (Attack: a, At: a.Points.MinBy(p => Distance(p.X, p.Y, player))))
            .Where(x => Distance(x.At.X, x.At.Y, player) < Profile.Reach)
            .OrderBy(x => Distance(x.At.X, x.At.Y, player)).Take(MaxLoops)
            .Select(x =>
            {
                var (right, up) = ScreenDirections.ToScreen(x.At.X - player.X, x.At.Y - player.Y);
                return new LoopSource(x.Attack.Key, HazardSound, right, up, profile, HazardPriority, level.Rate);
            })
            .ToList();
        _update(sources);
    }

    private static bool SameWay(float a, float b)
    {
        float d = MathF.Abs(a - b) % (2 * MathF.PI);
        return MathF.Min(d, 2 * MathF.PI - d) < MathF.PI / 6;
    }

    private static float Distance(float x, float y, PlayerState p) => Distance(x, y, p.X, p.Y);

    private static float Distance(float ax, float ay, float bx, float by) => MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
}
