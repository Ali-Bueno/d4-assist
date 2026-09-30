namespace D4Assist.Audio;

/// <summary>Builds a preview of a repeating cue: copies of one sound overlap-added at a fixed period.</summary>
public static class CueRepeat
{
    /// <summary>Mono buffer with <paramref name="count"/> copies of <paramref name="sound"/>, one starting every <paramref name="periodSeconds"/>.</summary>
    public static float[] Render(float[] sound, double periodSeconds, int count)
    {
        if (count <= 0 || sound.Length == 0) return [];
        int period = (int)Math.Round(Math.Max(0, periodSeconds) * AudioFormat.SampleRate);
        var output = new float[(count - 1) * period + sound.Length];
        for (int copy = 0; copy < count; copy++)
            for (int i = 0; i < sound.Length; i++)
                output[copy * period + i] += sound[i];
        return output;
    }
}
