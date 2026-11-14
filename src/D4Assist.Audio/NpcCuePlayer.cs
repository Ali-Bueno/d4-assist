namespace D4Assist.Audio;

/// <summary>One NPC to tap: a stable key, its screen direction and its distance in world units.</summary>
public readonly record struct NpcAim(uint Key, double Right, double Up, double DistanceUnits);

/// <summary>A tap's sound, how fast it repeats at contact and at the edge of its reach, that reach and how many sound at once.</summary>
public sealed record TapSound(Func<double, float[]> Synthesize, double NearIntervalMs, double FarIntervalMs, double RangeUnits, int Voices);

/// <summary>Plays the friendly-NPC tap as a repeating one-shot per NPC: panned and pitched from the NPC's screen direction, faster
/// as it gets closer (d2r-access entity cues). With another <see cref="TapSound"/>, the same for other players.</summary>
public sealed class NpcCuePlayer : IDisposable
{
    /// <summary>The NPCs' wooden tap.</summary>
    public static readonly TapSound Npc = new(NpcCueSynth.Synthesize, NearIntervalMs, FarIntervalMs, DefaultRangeUnits, DefaultVoices);

    /// <summary>Other players (player request 2026-11-13): a soft note well slower than the tap, heard about as far as the screen
    /// shows (twice the tap's reach), one for each of the three others a party of four holds.</summary>
    public static readonly TapSound OtherPlayer = new(PlayerCueSynth.Synthesize, 500, 1200, 2 * DefaultRangeUnits, 3);

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
    private readonly TapSound _sound;
    private readonly Func<double> _nowSeconds;
    private readonly PausingTimer? _ticks;
    private readonly object _gate = new();
    private readonly TapSchedule<NpcAim> _schedule = new(StaleAimSeconds);

    // Floor for Rate so a zero or negative setting can't stall the schedule.
    private const double MinRate = 0.05;

    /// <summary>User volume, 0..1.</summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>Repeat-speed multiplier: the interval between taps is divided by it.</summary>
    public double Rate { get; set; } = 1.0;

    /// <summary>World units beyond which the NPC is silent.</summary>
    public double RangeUnits { get; set; }

    /// <summary>How many of the nearest NPCs the caller aims at once (1 to <see cref="MaxVoices"/>).</summary>
    public int Voices { get; set; }

    /// <param name="autoTick">False in tests, which drive <see cref="Tick"/> against an injected clock.</param>
    /// <param name="sound">What it plays and how fast; the NPCs' tap when null.</param>
    public NpcCuePlayer(AudioMixer mixer, Func<double>? nowSeconds = null, bool autoTick = true, TapSound? sound = null)
    {
        _mixer = mixer;
        _sound = sound ?? Npc;
        (RangeUnits, Voices) = (_sound.RangeUnits, _sound.Voices);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _nowSeconds = nowSeconds ?? (() => clock.Elapsed.TotalSeconds);
        if (autoTick) _ticks = new PausingTimer(Tick, TickMs);
    }

    /// <summary>Seconds between taps at <paramref name="distanceUnits"/>: linear from 150 ms at contact to 450 ms at <paramref name="rangeUnits"/>.</summary>
    public static double IntervalSeconds(double distanceUnits, double rangeUnits = DefaultRangeUnits) =>
        IntervalSeconds(Npc, distanceUnits, rangeUnits);

    /// <summary>Seconds between taps of <paramref name="sound"/> at <paramref name="distanceUnits"/>: linear from its interval at
    /// contact to the one at <paramref name="rangeUnits"/>.</summary>
    public static double IntervalSeconds(TapSound sound, double distanceUnits, double rangeUnits)
    {
        double part = Math.Clamp(distanceUnits / rangeUnits, 0.0, 1.0);
        return Math.Truncate(sound.NearIntervalMs + (sound.FarIntervalMs - sound.NearIntervalMs) * part) / 1000.0;
    }

    /// <summary>Points one tap at each NPC; an NPC left out, or beyond <see cref="RangeUnits"/>, goes silent. Each keeps its own rhythm.</summary>
    public void Aim(IReadOnlyList<NpcAim> npcs)
    {
        lock (_gate)
        {
            _schedule.Aim(npcs.Where(n => n.DistanceUnits <= RangeUnits).Select(n => (n.Key, n)), _nowSeconds());
            if (!_schedule.IsEmpty) _ticks?.Run();
        }
    }

    /// <summary>Stops tapping until the next <see cref="Aim"/>.</summary>
    public void Silence()
    {
        lock (_gate)
        {
            _schedule.Clear();
            _ticks?.Pause();
        }
    }

    /// <summary>Plays the taps that are due; the timer calls this, tests call it directly.</summary>
    public void Tick()
    {
        List<NpcAim> due;
        lock (_gate)
        {
            if (_schedule.IsEmpty)
            {
                _ticks?.Pause();
                return;
            }
            due = _schedule.TakeDue(_nowSeconds(), n => IntervalSeconds(_sound, n.DistanceUnits, RangeUnits) / Math.Max(Rate, MinRate));
        }

        if (Volume <= 0) return; // keeps its schedule, plays nothing
        foreach (var npc in due)
        {
            var (right, up) = UnitDirection(npc.Right, npc.Up);
            _mixer.PlayEntity(_sound.Synthesize(NpcCueSynth.Pitch(up)), Volume * RadarCuePlayer.LoudnessBoost, right);
        }
    }

    /// <summary>A screen direction scaled to unit length (0,0 stays centred), the pan and pitch input of the entity taps.</summary>
    public static (double Right, double Up) UnitDirection(double right, double up)
    {
        double length = Math.Sqrt(right * right + up * up);
        return length > 0 ? (Math.Clamp(right / length, -1.0, 1.0), up / length) : (0, 0);
    }

    public void Dispose() => _ticks?.Dispose();
}
