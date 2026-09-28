namespace D4Assist.Audio;

/// <summary>
/// The four navigation-radar rays. Deliberately this library's own enum, not D2R's Direction, so
/// D4Assist.Audio has no dependency on any particular game's direction model — the caller maps its own
/// stick/world directions onto these.
/// </summary>
public enum RadarRay { Up, Right, Down, Left }
