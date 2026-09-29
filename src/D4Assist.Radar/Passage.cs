using D4Assist.Audio;

namespace D4Assist.Radar;

/// <summary>One ray's scan: D2R's Radar::Passage (d2r-access src/radar.h:139-149) without the level-exit
/// field, plus where the route meets unloaded cells.</summary>
public sealed record Passage
{
    /// <summary>Proved connected route along the ray, in cells (up to the scan window).</summary>
    public double Depth { get; init; }

    public double OpenDepth { get; init; }
    public double CloseDepth { get; init; }

    /// <summary>The reading; null only while unavailable.</summary>
    public RadarCueKind? Observed { get; init; }

    /// <summary>False means the player's own cell is unknown (no coverage), not a dead end.</summary>
    public bool Available { get; init; }

    /// <summary>Depth at which the route enters another room, or -1.</summary>
    public double Crossing { get; init; } = -1;

    /// <summary>The room entered at <see cref="Crossing"/>, or -1.</summary>
    public int CrossingRoom { get; init; } = -1;

    /// <summary>Nearest depth at which the route steps into unknown cells, or -1 (D4: leads beyond the loaded tiles).</summary>
    public double UnknownDepth { get; init; } = -1;

    /// <summary>The unexplored qualifier of a passable reading (RadarEngine.Qualify).</summary>
    public bool Unexplored { get; init; }

    /// <summary>A closed door blocks the route within the closing depth (radar_passage.cpp:53).</summary>
    public bool Door { get; init; }

    public bool LeadsUnknown => UnknownDepth >= 0;
}
