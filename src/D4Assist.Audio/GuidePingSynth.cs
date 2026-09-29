namespace D4Assist.Audio;

/// <summary>The exploration guide's ping and arrival chime: D2R's beacon notes (620 Hz blips), mono, with the rear pitch baked in.</summary>
public static class GuidePingSynth
{
    // d2r-access src/beacon_audio.cpp:105-109 (PingHz, NoteMs, Attack, Release, Decay, Harmonic, HarmonicLevel, Level).
    public const double PingHz = 620, NoteMs = 80;
    private const double Attack = 0.004, Release = 0.012, Decay = 0.035;
    private const double Harmonic = 2.0, HarmonicLevel = 0.15;

    // beacon_audio.cpp:104,111,117: 60 ms between notes; arrival is the ping rising a fifth, then an octave.
    private const double GapMs = 60, Fifth = 1.5, Octave = 2.0;

    /// <summary>Peak gain before the user's volume (beacon_audio.cpp:109).</summary>
    public const double Level = 0.15;

    /// <param name="pitch">Playback ratio: 1 in front or beside the listener, <see cref="SpatialLoopPlayer.RearPitch"/> behind.</param>
    public static float[] Synthesize(double pitch) => Notes([PingHz], pitch);

    /// <summary>The confirmation played when a guide target is reached (beacon_audio.cpp:117, Sound::Arrived).</summary>
    public static float[] SynthesizeArrived() => Notes([PingHz, PingHz * Fifth, PingHz * Octave], pitch: 1.0);

    private static float[] Notes(double[] notes, double pitch)
    {
        double duration = NoteMs / 1000.0;
        int noteFrames = (int)(duration * AudioFormat.SampleRate);
        int gapFrames = (int)(GapMs / 1000.0 * AudioFormat.SampleRate);
        var samples = new float[notes.Length * noteFrames + (notes.Length - 1) * gapFrames];
        for (int note = 0; note < notes.Length; note++)
        {
            int start = note * (noteFrames + gapFrames);
            double hz = notes[note] * pitch;
            for (int frame = 0; frame < noteFrames; frame++)
            {
                double time = (double)frame / AudioFormat.SampleRate;
                double phase = 2 * Math.PI * hz * time;
                double envelope = Math.Min(1.0, time / Attack) * Math.Min(1.0, (duration - time) / Release) * Math.Exp(-time / Decay);
                samples[start + frame] = (float)(Level * envelope * (Math.Sin(phase) + HarmonicLevel * Math.Sin(phase * Harmonic)));
            }
        }
        return samples;
    }
}
