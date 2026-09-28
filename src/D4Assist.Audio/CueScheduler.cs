namespace D4Assist.Audio;

/// <summary>
/// Serializes radar cues the way D2R does: cues never overlap, each one starts <see cref="CueGapSeconds"/>
/// after the previous one ends (d2r-access src/radar.h:64 CueGapMs = 50; radar_synth.cpp:70 inserts the
/// gap between the directions of one poll, radar_audio.cpp:129 waits the same gap before the next poll's
/// cues). Two simultaneous centred cues (up and down) would otherwise blend into one sound.
/// Pure time arithmetic so it is testable without an audio device.
/// </summary>
public sealed class CueScheduler
{
    /// <summary>Silence between consecutive cues. d2r-access src/radar.h:64.</summary>
    public const double CueGapSeconds = 0.050;

    /// <summary>A cue that would start later than this is dropped: a stale spatial reading misleads more
    /// than a missing one. D2R keeps at most one pending batch behind the one playing
    /// (radar_audio.cpp:110-115), i.e. roughly one four-direction batch of audio.</summary>
    public const double MaxStartDelaySeconds = 1.0;

    private double _freeAtSeconds = double.NegativeInfinity;

    /// <summary>Reserves a slot for a cue of <paramref name="durationSeconds"/> requested at
    /// <paramref name="nowSeconds"/>. Returns how long to delay its start, or null to drop it.</summary>
    public double? Reserve(double nowSeconds, double durationSeconds)
    {
        var start = Math.Max(nowSeconds, _freeAtSeconds);
        var delay = start - nowSeconds;
        if (delay > MaxStartDelaySeconds) return null;
        _freeAtSeconds = start + durationSeconds + CueGapSeconds;
        return delay;
    }
}
