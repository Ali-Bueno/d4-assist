namespace D4Assist.Audio;

/// <summary>The cue for another player nearby (player request 2026-11-13: a soft sound, slower than the NPCs' tap): a round,
/// quiet note with a slow onset, an octave below the tap (mono, pitched like it by direction).</summary>
public static class PlayerCueSynth
{
    // Twice the tap's length and half its pitch (NpcCueSynth: 84 ms, 850 Hz), no noise texture and a gentle onset.
    public const double CueMs = 2 * NpcCueSynth.CueMs;
    private const double Hz = 425, OctaveLevel = 0.12;
    private const double Attack = 0.020, Release = 0.030, DecaySeconds = 0.060;

    /// <summary>Peak gain before the user's volume: under the tap's (NpcCueSynth.Level).</summary>
    public const double Level = 0.22;

    /// <param name="pitch">Playback ratio, see <see cref="NpcCueSynth.Pitch"/>.</param>
    public static float[] Synthesize(double pitch)
    {
        int frames = (int)(CueMs * AudioFormat.SampleRate / 1000);
        double duration = CueMs / 1000.0;
        var samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            double time = (double)frame / AudioFormat.SampleRate;
            double phase = 2 * Math.PI * Hz * pitch * time;
            double envelope = Math.Clamp(time / Attack, 0.0, 1.0)
                * Math.Clamp((duration - time - 1.0 / AudioFormat.SampleRate) / Release, 0.0, 1.0) * Math.Exp(-time / DecaySeconds);
            samples[frame] = (float)(Level * envelope * (Math.Sin(phase) + OctaveLevel * Math.Sin(2 * phase)));
        }
        return samples;
    }
}
