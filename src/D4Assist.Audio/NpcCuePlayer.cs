namespace D4Assist.Audio;

/// <summary>One NPC to tap: a stable key, its screen direction and its distance in world units.</summary>
public readonly record struct NpcAim(uint Key, double Right, double Up, double DistanceUnits);

/// <summary>Plays the friendly-NPC tap as a repeating one-shot per NPC: panned and pitched from the NPC's screen direction, faster
/// as it gets closer (d2r-access entity cues).</summary>
public sealed class NpcCuePlayer : IDisposable
{
    /// <summary>D2R taps one NPC; the two nearest by default (user, 2026-10-02).</summary>
    public const int DefaultVoices = 2;

    /// <summary>The most NPCs the settings let tap at once: a crowded town holds more talkable NPCs than taps one can tell apart.</summary>
    public const int MaxVoices = 4;

    // d2r-access src/entity_cues.h:14: interval bounds (ms) at contact and at the range edge; entity_cues.cpp:41-43 interpolates.
    private const double NearIntervalMs = 150, FarIntervalMs = 450;

    /// <summary>Default reach: D2R's 15 collision subtiles (EntityRadar Settings::range), taken as 15 D4 units to start.</summary>
    public const double DefaultRangeUnits = 15;

    /// <summary>How often the schedule is checked; well under the shortest interval.</summary>
    public const int TickMs = 25;

    /// <summary>The tap stops by itself when not re-aimed for this long, so it can't outlive its feed.</summary>
    public const double StaleAimSeconds = 1.0;

    private readonly AudioMixer _mixer;
    private readonly Func<double> _nowSeconds;
    private readonly Timer? _timer;
    private readonly object _gate = new();
    private readonly TapSchedule<NpcAim> _schedule = new(StaleAimSeconds);

    // Floor for Rate so a zero or negative setting can't stall the schedule.
    private const double MinRate = 0.05;

    /// <summary>User volume, 0..1.</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>Repeat-speed multiplier: the interval between taps is divided by it.</summary>
    public double Rate { get; set; } = 1.0;

    /// <summary>World units beyond which the NPC is silent.</summary>
    public double RangeUnits { get; set; } = DefaultRangeUnits;

    /// <summary>How many of the nearest NPCs the caller aims at once (1 to <see cref="MaxVoices"/>).</summary>
    public int Voices { get; set; } = DefaultVoices;

    /// <param name="autoTick">False in tests, which drive <see cref="Tick"/> against an injected clock.</param>
    public NpcCuePlayer(AudioMixer mixer, Func<double>? nowSeconds = null, bool autoTick = true)
    {
        _mixer = mixer;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _nowSeconds = nowSeconds ?? (() => clock.Elapsed.TotalSeconds);
        if (autoTick) _timer = new Timer(_ => Tick(), null, TickMs, TickMs);
    }

    /// <summary>Seconds between taps at <paramref name="distanceUnits"/>: linear from 150 ms at contact to 450 ms at <paramref name="rangeUnits"/>.</summary>
    public static double IntervalSeconds(double distanceUnits, double rangeUnits = DefaultRangeUnits)
    {
        double part = Math.Clamp(distanceUnits / rangeUnits, 0.0, 1.0);
        return Math.Truncate(NearIntervalMs + (FarIntervalMs - NearIntervalMs) * part) / 1000.0;
    }

    /// <summary>Points one tap at each NPC; an NPC left out, or beyond <see cref="RangeUnits"/>, goes silent. Each keeps its own rhythm.</summary>
    public void Aim(IReadOnlyList<NpcAim> npcs)
    {
        lock (_gate) _schedule.Aim(npcs.Where(n => n.DistanceUnits <= RangeUnits).Select(n => (n.Key, n)), _nowSeconds());
    }

    /// <summary>Stops tapping until the next <see cref="Aim"/>.</summary>
    public void Silence()
    {
        lock (_gate) _schedule.Clear();
    }

    /// <summary>Plays the taps that are due; the timer calls this, tests call it directly.</summary>
    public void Tick()
    {
        List<NpcAim> due;
        lock (_gate)
        {
            if (_schedule.IsEmpty) return; // every 25 ms, mostly with nobody to tap: allocates nothing then
            due = _schedule.TakeDue(_nowSeconds(), n => IntervalSeconds(n.DistanceUnits, RangeUnits) / Math.Max(Rate, MinRate));
        }

        if (Volume <= 0) return; // keeps its schedule, plays nothing
        foreach (var npc in due)
        {
            var (right, up) = UnitDirection(npc.Right, npc.Up);
            _mixer.PlayEntity(NpcCueSynth.Synthesize(NpcCueSynth.Pitch(up)), Volume * RadarCuePlayer.LoudnessBoost, right);
        }
    }

    /// <summary>A screen direction scaled to unit length (0,0 stays centred), the pan and pitch input of the entity taps.</summary>
    public static (double Right, double Up) UnitDirection(double right, double up)
    {
        double length = Math.Sqrt(right * right + up * up);
        return length > 0 ? (Math.Clamp(right / length, -1.0, 1.0), up / length) : (0, 0);
    }

    public void Dispose() => _timer?.Dispose();
}
