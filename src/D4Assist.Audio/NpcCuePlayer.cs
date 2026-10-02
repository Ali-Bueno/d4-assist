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
    private readonly Dictionary<uint, Tap> _taps = [];

    private sealed class Tap
    {
        public double Right, Up, Distance, NextDue, LastAim;
    }

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
        lock (_gate)
        {
            double now = _nowSeconds();
            var inRange = npcs.Where(n => n.DistanceUnits <= RangeUnits).ToList();
            foreach (uint gone in _taps.Keys.Except(inRange.Select(n => n.Key)).ToList()) _taps.Remove(gone);
            foreach (var npc in inRange)
            {
                var tap = _taps.TryGetValue(npc.Key, out var known) ? known : _taps[npc.Key] = new Tap();
                (tap.Right, tap.Up, tap.Distance, tap.LastAim) = (npc.Right, npc.Up, npc.DistanceUnits, now);
            }
        }
    }

    /// <summary>Stops tapping until the next <see cref="Aim"/>.</summary>
    public void Silence()
    {
        lock (_gate) _taps.Clear();
    }

    /// <summary>Plays the taps that are due; the timer calls this, tests call it directly.</summary>
    public void Tick()
    {
        var due = new List<(double Right, double Up)>();
        lock (_gate)
        {
            double now = _nowSeconds();
            foreach (var (key, tap) in _taps.ToList())
            {
                if (now - tap.LastAim > StaleAimSeconds) { _taps.Remove(key); continue; }
                if (now < tap.NextDue) continue;
                due.Add((tap.Right, tap.Up));
                tap.NextDue = now + IntervalSeconds(tap.Distance, RangeUnits) / Math.Max(Rate, MinRate);
            }
        }

        if (Volume <= 0) return; // keeps its schedule, plays nothing
        foreach (var (right, up) in due) Play(right, up);
    }

    private void Play(double right, double up)
    {
        double length = Math.Sqrt(right * right + up * up);
        double unitRight = length > 0 ? right / length : 0, unitUp = length > 0 ? up / length : 0;
        _mixer.PlayEntity(NpcCueSynth.Synthesize(NpcCueSynth.Pitch(unitUp)),
            Volume * RadarCuePlayer.LoudnessBoost, Math.Clamp(unitRight, -1.0, 1.0));
    }

    public void Dispose() => _timer?.Dispose();
}
