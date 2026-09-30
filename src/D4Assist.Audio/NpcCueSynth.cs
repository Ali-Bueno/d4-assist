namespace D4Assist.Audio;

/// <summary>The friendly-NPC cue: D2R's light wooden tap (mono, rear pitch baked in).</summary>
public static class NpcCueSynth
{
    // d2r-access src/entity_cue_synth.cpp:12,18,14-17: length, pitch, decay, texture mix, envelope and partial levels.
    public const double CueMs = 84;
    private const double Hz = 850, DecaySeconds = 0.013, Texture = 0.38;
    private const double Attack = 0.0035, Release = 0.012, TextureCutoffHz = 1800;
    private const double BodyLevel = 0.75, UpperLevel = 0.30, TopLevel = 0.14, UpperRatio = 1.57, TopRatio = 2.31;

    /// <summary>Peak gain before the user's volume (entity_cue_synth.cpp:16).</summary>
    public const double Level = 0.32;

    /// <summary>Pitch of a cue directly behind the listener: a major-third drop (entity_cue_synth.cpp:13).</summary>
    public const double RearPitch = 0.80;

    /// <summary>Playback ratio for a unit screen-space <paramref name="up"/>: linear to <see cref="RearPitch"/> at screen-down (entity_cue_synth.cpp:75).</summary>
    public static double Pitch(double up) => 1 + (RearPitch - 1) * Math.Clamp(-up, 0.0, 1.0);

    /// <param name="pitch">Playback ratio, see <see cref="Pitch"/>.</param>
    public static float[] Synthesize(double pitch)
    {
        int frames = (int)(CueMs * AudioFormat.SampleRate / 1000);
        double duration = CueMs / 1000.0;
        double blend = 1 - Math.Exp(-2 * Math.PI * TextureCutoffHz * pitch / AudioFormat.SampleRate);
        double texture = 0;
        var samples = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            double time = (double)frame / AudioFormat.SampleRate;
            texture += blend * (Noise(frame) - texture);
            double phase = 2 * Math.PI * Hz * pitch * time;
            double body = BodyLevel * Math.Sin(phase) + UpperLevel * Math.Sin(UpperRatio * phase) + TopLevel * Math.Sin(TopRatio * phase);
            double envelope = Math.Clamp(time / Attack, 0.0, 1.0)
                * Math.Clamp((duration - time - 1.0 / AudioFormat.SampleRate) / Release, 0.0, 1.0) * Math.Exp(-time / DecaySeconds);
            samples[frame] = (float)(Level * envelope * (body + Texture * texture));
        }
        return samples;
    }

    // Deterministic hash noise in -1..1 (entity_cue_synth.cpp:62-66).
    private static double Noise(int frame)
    {
        uint hash = (uint)frame * 1664525u + 1013904223u;
        hash ^= hash >> 16;
        return (hash & 0xffffu) / 32767.5 - 1.0;
    }
}
