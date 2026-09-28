namespace D4Assist.Audio;

/// <summary>One in-flight mono cue: fixed gain/pan/priority for its lifetime, advanced sample-by-sample
/// by AudioMixer.Render.</summary>
internal sealed class Voice(float[] samples, double gain, double pan, int priority, long startOrder)
{
    private int _position;

    public double Gain { get; } = gain;
    public double Pan { get; } = pan;
    public int Priority { get; } = priority;
    public long StartOrder { get; } = startOrder;

    public bool Finished => _position >= samples.Length;

    public float NextSample() => _position < samples.Length ? samples[_position++] : 0f;
}
