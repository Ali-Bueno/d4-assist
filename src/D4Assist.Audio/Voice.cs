namespace D4Assist.Audio;

/// <summary>One in-flight mono cue: fixed gain/pan/priority for its lifetime, advanced sample-by-sample
/// by AudioMixer.Render. A delayed one plays <paramref name="delayFrames"/> of silence first, holding its slot.</summary>
internal sealed class Voice(float[] samples, double gain, double pan, int priority, long startOrder, int delayFrames = 0)
{
    private int _position;
    private int _delay = delayFrames;

    public double Gain { get; } = gain;
    public double Pan { get; } = pan;
    public int Priority { get; } = priority;
    public long StartOrder { get; } = startOrder;

    public bool Finished => _position >= samples.Length;

    public float NextSample()
    {
        if (_delay > 0) { _delay--; return 0f; }
        return _position < samples.Length ? samples[_position++] : 0f;
    }
}
