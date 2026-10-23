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

    public TelegraphShape? Of(int actorSno) => _shapes.GetOrAdd(actorSno, sno => Classify(nameOf(sno)));

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
    [GeneratedRegex("^(Warlock|Rogue|Sorc|Necro|Barb|Druid|Duid|Paladin|Spiritborn|npc|merc|PvP|emote|ui_)|_Sign$|Town|Mirage|RitualFx",
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
    private readonly Func<TimeSpan> _now;
    private readonly object _gate = new();
    private readonly HashSet<uint> _seen = [];
    private readonly List<(float X, float Y, TimeSpan At)> _recent = [];
    private PlayerState? _player;
    private TimeSpan? _lastSpoken;

    /// <param name="enemyClass">An actor's enemy class, null for none (EnemyClassifier.Of): a boss or elite near makes a fight.</param>
    /// <param name="playCue">Plays the warning cue at a pan (-1 left .. 1 right).</param>
    /// <param name="blocked">True for a world point a step can't cross (the radar's walls and closed doors); null for none.</param>
    public TelegraphWarner(
        IDungeonFeed feed, TelegraphClassifier telegraphs, Func<ActorState, EnemyClass?> enemyClass, ISpeechSink speech,
        LanguageSetting language, Action<double> playCue, Func<float, float, bool>? blocked = null, Action<string>? log = null,
        Func<TimeSpan>? now = null)
    {
        (_telegraphs, _enemyClass, _speech, _language, _playCue, _blocked, _log) = (telegraphs, enemyClass, speech, language, playCue, blocked, log);
        var clock = Stopwatch.StartNew();
        _now = now ?? (() => clock.Elapsed);
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _seen.Clear(); _recent.Clear(); _player = null; } };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) _player = p; };
        feed.ActorsUpdated += actors => { lock (_gate) OnActors(actors); };
    }

    private void OnActors(IReadOnlyList<ActorState> actors)
    {
        var effects = actors.Where(a => a.ClientOnly).ToList();
        var fresh = effects.Where(a => !_seen.Contains(a.Id)).ToList();
        _seen.IntersectWith(effects.Select(a => a.Id));
        _seen.UnionWith(fresh.Select(a => a.Id));
        if (_player is not { } player) return;
        var foe = actors.Where(a => !a.ClientOnly && _enemyClass(a) is not null && Distance(a, player) <= FightRangeUnits)
            .OrderBy(a => Distance(a, player)).FirstOrDefault();
        if (foe is null) return; // no fight: an effect named "warning" is someone else's business
        var now = _now();
        _recent.RemoveAll(r => now - r.At > SameWarningWindow);
        foreach (var effect in fresh)
        {
            if (_telegraphs.Of(effect.ActorSno) is not { } shape || Distance(effect, player) > WarnRangeUnits) continue;
            if (_recent.Any(r => MathF.Abs(r.X - effect.X) + MathF.Abs(r.Y - effect.Y) <= SameWarningUnits)) continue;
            _recent.Add((effect.X, effect.Y, now));
            Warn(effect, shape, player, foe, now);
        }
    }

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

    private static float Distance(ActorState a, PlayerState p) => MathF.Sqrt((a.X - p.X) * (a.X - p.X) + (a.Y - p.Y) * (a.Y - p.Y));
}
