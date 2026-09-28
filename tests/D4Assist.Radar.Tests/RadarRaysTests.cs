using D4Assist.Audio;
using D4Assist.Radar;

namespace D4Assist.Radar.Tests;

/// <summary>Pins the ray table against the live stick measurement (STATUS.md "Screen ↔ world": up = (-1,-1),
/// right = (-1,+1), down = (+1,+1), left = (+1,-1) in engine X = south, Y = east).</summary>
public class RadarRaysTests
{
    private static readonly double Half = Math.Sqrt(0.5);

    [Theory]
    [InlineData(RadarRay.Up, -1, -1)]
    [InlineData(RadarRay.Right, -1, 1)]
    [InlineData(RadarRay.Down, 1, 1)]
    [InlineData(RadarRay.Left, 1, -1)]
    public void Cardinal_StepsDiagonallyInTheEngineGrid(RadarRay ray, int x, int y) =>
        AssertVector(new RadarVector(x * Half, y * Half), RadarRays.World(ray, RadarFrame.Cardinal));

    // RadarDirectionModel's diagonal slots: Up = screen up-left, Left = up-right, Right = down-left, Down = down-right.
    [Theory]
    [InlineData(RadarRay.Up, 0, -1)]    // west
    [InlineData(RadarRay.Left, -1, 0)]  // north: tile exit N sits screen up-right
    [InlineData(RadarRay.Right, 1, 0)]  // south
    [InlineData(RadarRay.Down, 0, 1)]   // east
    public void Diagonal_FollowsTheEngineAxes(RadarRay ray, int x, int y) =>
        AssertVector(new RadarVector(x, y), RadarRays.World(ray, RadarFrame.Diagonal));

    [Fact]
    public void EveryRay_ProjectsBackOntoItsOwnPanAndPitch()
    {
        foreach (var frame in Enum.GetValues<RadarFrame>())
            foreach (var ray in RadarRays.All)
            {
                var world = RadarRays.World(ray, frame);
                Assert.Equal(Math.Sign(RadarDirectionModel.Pan(ray, frame)), Math.Sign(Math.Round(world.Dot(RadarRays.ScreenRight), 9)));
                Assert.Equal(RadarDirectionModel.Vertical(ray, frame), Math.Sign(Math.Round(world.Dot(RadarRays.ScreenDown), 9)));
                Assert.Equal(1, world.Length, precision: 9);
            }
    }

    private static void AssertVector(RadarVector expected, RadarVector actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 9);
        Assert.Equal(expected.Y, actual.Y, precision: 9);
    }
}
