namespace D4Assist.Radar;

/// <summary>A 2D vector in radar cells (world units), D2R's Radar::Position (d2r-access src/radar.h:11).</summary>
public readonly record struct RadarVector(double X, double Y)
{
    public double Length => Math.Sqrt(X * X + Y * Y);

    public double Dot(RadarVector other) => X * other.X + Y * other.Y;

    /// <summary>Signed offset of <paramref name="point"/> across this axis (radar_passage.cpp:41, `lateral`).</summary>
    public double Across(RadarVector point) => -point.X * Y + point.Y * X;

    public double DistanceTo(RadarVector other) => (this - other).Length;

    public RadarVector Normalized() => new(X / Length, Y / Length);

    public static RadarVector operator +(RadarVector a, RadarVector b) => new(a.X + b.X, a.Y + b.Y);
    public static RadarVector operator -(RadarVector a, RadarVector b) => new(a.X - b.X, a.Y - b.Y);
    public static RadarVector operator *(double scale, RadarVector v) => new(scale * v.X, scale * v.Y);
}
