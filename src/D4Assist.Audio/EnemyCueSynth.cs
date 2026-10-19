namespace D4Assist.Audio;

/// <summary>Which layer an enemy's growl carries: D2R's elite teeth or boss ring (d2r-access docs/entity-cues.md).</summary>
public enum EnemyClass { Elite, Boss }

/// <summary>D2R's enemy cue: a short rough growl every enemy shares, with its class's layer over it (mono, rear pitch baked in).</summary>
public static class EnemyCueSynth
{
    // d2r-access src/entity_cue_synth.cpp:25-28: the growl's length, falling pitch, level, loudness flutter and harmonics.
    public const double CueMs = 90;
    private const double GrowlStartHz = 175, GrowlEndHz = 125, GrowlLevel = 0.36, GrowlFlutterHz = 36, GrowlFlutterDepth = 0.55;
    private const int GrowlHarmonics = 5;

    // entity_cue_synth.cpp:34: every layer is tuned from the growl's middle frequency.
    private const double LayerBaseHz = (GrowlStartHz + GrowlEndHz) / 2;

    // entity_cue_synth.cpp:37-38: elite, a bright buzz chattering like teeth.
    private const double TeethRatio = 5, TeethChatterHz = 62, TeethLevel = 0.28, TeethRolloff = 1, TeethPhaseSpread = 1.3;
    private const int TeethHarmonics = 6;

    // entity_cue_synth.cpp:40-41: boss, a struck bar's inharmonic partials.
    private static readonly double[] RingRatios = [2.76, 5.40, 8.93], RingWeights = [1, 0.55, 0.3];
    private const double RingLevel = 0.08;

    /// <param name="pitch">Playback ratio, see <see cref="NpcCueSynth.Pitch"/>.</param>
    public static float[] Synthesize(EnemyClass kind, double pitch)
    {
        int frames = (int)(CueMs * AudioFormat.SampleRate / 1000);
        var samples = new float[frames];
        double phase = 0;
        for (int frame = 0; frame < frames; frame++)
        {
            double seconds = (double)frame / AudioFormat.SampleRate, progress = (double)frame / (frames - 1);
            samples[frame] = (float)(Growl(ref phase, progress, seconds, pitch) + Layer(kind, progress, seconds, pitch));
        }
        return samples;
    }

    // entity_cue_synth.cpp:118-130.
    private static double Growl(ref double phase, double progress, double seconds, double pitch)
    {
        double hz = (GrowlStartHz + (GrowlEndHz - GrowlStartHz) * progress) * pitch;
        phase += 2 * Math.PI * hz / AudioFormat.SampleRate;
        double body = 0, norm = 0;
        for (int harmonic = 1; harmonic <= GrowlHarmonics; harmonic++)
        {
            body += Math.Sin(harmonic * phase) / harmonic;
            norm += 1.0 / harmonic;
        }
        double flutter = 1 - GrowlFlutterDepth * (0.5 + 0.5 * Math.Sin(2 * Math.PI * GrowlFlutterHz * seconds));
        double swell = Math.Sin(Math.PI * progress);
        return GrowlLevel * swell * swell * flutter * body / norm;
    }

    // entity_cue_synth.cpp:132-147: the layer swells with the growl.
    private static double Layer(EnemyClass kind, double progress, double seconds, double pitch)
    {
        double baseHz = LayerBaseHz * pitch, rise = Math.Sin(Math.PI * progress), swell = rise * rise;
        if (kind == EnemyClass.Boss)
        {
            double ring = 0;
            for (int i = 0; i < RingRatios.Length; i++) ring += RingWeights[i] * Math.Sin(2 * Math.PI * baseHz * RingRatios[i] * seconds);
            return RingLevel * swell * ring;
        }
        double chatter = Math.Max(0.0, Math.Sin(2 * Math.PI * TeethChatterHz * seconds));
        double buzz = 0;
        for (int harmonic = 1; harmonic <= TeethHarmonics; harmonic++)
            buzz += Math.Sin(2 * Math.PI * baseHz * TeethRatio * harmonic * seconds + TeethPhaseSpread * harmonic * harmonic)
                / Math.Pow(harmonic, TeethRolloff);
        return TeethLevel * swell * chatter * chatter * buzz;
    }
}
