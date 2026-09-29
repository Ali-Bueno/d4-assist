namespace D4Assist.Audio;

/// <summary>
/// Mixes concurrent mono voices into interleaved stereo PCM16: per-voice gain and flat pan (never 3D —
/// reference/audio-navigation/README.md's hard rule), a master volume, and a soft limiter against clipping.
/// The D2R radar itself plays strictly one cue at a time through a priority queue (d2r-access
/// src/radar_audio.cpp CueQueue, urgency-ordered); this mixer generalizes that to real polyphony so
/// overlapping directions are never dropped, while keeping the same urgency idea for voice stealing once
/// the mixer is full.
/// </summary>
public sealed class AudioMixer
{
    public const int MaxVoices = 8; // headroom over D2R's 4 simultaneous directions

    /// <summary>Separate budget for looping sources, so loops and radar cues never steal from each other.
    /// D2R's ground-item mix holds 8 loop voices (d2r-access src/ground_items.h:44).</summary>
    public const int MaxLoopVoices = 8;

    /// <summary>Separate budget for guide pings (one plays at a time; the spare covers a ping still ringing
    /// when the next starts), so radar cues can neither steal nor starve them.</summary>
    public const int MaxGuideVoices = 2;

    // A single boosted cue peaks below this, so only overlapping cues are ever compressed.
    private const float LimiterKnee = 0.8f;

    public double MasterVolume { get; set; } = 1.0;

    /// <summary>While true every cue is silent (e.g. a cutscene): one-shot cues are dropped, loops hold their place.</summary>
    public bool Paused { get => _paused; set => _paused = value; }
    private volatile bool _paused;

    /// <summary>Currently mixing voice count, for diagnostics/tests (never opens a device).</summary>
    public int ActiveVoiceCount { get { lock (_lock) return _voices.Count; } }

    private readonly List<Voice> _voices = [];
    private readonly List<Voice> _guideVoices = [];
    private readonly List<LoopVoice> _loops = [];
    private readonly object _lock = new();
    private long _nextStartOrder;

    /// <summary><paramref name="samples"/> is mono, already cue-shaped by RadarCueSynth.
    /// <paramref name="pan"/> is -1..1. <paramref name="priority"/> follows RadarCueKindExtensions.Priority
    /// and decides which voice is stolen first when the mixer is already at MaxVoices.</summary>
    public void Play(float[] samples, double gain, double pan, int priority = 0)
    {
        if (_paused) return;
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

    /// <summary>Currently mixing guide-ping count, for diagnostics/tests.</summary>
    public int ActiveGuideVoiceCount { get { lock (_lock) return _guideVoices.Count; } }

    /// <summary>Plays a guide ping on its own voice budget; when both slots are busy the older ping is replaced.</summary>
    public void PlayGuide(float[] samples, double gain, double pan)
    {
        if (_paused) return;
        var voice = new Voice(samples, gain, Math.Clamp(pan, -1.0, 1.0), priority: 0, _nextStartOrder++);
        lock (_lock)
        {
            if (_guideVoices.Count >= MaxGuideVoices) _guideVoices.RemoveAt(0);
            _guideVoices.Add(voice);
        }
    }

    /// <summary>Currently mixing loop count (fading-out loops included), for diagnostics/tests.</summary>
    public int ActiveLoopCount { get { lock (_lock) return _loops.Count; } }

    /// <summary>Starts a looping voice, or returns null when the loop budget is full or the sound is empty.</summary>
    public LoopVoice? StartLoop(float[] samples, double gain, double pan, double pitch = 1.0)
    {
        if (samples.Length == 0) return null;
        var loop = new LoopVoice(this, samples, Math.Max(0, gain), Math.Clamp(pan, -1.0, 1.0), Math.Max(0.01, pitch));
        lock (_lock)
        {
            if (_loops.Count >= MaxLoopVoices) return null;
            _loops.Add(loop);
        }
        return loop;
    }

    internal void UpdateLoop(LoopVoice loop, double gain, double pan, double pitch)
    {
        lock (_lock)
        {
            if (loop.Stopping) return;
            loop.TargetGain = Math.Max(0, gain);
            loop.TargetPan = Math.Clamp(pan, -1.0, 1.0);
            loop.TargetPitch = Math.Max(0.01, pitch);
        }
    }

    internal void StopLoop(LoopVoice loop)
    {
        lock (_lock)
        {
            loop.Stopping = true;
            loop.TargetGain = 0;
        }
    }

    /// <summary>IAudioOut render callback: fills <paramref name="buffer"/> (frameCount * Channels
    /// interleaved PCM16 samples) from the currently active voices, removing any that finish.</summary>
    public void Render(short[] buffer, int frameCount)
    {
        int sampleCount = frameCount * AudioFormat.Channels;
        if (_paused)
        {
            Array.Clear(buffer, 0, sampleCount);
            lock (_lock) { _voices.Clear(); _guideVoices.Clear(); _loops.RemoveAll(l => l.Stopping); }
            return;
        }
        var mix = new float[sampleCount];

        lock (_lock)
        {
            MixVoices(_voices, mix, frameCount);
            MixVoices(_guideVoices, mix, frameCount);

            for (int i = _loops.Count - 1; i >= 0; i--)
            {
                _loops[i].MixInto(mix, frameCount, MasterVolume);
                if (_loops[i].Faded) _loops.RemoveAt(i);
            }
        }

        for (int i = 0; i < sampleCount; i++)
            buffer[i] = (short)(SoftLimit(mix[i]) * short.MaxValue);
    }

    // Caller holds the lock; removes voices that finish.
    private void MixVoices(List<Voice> voices, float[] mix, int frameCount)
    {
        for (int i = voices.Count - 1; i >= 0; i--)
        {
            var voice = voices[i];
            // Equal-power pan law, same as the D2R radar's own cues. d2r-access src/radar_synth.cpp:73.
            double left = Math.Sqrt((1 - voice.Pan) / 2) * voice.Gain * MasterVolume;
            double right = Math.Sqrt((1 + voice.Pan) / 2) * voice.Gain * MasterVolume;
            for (int frame = 0; frame < frameCount && !voice.Finished; frame++)
            {
                float sample = voice.NextSample();
                mix[frame * AudioFormat.Channels] += (float)(sample * left);
                mix[frame * AudioFormat.Channels + 1] += (float)(sample * right);
            }
            if (voice.Finished) voices.RemoveAt(i);
        }
    }

    /// <summary>Linear up to <see cref="LimiterKnee"/>, then eases towards full scale instead of hard clipping,
    /// so the boosted radar (RadarCuePlayer.LoudnessBoost) stays clean when several cues overlap.</summary>
    public static float SoftLimit(float sample)
    {
        float magnitude = Math.Abs(sample);
        if (magnitude <= LimiterKnee) return sample;
        double headroom = 1.0 - LimiterKnee;
        double limited = LimiterKnee + headroom * Math.Tanh((magnitude - LimiterKnee) / headroom);
        return (float)Math.Min(1.0, limited) * Math.Sign(sample);
    }
}
