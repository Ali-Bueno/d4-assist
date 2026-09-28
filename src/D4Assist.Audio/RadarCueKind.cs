namespace D4Assist.Audio;

/// <summary>
/// The radar sensor readings this mod turns into sound — the D2R radar's State enum minus Unknown
/// (d2r-access src/radar.h:15); "no event" is simply not calling IRadarCuePlayer.Play.
/// </summary>
public enum RadarCueKind
{
    /// <summary>Floor that leads nowhere new — soft, low-passed-noise "breath".</summary>
    Open,

    /// <summary>A wall within the closing depth — dull "tock", ~260 Hz.</summary>
    Blocked,

    /// <summary>A route into another chamber or a level exit — bright "tin", ~1100 Hz.</summary>
    Exit,

    /// <summary>A closed door — two knocks, ~180 Hz.</summary>
    Door,
}

/// <summary>Small per-kind facts the mixer and synth need, kept off the enum itself.</summary>
public static class RadarCueKindExtensions
{
    /// <summary>Voice-stealing priority when the mixer is full, mirroring the D2R radar's own cue
    /// urgency (d2r-access src/radar_audio.cpp:25, CueQueue::Take): an obstacle outranks an exit, which
    /// outranks background floor.</summary>
    public static int Priority(this RadarCueKind kind) => kind switch
    {
        RadarCueKind.Blocked or RadarCueKind.Door => 2,
        RadarCueKind.Exit => 1,
        _ => 0,
    };

    /// <summary>Whether this reading may carry the unexplored tail — walls and doors never do
    /// (d2r-access src/radar.h:16, Passable).</summary>
    public static bool IsPassable(this RadarCueKind kind) => kind is RadarCueKind.Open or RadarCueKind.Exit;
}
