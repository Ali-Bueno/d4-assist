namespace D4Assist.Audio;

/// <summary>One enemy to cue: a stable key, its class, its screen direction and its distance in world units.</summary>
public readonly record struct EnemyAim(uint Key, EnemyClass Class, double Right, double Up, double DistanceUnits);

/// <summary>Plays D2R's enemy cue as a repeating one-shot per enemy: panned and pitched from its screen direction, faster as it
/// gets closer, and never two starts closer than D2R's gap (d2r-access entity cues).</summary>
public sealed class EnemyCuePlayer : IDisposable
{
    // d2r-access src/entity_cues.h:25: interval bounds (ms) at contact and at the range edge, and the least spacing between any two starts.
    private const double NearIntervalMs = 150, FarIntervalMs = 600, GapMs = 100;

    /// <summary>The cue stops by itself when not re-aimed for this long, so it can't outlive its feed.</summary>
    public const double StaleAimSeconds = 1.0;

    // Floor for a rate so a zero or negative setting can't stall the schedule.
    private const double MinRate = 0.05;

    /// <summary>Reach in world units: the quest loop's, which this cue replaces on the bosses a quest names.</summary>
    public static double RangeUnits => LoopProfiles.Quest.Reach;

    private readonly AudioMixer _mixer;
    private readonly Func<double> _nowSeconds;
    private readonly Timer? _timer;
    private readonly object _gate = new();
    private readonly TapSchedule<EnemyAim> _schedule = new(StaleAimSeconds);
    private readonly Dictionary<EnemyClass, (double Volume, double Rate)> _levels =
        Enum.GetValues<EnemyClass>().ToDictionary(c => c, _ => (1.0, 1.0));
    private double _lastStart = double.NegativeInfinity;

    /// <param name="autoTick">False in tests, which drive <see cref="Tick"/> against an injected clock.</param>
    public EnemyCuePlayer(AudioMixer mixer, Func<double>? nowSeconds = null, bool autoTick = true)
    {
        _mixer = mixer;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _nowSeconds = nowSeconds ?? (() => clock.Elapsed.TotalSeconds);
        if (autoTick) _timer = new Timer(_ => Tick(), null, NpcCuePlayer.TickMs, NpcCuePlayer.TickMs);
    }

    /// <summary>Seconds between cues at <paramref name="distanceUnits"/>: linear from 150 ms at contact to 600 ms at the range edge
    /// (entity_cues.cpp:37-41).</summary>
    public static double IntervalSeconds(double distanceUnits)
    {
        double part = Math.Clamp(distanceUnits / RangeUnits, 0.0, 1.0);
        return Math.Truncate(NearIntervalMs + (FarIntervalMs - NearIntervalMs) * part) / 1000.0;
    }

    /// <summary>User volume (0..1) and repeat-speed multiplier of one class.</summary>
    public void SetLevel(EnemyClass kind, double volume, double rate)
    {
        lock (_gate) _levels[kind] = (volume, rate);
    }

    /// <summary>Points one cue at each enemy; an enemy left out, or beyond <see cref="RangeUnits"/>, goes silent.</summary>
    public void Aim(IReadOnlyList<EnemyAim> enemies)
    {
        lock (_gate) _schedule.Aim(enemies.Where(e => e.DistanceUnits <= RangeUnits).Select(e => (e.Key, e)), _nowSeconds());
    }

    /// <summary>Stops every cue until the next <see cref="Aim"/>.</summary>
    public void Silence()
    {
        lock (_gate) _schedule.Clear();
    }

    /// <summary>Plays the most overdue cue once the gap since the last start has passed; the timer calls this, tests call it directly.</summary>
    public void Tick()
    {
        EnemyAim enemy;
        double volume;
        lock (_gate)
        {
            double now = _nowSeconds();
            if ((now - _lastStart) * 1000 < GapMs) return;
            var due = _schedule.TakeDue(now, e => IntervalSeconds(e.DistanceUnits) / Math.Max(_levels[e.Class].Rate, MinRate), max: 1);
            if (due.Count == 0) return;
            (enemy, _lastStart, volume) = (due[0], now, _levels[due[0].Class].Volume);
        }

        if (volume <= 0) return; // keeps its schedule, plays nothing
        var (right, up) = NpcCuePlayer.UnitDirection(enemy.Right, enemy.Up);
        _mixer.PlayEntity(EnemyCueSynth.Synthesize(enemy.Class, NpcCueSynth.Pitch(up)), volume * RadarCuePlayer.LoudnessBoost, right);
    }

    public void Dispose() => _timer?.Dispose();
}
