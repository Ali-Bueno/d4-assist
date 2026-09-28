namespace D4Assist.Audio;

/// <summary>
/// Exact port of the D2R radar's cue recipes (d2r-access src/radar_synth.cpp): bright tin = exit, dull
/// tock = wall, a double knock = door, filtered-noise breath = floor. A marked (unexplored) exit or
/// floor gets a two-pip tail. Deliberate change: the breath sweeps up/down with the ray (see BuildBreath). Produces mono float samples in [-1, 1] — pan is applied later by
/// AudioMixer, per RadarDirectionModel. Pure function of its inputs (a fixed noise seed per cue), so
/// output is deterministic and testable without an audio device.
/// </summary>
public static class RadarCueSynth
{
    // Sound-design values, ported unchanged. d2r-access src/radar_synth.cpp:49-59.
    private const double Duration = 0.120, Attack = 0.003, Release = 0.015;
    private const double ExitHz = 1100, BlockedHz = 260, DoorHz = 180, BreathCutoffHz = 700, DownPitch = 0.86;
    private const double ExitHarmonic = 2.7, BlockedHarmonic = 1.5, DoorHarmonic = 2.0, HarmonicLevel = 0.2;
    private const double ExitDecay = 0.045, BlockedDecay = 0.025, KnockDecay = 0.012, KnockGap = 0.06;
    // D4 change (user feedback 2026-09-28: D2R's breath up/down were indistinguishable): a band-passed
    // noise sweep that rises for up rays, falls for down rays and holds steady for left/right.
    private const double BreathDuration = 0.180, BreathQ = 3.0, BreathPeak = Level * 0.7;
    private const double BreathUpStartHz = 900, BreathUpEndHz = 2400;
    private const double BreathDownStartHz = 600, BreathDownEndHz = 220;
    private const double BreathFlatHz = 1200;
    private const double Level = 0.17;
    private const double MarkHz = 900, MarkDecay = 0.03, MarkHarmonic = 2.0;
    private const double PipDuration = 0.035, UnexploredMarkGapMs = 30, UnexploredPipMs = 35, UnexploredPipGapMs = 30;

    /// <summary>Synthesizes one cue for one direction. <paramref name="vertical"/> comes from
    /// RadarDirectionModel.Vertical (-1 up, 0 level, +1 down); <paramref name="unexplored"/> only has an
    /// audible effect on Open/Exit (RadarCueKindExtensions.IsPassable) — walls and doors ignore it, matching D2R.</summary>
    public static float[] Synthesize(RadarCueKind kind, int vertical, bool unexplored)
    {
        bool lowPitch = vertical > 0;
        var baseCue = kind switch
        {
            RadarCueKind.Exit => BuildTone(ExitHz, ExitHarmonic, ExitDecay, isDoor: false, lowPitch),
            RadarCueKind.Blocked => BuildTone(BlockedHz, BlockedHarmonic, BlockedDecay, isDoor: false, lowPitch),
            RadarCueKind.Door => BuildTone(DoorHz, DoorHarmonic, KnockDecay, isDoor: true, lowPitch),
            RadarCueKind.Open => BuildBreath(vertical),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
        if (!unexplored || !kind.IsPassable()) return baseCue;

        var tail = BuildUnexploredTail(lowPitch);
        var combined = new float[baseCue.Length + tail.Length];
        baseCue.CopyTo(combined, 0);
        tail.CopyTo(combined, baseCue.Length);
        return combined;
    }

    /// <summary>Exit/Blocked/Door: a decaying sine + a quieter harmonic. A door is the same knock struck
    /// twice, KnockGap apart. d2r-access src/radar_synth.cpp:84-99 (else branch).</summary>
    private static float[] BuildTone(double hzBase, double harmonic, double decay, bool isDoor, bool lowPitch)
    {
        double hz = hzBase * (lowPitch ? DownPitch : 1.0);
        int frames = (int)(Duration * AudioFormat.SampleRate);
        var samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            double t = (double)frame / AudioFormat.SampleRate;
            double fade = Math.Min(1.0, (Duration - t) / Release);
            double struck = isDoor && t >= KnockGap ? t - KnockGap : t;
            double phase = 2 * Math.PI * hz * t;
            double envelope = Math.Min(1.0, struck / Attack) * fade * Math.Exp(-struck / decay);
            samples[frame] = (float)(Level * envelope * (Math.Sin(phase) + HarmonicLevel * Math.Sin(phase * harmonic)));
        }
        return samples;
    }

