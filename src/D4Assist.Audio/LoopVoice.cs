namespace D4Assist.Audio;

/// <summary>A looping mono voice whose gain, pan, pitch ratio and repeat rate can change while it plays; owned by AudioMixer.</summary>
public sealed class LoopVoice
{
    // Same fade-in / retarget smoothing as the D2R loops (d2r-access src/item_audio.cpp:24, "approximately 80 ms").
    private const double SmoothingSeconds = 0.08;
    // A stopped loop is dropped once its faded gain falls below this (-80 dB), so stopping never clicks.
    private const double SilenceGain = 1e-4;

    /// <summary>Repeat-rate bounds: repetitions per natural cycle of the sound (1 = back to back).</summary>
    public const double MinRate = 0.05, MaxRate = 10;

    // Overlap at MaxRate keeps about MaxRate repetitions sounding at once; one spare absorbs the spawn/drop boundary.
    private const int MaxHeads = (int)MaxRate + 1;

    private static readonly double SmoothingPerFrame = 1 - Math.Exp(-1.0 / (SmoothingSeconds * AudioFormat.SampleRate));

    private readonly float[] _samples;
    private readonly AudioMixer _owner;
    private readonly double[] _heads = new double[MaxHeads]; // read positions of the repetitions still sounding
    private int _headCount = 1; // the first repetition starts immediately, at position 0
    private double _cycle;
    private double _currentGain, _currentPan;

    internal double TargetGain, TargetPan, TargetPitch, TargetRate;
    internal bool Stopping;

    internal LoopVoice(AudioMixer owner, float[] samples, double gain, double pan, double pitch, double rate)
    {
        _owner = owner;
        _samples = samples;
        TargetGain = gain;
        TargetPan = pan;
        TargetPitch = pitch;
        TargetRate = ClampRate(rate);
        _currentPan = pan; // a new source appears at its place and only the gain fades in
    }

    internal static double ClampRate(double rate) => Math.Clamp(rate, MinRate, MaxRate);

    /// <summary>Thread-safe; gain and pan change smoothly. Pan is -1..1, pitch a playback-rate ratio, rate the repetitions per natural cycle.</summary>
    public void Set(double gain, double pan, double pitch, double rate = 1.0) => _owner.UpdateLoop(this, gain, pan, pitch, rate);

    /// <summary>Fades the loop out and removes it from the mixer.</summary>
    public void Stop() => _owner.StopLoop(this);

    internal bool Faded => Stopping && _currentGain < SilenceGain;

    // Adds this loop's next frames into the interleaved stereo mix (caller holds the mixer lock).
    internal void MixInto(float[] mix, int frameCount, double master)
    {
        int length = _samples.Length;
        for (int frame = 0; frame < frameCount; frame++)
        {
            _currentGain += (TargetGain - _currentGain) * SmoothingPerFrame;
            _currentPan += (TargetPan - _currentPan) * SmoothingPerFrame;

            double raw = 0;
            for (int h = 0; h < _headCount; h++)
            {
                int index = (int)_heads[h];
                double fraction = _heads[h] - index;
                float a = _samples[index];
                float b = index + 1 < length ? _samples[index + 1] : 0f; // a one-shot: past the end is silence
                raw += a + (b - a) * fraction;
            }
            double sample = raw * _currentGain * master;

            // Equal-power pan, same law as the cues (d2r-access src/radar_synth.cpp:73).
            mix[frame * AudioFormat.Channels] += (float)(sample * Math.Sqrt((1 - _currentPan) / 2));
            mix[frame * AudioFormat.Channels + 1] += (float)(sample * Math.Sqrt((1 + _currentPan) / 2));

            for (int h = _headCount - 1; h >= 0; h--)
            {
                _heads[h] += TargetPitch;
                if (_heads[h] >= length) _heads[h] = _heads[--_headCount];
            }

            _cycle += TargetPitch * TargetRate;
            if (_cycle >= length)
            {
                _cycle -= length;
                if (_headCount < MaxHeads) _heads[_headCount++] = 0;
            }
        }
    }
}
