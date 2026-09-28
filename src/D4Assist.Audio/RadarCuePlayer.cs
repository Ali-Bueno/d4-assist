namespace D4Assist.Audio;

/// <summary>
/// Wires the direction model, the deterministic cue synth and the mixer together, and owns the audio
/// output's lifetime. <see cref="Frame"/> and <see cref="Volume"/> are the only runtime knobs (frame
/// toggle, master radar volume — see AppConfig.RadarVolume); everything else is the fixed cue design
/// ported from D2R.
/// </summary>
public sealed class RadarCuePlayer : IRadarCuePlayer, IDisposable
{
    private readonly AudioMixer _mixer;
    private readonly IAudioOut _audioOut;
    private readonly CueScheduler _scheduler = new();
    private readonly Func<double> _nowSeconds;

    public RadarFrame Frame { get; set; } = RadarFrame.Cardinal; // D2R default since 2026-09-25
    public double Volume { get; set; } = 1.0;

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
        var samples = RadarCueSynth.Synthesize(kind, RadarDirectionModel.Vertical(ray, Frame), unexplored);
        var delay = _scheduler.Reserve(_nowSeconds(), (double)samples.Length / AudioFormat.SampleRate);
        if (delay is not { } seconds) return; // too stale to be useful (CueScheduler.MaxStartDelaySeconds)
        _mixer.Play(WithLeadingSilence(samples, seconds), Volume, RadarDirectionModel.Pan(ray, Frame), kind.Priority());
    }

    // Delays a cue inside the mixer by prepending silence, so queued cues keep their slot without a timer thread.
    private static float[] WithLeadingSilence(float[] samples, double seconds)
    {
        int silentFrames = (int)Math.Round(seconds * AudioFormat.SampleRate);
        if (silentFrames == 0) return samples;
        var delayed = new float[silentFrames + samples.Length];
        samples.CopyTo(delayed, silentFrames);
        return delayed;
    }

    public void Dispose()
    {
        _audioOut.Stop();
        _audioOut.Dispose();
    }
}
