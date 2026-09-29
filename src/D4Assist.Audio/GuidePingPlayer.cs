namespace D4Assist.Audio;

/// <summary>
/// Plays the exploration guide's ping as a repeating one-shot on the shared mixer: panned and pitched from the
/// waypoint's screen offset, repeating faster as the remaining route shortens (d2r-access beacon).
/// </summary>
public sealed class GuidePingPlayer : IDisposable
{
    // d2r-access src/beacon.h:39-40: ping interval bounds (ms) and the remaining route (subtiles) where it is slowest.
    private const double NearPingMs = 160, FarPingMs = 1200, FarDistanceSubtiles = 40;
    public const double FarDistanceUnits = FarDistanceSubtiles * D2rScale.D4UnitsPerSubtile;

    /// <summary>How often the schedule is checked; well under the shortest interval so cadence isn't quantized to the poll rate.</summary>
    public const int TickMs = 25;

    /// <summary>The ping stops by itself when not re-aimed for this long (ten 100 ms player polls), so it can never
    /// outlive its feed — e.g. after the game closes (user report, 2026-09-28).</summary>
    public const double StaleAimSeconds = 1.0;

    private readonly AudioMixer _mixer;
    private readonly Func<double> _nowSeconds;
    private readonly Timer? _timer;
    private readonly object _gate = new();
    private bool _active;
    private double _right, _up, _remaining, _nextDue, _lastAim;

    /// <summary>User volume, 0..1.</summary>
    public double Volume { get; set; } = 1.0;

    /// <param name="autoTick">False in tests, which drive <see cref="Tick"/> against an injected clock.</param>
    public GuidePingPlayer(AudioMixer mixer, Func<double>? nowSeconds = null, bool autoTick = true)
    {
        _mixer = mixer;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _nowSeconds = nowSeconds ?? (() => clock.Elapsed.TotalSeconds);
        if (autoTick) _timer = new Timer(_ => Tick(), null, TickMs, TickMs);
    }

    /// <summary>Seconds between pings for a route of <paramref name="remainingUnits"/> world units: linear from 160 to 1200 ms (beacon.cpp:95-100).</summary>
    public static double IntervalSeconds(double remainingUnits)
    {
        double part = Math.Clamp(remainingUnits / FarDistanceUnits, 0.0, 1.0);
        return Math.Round(NearPingMs + part * (FarPingMs - NearPingMs), MidpointRounding.AwayFromZero) / 1000.0;
    }

    /// <summary>Points the ping at a waypoint (screen offset in world units) and keeps it repeating.</summary>
    public void Aim(double right, double up, double remainingUnits)
    {
        lock (_gate) { _active = true; (_right, _up, _remaining, _lastAim) = (right, up, remainingUnits, _nowSeconds()); }
    }

    /// <summary>Stops pinging until the next <see cref="Aim"/>.</summary>
    public void Silence()
    {
        lock (_gate) _active = false;
    }

    /// <summary>Plays the arrival chime once, centred like D2R's (beacon_service.cpp:329), and holds the next ping until it ends.</summary>
    public void PlayArrived()
    {
        var samples = GuidePingSynth.SynthesizeArrived();
        lock (_gate) _nextDue = Math.Max(_nextDue, _nowSeconds() + (double)samples.Length / AudioFormat.SampleRate);
        _mixer.PlayGuide(samples, Volume * RadarCuePlayer.LoudnessBoost, pan: 0);
    }

    /// <summary>Plays one centred ping right away, outside the schedule (the sound examples, Ctrl+Alt+Shift+7).</summary>
    public void PingOnce() => _mixer.PlayGuide(GuidePingSynth.Synthesize(1.0), Volume * RadarCuePlayer.LoudnessBoost, pan: 0);

    /// <summary>Plays the ping when due; the timer calls this, tests call it directly.</summary>
    public void Tick()
    {
        double right, up, remaining;
        lock (_gate)
        {
            double now = _nowSeconds();
            if (!_active || now < _nextDue) return;
            if (now - _lastAim > StaleAimSeconds) { _active = false; return; }
            (right, up, remaining) = (_right, _up, _remaining);
            _nextDue = now + IntervalSeconds(remaining);
        }

        double length = Math.Sqrt(right * right + up * up);
        double unitRight = length > 0 ? right / length : 0, unitUp = length > 0 ? up / length : 0;
        _mixer.PlayGuide(
            GuidePingSynth.Synthesize(SpatialLoopPlayer.Pitch(unitUp)),
            Volume * RadarCuePlayer.LoudnessBoost, SpatialLoopPlayer.Pan(unitRight, unitUp));
    }

    public void Dispose() => _timer?.Dispose();
}
