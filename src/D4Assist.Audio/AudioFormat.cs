namespace D4Assist.Audio;

/// <summary>
/// PCM format for the whole audio pipeline. 48 kHz stereo matches the D2R radar's own WinMM format
/// exactly (d2r-access src/radar_audio.h:9), so the ported cue timings/frequencies need no rescaling.
/// </summary>
public static class AudioFormat
{
    public const int SampleRate = 48000;
    public const int Channels = 2;
}
