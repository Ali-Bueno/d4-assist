namespace D4Assist.Audio;

/// <summary>Fills <paramref name="samples"/> (length == frameCount * AudioFormat.Channels, interleaved
/// PCM16) with the next block of audio. Called on the output's own background thread.</summary>
public delegate void AudioRenderCallback(short[] samples, int frameCount);

/// <summary>
/// A streaming audio output sink. <see cref="Start"/> begins pulling audio from a render callback on a
/// background thread at AudioFormat's rate, so playing a cue (adding a voice upstream) never blocks the
/// caller. Implementations must degrade to silence — never throw — when no output device is present.
/// </summary>
public interface IAudioOut : IDisposable
{
    void Start(AudioRenderCallback render);
    void Stop();
}
