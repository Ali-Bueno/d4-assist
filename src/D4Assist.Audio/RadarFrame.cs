namespace D4Assist.Audio;

/// <summary>
/// Which four rays drive the radar. Cardinal is the stick's own up/left/right/down; Diagonal is the
/// stick's diagonals, used when a game's walls run at 45 degrees to the stick (d2r-access
/// src/radar.h:41-46). Cardinal has been D2R's default since 2026-09-25 (research/d2r-radar-port.md §1).
/// </summary>
public enum RadarFrame { Cardinal, Diagonal }
