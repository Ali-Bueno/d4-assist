namespace D4Assist.Audio;

/// <summary>Plays the friendly-NPC tap as a repeating one-shot: panned and pitched from the NPC's screen direction, faster as it gets closer (d2r-access entity cues).</summary>
public sealed class NpcCuePlayer : IDisposable
{
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
    private bool _active;
    private double _right, _up, _distance, _nextDue, _lastAim;

    /// <summary>User volume, 0..1.</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>World units beyond which the NPC is silent.</summary>
    public double RangeUnits { get; set; } = DefaultRangeUnits;

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

    /// <summary>Points the tap at an NPC (screen direction, distance in world units); beyond <see cref="RangeUnits"/> it goes silent.</summary>
    public void Aim(double right, double up, double distanceUnits)
    {
        lock (_gate)
        {
            _active = distanceUnits <= RangeUnits;
            (_right, _up, _distance, _lastAim) = (right, up, distanceUnits, _nowSeconds());
        }
    }

    /// <summary>Stops tapping until the next <see cref="Aim"/>.</summary>
    public void Silence()
    {
        lock (_gate) _active = false;
    }

    /// <summary>Plays the tap when due; the timer calls this, tests call it directly.</summary>
    public void Tick()
    {
        double right, up;
        lock (_gate)
        {
            double now = _nowSeconds();
            if (!_active || now < _nextDue) return;
            if (now - _lastAim > StaleAimSeconds) { _active = false; return; }
            (right, up) = (_right, _up);
            _nextDue = now + IntervalSeconds(_distance, RangeUnits);
        }

        double length = Math.Sqrt(right * right + up * up);
        double unitRight = length > 0 ? right / length : 0, unitUp = length > 0 ? up / length : 0;
        _mixer.PlayEntity(NpcCueSynth.Synthesize(NpcCueSynth.Pitch(unitUp)),
            Volume * RadarCuePlayer.LoudnessBoost, Math.Clamp(unitRight, -1.0, 1.0));
    }

    public void Dispose() => _timer?.Dispose();
}