    /// <summary>Open: white noise through a resonant band-pass whose centre sweeps up (up rays), down
    /// (down rays) or holds (left/right), swelled by a half-sine and peak-normalized. Same LCG and swell
    /// as d2r-access src/radar_synth.cpp:80-92; the filter sweep is the D4 change noted above.</summary>
    private static float[] BuildBreath(int vertical)
    {
        var (startHz, endHz) = vertical switch
        {
            < 0 => (BreathUpStartHz, BreathUpEndHz),
            > 0 => (BreathDownStartHz, BreathDownEndHz),
            _ => (BreathFlatHz, BreathFlatHz),
        };
        int frames = (int)(BreathDuration * AudioFormat.SampleRate);
        var samples = new float[frames];
        uint seed = 1;
        double low = 0, band = 0, peak = 0;
        for (int frame = 0; frame < frames; frame++)
        {
            double t = (double)frame / AudioFormat.SampleRate;
            double centreHz = startHz * Math.Pow(endHz / startHz, t / BreathDuration);
            double f = 2 * Math.Sin(Math.PI * centreHz / AudioFormat.SampleRate); // Chamberlin state-variable filter
            seed = seed * 1664525u + 1013904223u; // same LCG constants as d2r-access src/radar_synth.cpp:89
            double noise = (seed >> 8) / 8388608.0 - 1;
            double high = noise - low - band / BreathQ;
            band += f * high;
            low += f * band;

            double fade = Math.Min(1.0, (BreathDuration - t) / Release);
            double swell = Math.Sin(Math.PI * t / BreathDuration);
            double value = swell * swell * fade * band;
            samples[frame] = (float)value;
            peak = Math.Max(peak, Math.Abs(value));
        }
        if (peak > 0)
            for (int frame = 0; frame < frames; frame++) samples[frame] = (float)(samples[frame] * BreathPeak / peak);
        return samples;
    }

    /// <summary>Two ~900 Hz pips after a short gap — the unexplored qualifier. d2r-access
    /// src/radar_synth.cpp:104-118.</summary>
    private static float[] BuildUnexploredTail(bool lowPitch)
    {
        double pitch = lowPitch ? DownPitch : 1.0;
        int sampleRate = AudioFormat.SampleRate;
        int markGapFrames = (int)(UnexploredMarkGapMs * sampleRate / 1000);
        int pipFrames = (int)(UnexploredPipMs * sampleRate / 1000);
        int pipGapFrames = (int)(UnexploredPipGapMs * sampleRate / 1000);
        int markFrames = markGapFrames + 2 * pipFrames + pipGapFrames;
        var samples = new float[markFrames];
        for (int frame = 0; frame < markFrames; frame++)
        {
            if (frame < markGapFrames) continue;
            int pipFrame = (frame - markGapFrames) % (pipFrames + pipGapFrames);
            if (pipFrame >= pipFrames) continue;

            double t = (double)pipFrame / sampleRate;
            double fade = Math.Min(1.0, (PipDuration - t) / Release);
            double phase = 2 * Math.PI * MarkHz * pitch * t;
            double envelope = Math.Min(1.0, t / Attack) * fade * Math.Exp(-t / MarkDecay);
            samples[frame] = (float)(Level * envelope * (Math.Sin(phase) + HarmonicLevel * Math.Sin(phase * MarkHarmonic)));
        }
        return samples;
    }
}
