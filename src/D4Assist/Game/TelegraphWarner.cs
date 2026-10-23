using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using D4Assist.Audio;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Game;

/// <summary>
/// Tells a boss attack's warning effect apart by name (research/boss-telegraphs.md): the effects that mark the ground before an
/// attack lands are client actors named for it (<c>fxKit_damaging_persistentWarning_spirits_swirl_fxMesh</c>,
/// <c>fxKit_damaging_coneWarning_*</c>, <c>*_lineWarning_*</c>, <c>MonsterAffix_Chaos_WarningRing</c>,
/// <c>monsterAffix_mortar_warning_cyl</c>); the name also gives the marked shape. A player's or an ally's own warnings, and the
/// signs of rare spawns, are not attacks.
/// </summary>
public sealed partial class TelegraphClassifier(Func<int, string?> nameOf)
{
    private readonly ConcurrentDictionary<int, TelegraphShape?> _shapes = new();

    private readonly ConcurrentDictionary<int, TelegraphShape?> _projectiles = new();

    public TelegraphShape? Of(int actorSno) => _shapes.GetOrAdd(actorSno, sno => Classify(nameOf(sno)));

    /// <summary>A projectile's hazard (eType 10, it has an ACD): the guardian's wave wall and tornadoes (live 2026-10-23).</summary>
    public TelegraphShape? OfProjectile(int actorSno) => _projectiles.GetOrAdd(actorSno, sno => ClassifyProjectile(nameOf(sno)));

    public static TelegraphShape? ClassifyProjectile(string? name)
    {
        if (name is null || NotAnAttack().IsMatch(name)) return null;
        if (name.Contains("wall", StringComparison.OrdinalIgnoreCase)) return TelegraphShape.Wave;
        if (Mover().IsMatch(name)) return TelegraphShape.Mover;
        return null;
    }

    [GeneratedRegex("tornado|vortex|whirlwind", RegexOptions.IgnoreCase)]
    private static partial Regex Mover();

    public string? NameOf(int actorSno) => nameOf(actorSno);

    public static TelegraphShape? Classify(string? name)
    {
        if (name is null || !WarningName().IsMatch(name) || NotAnAttack().IsMatch(name)) return null;
        if (name.Contains("cone", StringComparison.OrdinalIgnoreCase)) return TelegraphShape.Cone;
        if (name.Contains("line", StringComparison.OrdinalIgnoreCase) || name.Contains("ProjWarning", StringComparison.OrdinalIgnoreCase))
            return TelegraphShape.Line;
        if (name.Contains("ring", StringComparison.OrdinalIgnoreCase)) return TelegraphShape.Ring;
        return TelegraphShape.Circle;
    }

    [GeneratedRegex("warning|telegraph", RegexOptions.IgnoreCase)]
    private static partial Regex WarningName();

    // The player classes' and allies' own effects (Warlock_DemonicGaze_warning, rogue_deathTrap_warning_cloneTrail), the signs of
    // rare spawns and towns, quest staging.
    [GeneratedRegex("^(Warlock|Rogue|Sorc|Necro|Barb|Druid|Duid|Paladin|Spiritborn|npc|merc|PvP|emote|ui_|war_)|_Sign$|Town|Mirage|RitualFx",
        RegexOptions.IgnoreCase)]
    private static partial Regex NotAnAttack();
}

/// <summary>
/// In a fight with a boss or an elite, warns of an attack about to land where the player stands: a sharp cue panned toward the
/// marked ground and "Dodge: &lt;direction&gt;" (DodgePlanner), as a sighted player steps out of the warning on the floor. Live
/// 2026-10-23, the Horadric guardian: its spirit bombs mark the player's spot 3-4 s before they burst there.
/// </summary>
public sealed class TelegraphWarner
{
    /// <summary>Warnings farther than this from the player are not theirs to dodge.</summary>
    public const float WarnRangeUnits = 25f;

    /// <summary>A boss or elite within this makes it a fight.</summary>
    public const float FightRangeUnits = 40f;

    /// <summary>One warning is several effects on one spot (swirl, volume, light): within this distance and time they count once.</summary>
    public const float SameWarningUnits = 2.5f;
    public static readonly TimeSpan SameWarningWindow = TimeSpan.FromSeconds(1.5);

