using D4Assist.Audio;

namespace D4Assist.Radar;

/// <summary>
/// Radar policy values, ported from D2R's Radar::Settings (d2r-access src/radar.h:74-94). One cell is one D4
/// world unit (the offline walk map's resolution); cell counts keep D2R's values as the starting point for live
/// tuning. Sources and D4 values: README.md "Constants".
/// </summary>
public sealed record RadarSettings
{
    /// <summary>radar.h:75; D2R's default since docs/decisions.md 2026-09-25.</summary>
    public RadarFrame Frame { get; init; } = RadarFrame.Cardinal;

    /// <summary>D4 player poll (GameSession.DefaultPlayerPollIntervalMs); D2R polls at 50 ms, radar.h:62.</summary>
    public int PollMs { get; init; } = 100;

    /// <summary>A route this long straight ahead is passable. radar.h:78.</summary>
    public double OpenDepth { get; init; } = 10;

    /// <summary>Lower bound of the closing depth in front of a wall. radar.h:79.</summary>
    public double BlockedDepth { get; init; } = 3;

    /// <summary>How far past the opening depth the route is proved, for the wall memory. radar.h:93.</summary>
    public double ReachDepth { get; init; } = 20;

    /// <summary>Structural neighbourhood and the wall memory's lateral reach. radar.h:77.</summary>
    public double BoundaryRange { get; init; } = 20;

    /// <summary>A jump this long between two polls is a teleport. radar.h:80.</summary>
    public double TeleportDistance { get; init; } = 20;

    /// <summary>A changed reading must persist over this much travel. radar.h:85.</summary>
    public double ConfirmDistance { get; init; } = 1;

    /// <summary>A stationary player confirms after this many polls. radar.h:86 (2 * PollMs).</summary>
    public int StationaryConfirmPolls { get; init; } = 2;

    /// <summary>A longer poll gap discards live evidence. radar.h:81: 250 ms, five D2R polls.</summary>
    public int MaxGapPolls { get; init; } = 5;

    /// <summary>No movement for this long counts as stopped. radar.h:82.</summary>
    public long StoppedMs { get; init; } = 200;

    /// <summary>At most one opening cue per direction in this interval; walls are never delayed. radar.h:87-90.</summary>
    public long CueIntervalMs { get; init; } = 500;

    /// <summary>Body term of the closing depth. D2R's player is always the three-cell cross
    /// (docs/collision-semantics.md:42, radar_passage.cpp:31), which gives the user's familiar 7/10 band.</summary>
    public int BodySpan { get; init; } = 3;

    /// <summary>The raster wobble of a diagonal line, the scan's only lateral slack. radar_passage.cpp:46-48.</summary>
    public double LateralTolerance { get; init; } = 1;

    /// <summary>Largest removable clutter island, one D2 map tile. radar_structure.cpp:11-15.</summary>
    public int ClutterSpan { get; init; } = 5;

    /// <summary>Erase small bypassable clutter (D2R World::slides, radar.h:121-123). Off, as in the user's D2R
    /// setup where wall-slide prevention is on by default (docs/radar-readings.md, precision review).</summary>
    public bool RemoveClutter { get; init; }

    public long StationaryConfirmMs => StationaryConfirmPolls * (long)PollMs;

    public long MaxGapMs => MaxGapPolls * (long)PollMs;

    public int BodyRadius => (BodySpan - 1) / 2;

    /// <summary>Engine::CloseDepth, radar.cpp:18-20: one body span under the opening depth.</summary>
    public double CloseDepth => Math.Max(BlockedDepth, OpenDepth - BodySpan);

    /// <summary>StructuralRadius, radar_structure.cpp:71-74.</summary>
    public int StructuralRadius => (int)Math.Ceiling(BoundaryRange) + ClutterSpan + 2 * BodyRadius + 1;

    /// <summary>The passage window stays inside the structural window, radar.cpp:198-200.</summary>
    public double Reach => Math.Min(Math.Max(OpenDepth, ReachDepth), StructuralRadius - 1 - BodySpan);

    /// <summary>PassageRadius, radar_passage.cpp:32-34.</summary>
    public int PassageRadius => (int)Math.Ceiling(Reach + BodySpan) + 1;

    /// <summary>Engine::WayThrough, radar.cpp:21-27: the dungeon's doorway width, clamped between the body and
    /// the opening depth; the opening depth when no opening is known.</summary>
    public double WayThrough(double typicalOpeningWidth) =>
        typicalOpeningWidth <= 0 ? OpenDepth : Math.Clamp(typicalOpeningWidth, Math.Min(BodySpan, OpenDepth), OpenDepth);
}
