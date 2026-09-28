namespace D4Assist.Audio;

/// <summary>
/// Mixes concurrent mono voices into interleaved stereo PCM16: per-voice gain and flat pan (never 3D —
/// reference/audio-navigation/README.md's hard rule), a master volume, and hard clipping protection.
/// The D2R radar itself plays strictly one cue at a time through a priority queue (d2r-access
/// src/radar_audio.cpp CueQueue, urgency-ordered); this mixer generalizes that to real polyphony so
/// overlapping directions are never dropped, while keeping the same urgency idea for voice stealing once
/// the mixer is full.
/// </summary>
public sealed class AudioMixer
{
    public const int MaxVoices = 8; // headroom over D2R's 4 simultaneous directions

    public double MasterVolume { get; set; } = 1.0;

    /// <summary>Currently mixing voice count, for diagnostics/tests (never opens a device).</summary>
    public int ActiveVoiceCount { get { lock (_lock) return _voices.Count; } }

    private readonly List<Voice> _voices = [];
    private readonly object _lock = new();
    private long _nextStartOrder;

    /// <summary><paramref name="samples"/> is mono, already cue-shaped by RadarCueSynth.
    /// <paramref name="pan"/> is -1..1. <paramref name="priority"/> follows RadarCueKindExtensions.Priority
    /// and decides which voice is stolen first when the mixer is already at MaxVoices.</summary>
    public void Play(float[] samples, double gain, double pan, int priority = 0)
    {
        var voice = new Voice(samples, gain, Math.Clamp(pan, -1.0, 1.0), priority, _nextStartOrder++);
        lock (_lock)
        {
            if (_voices.Count >= MaxVoices)
            {
                var weakest = _voices[0];
                foreach (var candidate in _voices)
                    if (candidate.Priority < weakest.Priority
                        || (candidate.Priority == weakest.Priority && candidate.StartOrder < weakest.StartOrder))
                        weakest = candidate;
                _voices.Remove(weakest);
            }
            _voices.Add(voice);
        }
    }

    /// <summary>IAudioOut render callback: fills <paramref name="buffer"/> (frameCount * Channels
    /// interleaved PCM16 samples) from the currently active voices, removing any that finish.</summary>
    public void Render(short[] buffer, int frameCount)
    {
        int sampleCount = frameCount * AudioFormat.Channels;
        var mix = new float[sampleCount];

        lock (_lock)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                var voice = _voices[i];
                // Equal-power pan law, same as the D2R radar's own cues. d2r-access src/radar_synth.cpp:73.
                double left = Math.Sqrt((1 - voice.Pan) / 2) * voice.Gain * MasterVolume;
                double right = Math.Sqrt((1 + voice.Pan) / 2) * voice.Gain * MasterVolume;
                for (int frame = 0; frame < frameCount && !voice.Finished; frame++)
                {
                    float sample = voice.NextSample();
                    mix[frame * AudioFormat.Channels] += (float)(sample * left);
                    mix[frame * AudioFormat.Channels + 1] += (float)(sample * right);
                }
                if (voice.Finished) _voices.RemoveAt(i);
            }
        }

        for (int i = 0; i < sampleCount; i++)
        {
            // Hard clip, the same safety net as the D2R radar's own cues. d2r-access src/radar_synth.cpp:100-101.
            float clipped = Math.Clamp(mix[i], -1f, 1f);
            buffer[i] = (short)(clipped * short.MaxValue);
        }
    }
}
