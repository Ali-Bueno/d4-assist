namespace D4Assist.Audio;

/// <summary>A looping mono voice whose gain, pan and pitch ratio can change while it plays; owned by AudioMixer.</summary>
public sealed class LoopVoice
{
    // Same fade-in / retarget smoothing as the D2R loops (d2r-access src/item_audio.cpp:24, "approximately 80 ms").
    private const double SmoothingSeconds = 0.08;
    // A stopped loop is dropped once its faded gain falls below this (-80 dB), so stopping never clicks.
    private const double SilenceGain = 1e-4;

    private static readonly double SmoothingPerFrame = 1 - Math.Exp(-1.0 / (SmoothingSeconds * AudioFormat.SampleRate));

    private readonly float[] _samples;
    private readonly AudioMixer _owner;
    private double _position;
    private double _currentGain, _currentPan;

    internal double TargetGain, TargetPan, TargetPitch;
    internal bool Stopping;

    internal LoopVoice(AudioMixer owner, float[] samples, double gain, double pan, double pitch)
    {
        _owner = owner;
        _samples = samples;
        TargetGain = gain;
        TargetPan = pan;
        TargetPitch = pitch;
        _currentPan = pan; // a new source appears at its place and only the gain fades in
    }

    /// <summary>Thread-safe; takes effect smoothly. Pan is -1..1, pitch is a playback-rate ratio.</summary>
    public void Set(double gain, double pan, double pitch) => _owner.UpdateLoop(this, gain, pan, pitch);

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

            int index = (int)_position;
            double fraction = _position - index;
            float a = _samples[index];
            float b = _samples[index + 1 < length ? index + 1 : 0];
            double sample = (a + (b - a) * fraction) * _currentGain * master;

            // Equal-power pan, same law as the cues (d2r-access src/radar_synth.cpp:73).
            mix[frame * AudioFormat.Channels] += (float)(sample * Math.Sqrt((1 - _currentPan) / 2));
            mix[frame * AudioFormat.Channels + 1] += (float)(sample * Math.Sqrt((1 + _currentPan) / 2));

            _position += TargetPitch;
            if (_position >= length) _position -= length;
        }
    }
}
