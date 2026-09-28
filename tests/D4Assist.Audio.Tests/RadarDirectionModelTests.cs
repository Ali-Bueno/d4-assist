namespace D4Assist.Audio.Tests;

/// <summary>Ports the D2R radar's ray table (d2r-access src/radar.h:52-55) — verifies every
/// (frame, ray) pan/low-pitch combination the mixer relies on.</summary>
public class RadarDirectionModelTests
{
    [Theory]
    [InlineData(RadarRay.Up, 0.0, false)]
    [InlineData(RadarRay.Left, -0.9, false)]
    [InlineData(RadarRay.Right, 0.9, false)]
    [InlineData(RadarRay.Down, 0.0, true)]
    public void Cardinal_MatchesD2RScreenAxes(RadarRay ray, double expectedPan, bool expectedLowPitch)
    {
        Assert.Equal(expectedPan, RadarDirectionModel.Pan(ray, RadarFrame.Cardinal), precision: 6);
        Assert.Equal(expectedLowPitch, RadarDirectionModel.IsLowPitch(ray, RadarFrame.Cardinal));
    }

    [Theory]
    [InlineData(RadarRay.Up, -0.9, false)]
    [InlineData(RadarRay.Left, 0.9, false)]
    [InlineData(RadarRay.Right, -0.9, true)]
    [InlineData(RadarRay.Down, 0.9, true)]
    public void Diagonal_MatchesD2RRotatedRays(RadarRay ray, double expectedPan, bool expectedLowPitch)
    {
        Assert.Equal(expectedPan, RadarDirectionModel.Pan(ray, RadarFrame.Diagonal), precision: 6);
        Assert.Equal(expectedLowPitch, RadarDirectionModel.IsLowPitch(ray, RadarFrame.Diagonal));
    }

    [Fact]
    public void UpAndDown_StayDistinguishableInCardinal()
    {
        // Both center-pan; only the low-pitch flag tells them apart (README.md "Direction model").
        Assert.Equal(RadarDirectionModel.Pan(RadarRay.Up, RadarFrame.Cardinal), RadarDirectionModel.Pan(RadarRay.Down, RadarFrame.Cardinal));
        Assert.NotEqual(RadarDirectionModel.IsLowPitch(RadarRay.Up, RadarFrame.Cardinal), RadarDirectionModel.IsLowPitch(RadarRay.Down, RadarFrame.Cardinal));
    }
}
