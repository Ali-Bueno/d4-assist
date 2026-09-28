using D4Assist.Audio;

namespace D4Assist.Radar;

/// <summary>
/// Each ray's engine direction: RadarDirectionModel's screen vector (the single ray table, ported from
/// d2r-access src/radar.h:52-55) taken into D4 engine axes (X = south, Y = east) through the measured
/// camera. Cardinal rays therefore step diagonally in the engine grid, Diagonal rays along its axes.
/// </summary>
public static class RadarRays
{
    /// <summary>Screen right in engine axes: stick right moves (-1,+1) (STATUS.md "Screen ↔ world", live 2026-09-28).</summary>
    public static readonly RadarVector ScreenRight = new RadarVector(-1, 1).Normalized();

    /// <summary>Screen down in engine axes: stick down moves (+1,+1) (same measurement).</summary>
    public static readonly RadarVector ScreenDown = new RadarVector(1, 1).Normalized();

    public static IReadOnlyList<RadarRay> All { get; } = Enum.GetValues<RadarRay>();

    private static readonly Dictionary<(RadarFrame Frame, RadarRay Ray), RadarVector> Table = Build();

    /// <summary>The ray's unit direction in engine cells.</summary>
    public static RadarVector World(RadarRay ray, RadarFrame frame) => Table[(frame, ray)];

    private static Dictionary<(RadarFrame, RadarRay), RadarVector> Build()
    {
        var table = new Dictionary<(RadarFrame, RadarRay), RadarVector>();
        foreach (var frame in Enum.GetValues<RadarFrame>())
            foreach (var ray in All)
            {
                var (screenX, screenY) = RadarDirectionModel.ScreenVector(ray, frame);
                table[(frame, ray)] = (screenX * ScreenRight + screenY * ScreenDown).Normalized();
            }
        return table;
    }
}
