using System.Collections.Concurrent;

namespace D4Assist.Audio;

/// <summary>
/// Wires the direction model, the deterministic cue synth and the mixer together, and owns the audio
/// output's lifetime. <see cref="Frame"/> and <see cref="GainOf"/> are the only runtime knobs (frame
/// toggle, per-cue user gain); everything else is the fixed cue design
/// ported from D2R.
/// </summary>
public sealed class RadarCuePlayer : IRadarCuePlayer, IDisposable
{
    private readonly AudioMixer _mixer;
    private readonly IAudioOut _audioOut;
    private readonly CueScheduler _scheduler = new();
    private readonly Func<double> _nowSeconds;

    /// <summary>Gain over D2R's cue level (+12 dB): the user found D2R's level drowned out by D4 combat (2026-09-28).
    /// The loudest cue (~0.2 peak) stays under the mixer's limiter knee.</summary>
    public const double LoudnessBoost = 4.0;

    public RadarFrame Frame { get; set; } = RadarFrame.Cardinal; // D2R default since 2026-09-25

    /// <summary>User gain per cue kind and ray, 0 (muted) to 1 (full = <see cref="LoudnessBoost"/> over D2R's level).</summary>
    public Func<RadarCueKind, RadarRay, double> GainOf { get; set; } = (_, _) => 1.0;

    /// <param name="nowSeconds">Clock for cue scheduling; tests inject one, the app uses a Stopwatch.</param>
    public RadarCuePlayer(IAudioOut audioOut, AudioMixer? mixer = null, Func<double>? nowSeconds = null)
    {
        _audioOut = audioOut;
        _mixer = mixer ?? new AudioMixer();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        _nowSeconds = nowSeconds ?? (() => clock.Elapsed.TotalSeconds);
        _audioOut.Start(_mixer.Render);
    }

    public void Play(RadarCueKind kind, RadarRay ray, bool unexplored)
    {
        double gain = GainOf(kind, ray) * LoudnessBoost;
        if (gain <= 0) return; // muted: must not hold a scheduler slot
        var samples = Cue(kind, RadarDirectionModel.Vertical(ray, Frame), unexplored);
        var delay = _scheduler.Reserve(_nowSeconds(), (double)samples.Length / AudioFormat.SampleRate);
        if (delay is not { } seconds) return; // too stale to be useful (CueScheduler.MaxStartDelaySeconds)
        // Delayed inside the mixer, so queued cues keep their slot without a timer thread.
        _mixer.Play(samples, gain, RadarDirectionModel.Pan(ray, Frame), kind.Priority(), delaySeconds: seconds);
    }

    // The synth is a pure function of these three (a fixed noise seed), and a batch plays up to four cues every few hundred
    // milliseconds: each cue is synthesized once. The mixer only reads the samples.
    private static readonly ConcurrentDictionary<(RadarCueKind Kind, int Vertical, bool Unexplored), float[]> Cues = new();

    private static float[] Cue(RadarCueKind kind, int vertical, bool unexplored) =>
        Cues.GetOrAdd((kind, vertical, unexplored), key => RadarCueSynth.Synthesize(key.Kind, key.Vertical, key.Unexplored));

    public void Dispose()
    {
        _audioOut.Stop();
        _audioOut.Dispose();
    }
}