    /// <summary>At most one spoken direction this often; the cue still marks every warning.</summary>
    public static readonly TimeSpan SpeechGap = TimeSpan.FromSeconds(1.2);

    private readonly TelegraphClassifier _telegraphs;
    private readonly Func<ActorState, EnemyClass?> _enemyClass;
    private readonly ISpeechSink _speech;
    private readonly LanguageSetting _language;
    private readonly Action<double> _playCue;
    private readonly Func<float, float, bool>? _blocked;
    private readonly Action<string>? _log;
    private readonly Func<int, bool>? _isProjectile;
    private readonly Func<TimeSpan> _now;
    private readonly object _gate = new();
    private readonly HashSet<uint> _seen = [];
    private readonly List<(float X, float Y, TimeSpan At)> _recent = [];
    private PlayerState? _player;
    private TimeSpan? _lastSpoken;

    /// <param name="enemyClass">An actor's enemy class, null for none (EnemyClassifier.Of): a boss or elite near makes a fight.</param>
    /// <param name="playCue">Plays the warning cue at a pan (-1 left .. 1 right).</param>
    /// <param name="blocked">True for a world point a step can't cross (the radar's walls and closed doors); null for none.</param>
    /// <param name="isProjectile">True for a projectile's actor SNO (eType 10), logged in debug with the damaging effects.</param>
    public TelegraphWarner(
        IDungeonFeed feed, TelegraphClassifier telegraphs, Func<ActorState, EnemyClass?> enemyClass, ISpeechSink speech,
        LanguageSetting language, Action<double> playCue, Func<float, float, bool>? blocked = null, Action<string>? log = null,
        Func<TimeSpan>? now = null, Func<int, bool>? isProjectile = null)
    {
        (_telegraphs, _enemyClass, _speech, _language, _playCue, _blocked, _log) = (telegraphs, enemyClass, speech, language, playCue, blocked, log);
        _isProjectile = isProjectile;
        var clock = Stopwatch.StartNew();
        _now = now ?? (() => clock.Elapsed);
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _seen.Clear(); _recent.Clear(); _moverWarned.Clear(); _moverLast.Clear(); _lastWave = null; _player = null; } };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) _player = p; };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        var effects = actors.Where(a => a.ClientOnly).ToList();
        var fresh = effects.Where(a => !_seen.Contains(a.Id)).ToList();
        _seen.IntersectWith(effects.Select(a => a.Id));
        _seen.UnionWith(fresh.Select(a => a.Id));
        var movers = actors.Where(a => !a.ClientOnly && _isProjectile?.Invoke(a.ActorSno) == true)
            .Select(a => (Actor: a, Shape: _telegraphs.OfProjectile(a.ActorSno))).Where(m => m.Shape is not null).ToList();
        foreach (var id in _moverWarned.Keys.Except(movers.Select(m => m.Actor.Id)).ToList()) _moverWarned.Remove(id);
        if (_player is not { } player) return;
        var foe = actors.Where(a => !a.ClientOnly && _enemyClass(a) is not null && Distance(a, player) <= FightRangeUnits)
            .OrderBy(a => Distance(a, player)).FirstOrDefault();
        if (foe is null) return; // no fight: an effect named "warning" is someone else's business
        var now = _now();
        _recent.RemoveAll(r => now - r.At > SameWarningWindow);
        if (_log is not null) LogHazards(actors, player);
        foreach (var effect in fresh)
        {
            if (_telegraphs.Of(effect.ActorSno) is not { } shape || Distance(effect, player) > WarnRangeUnits) continue;
            if (_recent.Any(r => MathF.Abs(r.X - effect.X) + MathF.Abs(r.Y - effect.Y) <= SameWarningUnits)) continue;
            _recent.Add((effect.X, effect.Y, now));
            Warn(effect, shape, player, foe, now);
        }
        // Hazards on the move are weighed every poll: a tornado drifts onto the player, a wall sweeps toward them. A wall's
        // segments rise together: one warning for all of them.
        foreach (var (mover, shape) in movers)
        {
            if (Distance(mover, player) > WarnRangeUnits || _moverWarned.ContainsKey(mover.Id)) continue;
            if (shape == TelegraphShape.Wave && _lastWave is { } wave && now - wave < MoverRewarn) continue;
            // A wall faces the way it sweeps; a tornado drifts its own way, read from where it was a poll ago.
            if (Heading(mover, shape!.Value) is not { } heading) continue;
            var moving = mover with { Yaw = heading };
            if (DodgePlanner.Plan(shape.Value, (moving.X, moving.Y), heading, (player.X, player.Y), (foe.X, foe.Y)) is null) continue;
            _moverWarned[mover.Id] = now;
            if (shape == TelegraphShape.Wave) _lastWave = now;
            Warn(moving, shape.Value, player, foe, now);
        }
        foreach (var (mover, _) in movers) _moverLast[mover.Id] = (mover.X, mover.Y);
        foreach (var id in _moverLast.Keys.Except(movers.Select(m => m.Actor.Id)).ToList()) _moverLast.Remove(id);
    }

    // Under half a unit between polls is no heading yet.
    private float? Heading(ActorState mover, TelegraphShape shape)
    {
        if (shape != TelegraphShape.Mover) return mover.Yaw;
        if (!_moverLast.TryGetValue(mover.Id, out var last)) return null;
        float dx = mover.X - last.X, dy = mover.Y - last.Y;
        return dx * dx + dy * dy >= 0.25f ? MathF.Atan2(dy, dx) : null;
    }

    private readonly Dictionary<uint, (float X, float Y)> _moverLast = [];

    /// <summary>A wave warned of is not warned of again for this long (its segments, its sweep).</summary>
    public static readonly TimeSpan MoverRewarn = TimeSpan.FromSeconds(4);

    private readonly Dictionary<uint, TimeSpan> _moverWarned = [];
    private TimeSpan? _lastWave;

    private void Warn(ActorState effect, TelegraphShape shape, PlayerState player, ActorState foe, TimeSpan now)
    {
        var dodge = DodgePlanner.Plan(shape, (effect.X, effect.Y), effect.Yaw, (player.X, player.Y), (foe.X, foe.Y), _blocked);
        _log?.Invoke($"TELEGRAPH {shape} at ({effect.X:F1},{effect.Y:F1}) distance={Distance(effect, player):F1} dodge={dodge?.ToString() ?? "clear"}");
        if (dodge is not { } direction) return;
        var (right, up) = ScreenDirections.ToScreen(effect.X - player.X, effect.Y - player.Y);
        double length = Math.Sqrt(right * right + up * up);
        _playCue(length > 0.01 ? right / length : 0);
        if (_lastSpoken is { } last && now - last < SpeechGap) return;
        _lastSpoken = now;
        string way = NavigationPhrases.For(_language.Current).Direction(direction);
        _speech.Speak(string.Format(AppStrings.For(_language.Current).DodgeFormat, way), interrupt: true);
    }

    // Debug: every damaging effect and projectile new near the player, warned of or not, to learn what a warning missed (a death's
    // last lines name what hit). Projectiles (the guardian's wave wall, tornadoes, chakram) carry an ACD: they are not client effects.
    private void LogHazards(IReadOnlyList<ActorState> actors, PlayerState player)
    {
        foreach (var a in actors)
        {
            if (Distance(a, player) > WarnRangeUnits || !_hazardsSeen.Add(a.Id)) continue;
            string? name = _telegraphs.NameOf(a.ActorSno);
            bool projectile = _isProjectile?.Invoke(a.ActorSno) == true;
            if (!projectile && !(a.ClientOnly && name?.Contains("damaging", StringComparison.OrdinalIgnoreCase) == true)) continue;
            var (right, up) = ScreenDirections.ToScreen(a.X - player.X, a.Y - player.Y);
            _log!($"HAZARD {(projectile ? "projectile" : "effect")} {name ?? a.ActorSno.ToString()} at ({a.X:F1},{a.Y:F1}) yaw={a.Yaw * 180 / MathF.PI:F0} " +
                  $"distance={Distance(a, player):F1} {ScreenDirections.Bin(a.X - player.X, a.Y - player.Y)}");
        }
        _hazardsSeen.IntersectWith(actors.Select(a => a.Id));
    }

    private readonly HashSet<uint> _hazardsSeen = [];

    private static float Distance(ActorState a, PlayerState p) => MathF.Sqrt((a.X - p.X) * (a.X - p.X) + (a.Y - p.Y) * (a.Y - p.Y));
}
