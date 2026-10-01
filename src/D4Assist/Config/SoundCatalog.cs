using D4Assist.Audio;

namespace D4Assist.Config;

/// <summary>Which group each cue belongs to, which cues repeat (and so have a speed), and the radar's cue per kind and ray.</summary>
public static class SoundCatalog
{
    private static readonly SoundCue[] RadarDirections = [SoundCue.RadarFront, SoundCue.RadarRight, SoundCue.RadarBack, SoundCue.RadarLeft];

    public static SoundGroup GroupOf(SoundCue cue) => cue switch
    {
        <= SoundCue.RadarLeft => SoundGroup.Radar,
        <= SoundCue.PointOfInterest => SoundGroup.Objects,
        <= SoundCue.WardBreaker => SoundGroup.Quest,
        <= SoundCue.GuideArrival => SoundGroup.Guide,
        _ => SoundGroup.Characters,
    };

    /// <summary>The group's cues in settings-window order.</summary>
    public static IReadOnlyList<SoundCue> CuesOf(SoundGroup group) => [.. Enum.GetValues<SoundCue>().Where(c => GroupOf(c) == group)];

    /// <summary>True for the looped and repeating cues (actor loops, guide ping, NPC tap), the ones a speed applies to.</summary>
    public static bool Repeats(SoundCue cue) =>
        GroupOf(cue) is SoundGroup.Objects or SoundGroup.Quest || cue is SoundCue.GuidePing or SoundCue.Npc;

    public static bool Repeats(SoundGroup group) => CuesOf(group).Any(Repeats);

    /// <summary>The radar's four ray volumes, which scale every radar sound coming from that side.</summary>
    public static bool IsRadarDirection(SoundCue cue) => RadarDirections.Contains(cue);

    public static SoundCue RadarCue(RadarCueKind kind) => kind switch
    {
        RadarCueKind.Exit => SoundCue.RadarExit,
        RadarCueKind.Open => SoundCue.RadarOpen,
        RadarCueKind.Blocked => SoundCue.RadarWall,
        RadarCueKind.Door => SoundCue.RadarDoor,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Screen up is "front": D4's camera never turns.</summary>
    public static SoundCue RadarCue(RadarRay ray) => ray switch
    {
        RadarRay.Up => SoundCue.RadarFront,
        RadarRay.Right => SoundCue.RadarRight,
        RadarRay.Down => SoundCue.RadarBack,
        RadarRay.Left => SoundCue.RadarLeft,
        _ => throw new ArgumentOutOfRangeException(nameof(ray)),
    };

    public static RadarRay RayOf(SoundCue direction) => direction switch
    {
        SoundCue.RadarFront => RadarRay.Up,
        SoundCue.RadarRight => RadarRay.Right,
        SoundCue.RadarBack => RadarRay.Down,
        SoundCue.RadarLeft => RadarRay.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };
}
