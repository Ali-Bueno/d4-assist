namespace D4MemScan.Scan;

/// <summary>
/// Formats a movement delta as an 8-point compass bearing, in engine axes: +X = south, +Y = east (task
/// spec). North is therefore -X, east is +Y; the bearing is measured clockwise from north, as on a compass.
/// </summary>
public static class BearingFormatter
{
    private static readonly string[] Points = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
    private const double DegreesPerPoint = 360.0 / 8;

    // Below this combined delta magnitude (world units), treat the sample as stationary rather than reporting jitter.
    public const float MinMovementUnits = 0.01f;

    /// <summary>Null when the movement is too small to have a meaningful direction.</summary>
    public static string? Format(float deltaX, float deltaY)
    {
        if (MathF.Sqrt(deltaX * deltaX + deltaY * deltaY) < MinMovementUnits) return null;

        double north = -deltaX;
        double east = deltaY;
        double degrees = Math.Atan2(east, north) * (180.0 / Math.PI);
        double normalized = (degrees + 360.0) % 360.0;
        int index = (int)Math.Round(normalized / DegreesPerPoint) % Points.Length;
        return Points[index];
    }
}
