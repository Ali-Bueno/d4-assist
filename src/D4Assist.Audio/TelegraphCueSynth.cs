namespace D4Assist.Audio;

/// <summary>
/// The warning that a boss attack is about to land on the player (research/boss-telegraphs.md): two quick falling notes, a
/// fourth apart, with a hard bright edge, unlike any loop, ping or growl of the app, so it reads as "move" at once.
/// </summary>
public static class TelegraphCueSynth
{
    private const double NoteMs = 85, GapMs = 25;
    private const double HighHz = 1320, LowHz = 990, Level = 0.5;
    private static readonly double[] Harmonics = [1, 0.5, 0.33, 0.25];

    public static float[] Synthesize()
    {
        int note = (int)(NoteMs * AudioFormat.SampleRate / 1000), gap = (int)(GapMs * AudioFormat.SampleRate / 1000);
        var samples = new float[note * 2 + gap];
        Note(samples, 0, note, HighHz);
        Note(samples, note + gap, note, LowHz);
        return samples;
    }

    // A square-ish tone (odd-weighted harmonics) with a 3 ms attack and an exponential fall.
    private static void Note(float[] samples, int start, int length, double hz)
    {
        int attack = (int)(0.003 * AudioFormat.SampleRate);
        for (int i = 0; i < length; i++)
        {
            double t = (double)i / AudioFormat.SampleRate;
            double envelope = (i < attack ? (double)i / attack : 1) * Math.Exp(-t * 18);
            double tone = 0;
            for (int h = 0; h < Harmonics.Length; h++) tone += Harmonics[h] * Math.Sin(2 * Math.PI * hz * (2 * h + 1) * t);
            samples[start + i] = (float)(tone / 2.08 * envelope * Level);
        }
    }
}
