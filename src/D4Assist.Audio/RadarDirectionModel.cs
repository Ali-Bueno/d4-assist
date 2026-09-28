namespace D4Assist.Audio;

/// <summary>
/// Maps a ray + frame to stereo pan and a "low pitch" flag, ported verbatim from the D2R radar's ray
/// table (d2r-access src/radar.h:28-55, Axes/ScreenAxes/Rays): pan follows the ray's screen X; a ray
/// whose screen Y points down is pitched lower (RadarCueSynth.DownPitch) so Up and Down stay
/// distinguishable even though both pan center.
/// </summary>
public static class RadarDirectionModel
{
    // Left/right sit just inside the stereo edges, not hard left/right. d2r-access src/radar_synth.cpp:56.
    private const double PanWidth = 0.9;

    // (screenX, screenY) per (frame, ray) — d2r-access src/radar.h:52-55, the Rays table.
    private static readonly Dictionary<(RadarFrame Frame, RadarRay Ray), (int X, int Y)> ScreenVectors = new()
    {
        [(RadarFrame.Cardinal, RadarRay.Up)] = (0, -1),
        [(RadarFrame.Cardinal, RadarRay.Left)] = (-1, 0),
        [(RadarFrame.Cardinal, RadarRay.Right)] = (1, 0),
        [(RadarFrame.Cardinal, RadarRay.Down)] = (0, 1),
        [(RadarFrame.Diagonal, RadarRay.Up)] = (-1, -1),
        [(RadarFrame.Diagonal, RadarRay.Left)] = (1, -1),
        [(RadarFrame.Diagonal, RadarRay.Right)] = (-1, 1),
        [(RadarFrame.Diagonal, RadarRay.Down)] = (1, 1),
    };

    /// <summary>Flat stereo pan in [-1, 1]; never 3D audio (reference/audio-navigation/README.md hard rule).</summary>
    public static double Pan(RadarRay ray, RadarFrame frame) => PanWidth * ScreenVectors[(frame, ray)].X;

    public static bool IsLowPitch(RadarRay ray, RadarFrame frame) => ScreenVectors[(frame, ray)].Y > 0;

    /// <summary>Screen-vertical sign of the ray: -1 up, 0 level, +1 down.</summary>
    public static int Vertical(RadarRay ray, RadarFrame frame) => Math.Sign(ScreenVectors[(frame, ray)].Y);

    /// <summary>The ray's screen vector (x right, y down, components -1/0/+1): the one ray table, for callers
    /// that map rays into their own world axes (D4Assist.Radar.RadarRays).</summary>
    public static (int X, int Y) ScreenVector(RadarRay ray, RadarFrame frame) => ScreenVectors[(frame, ray)];
}
