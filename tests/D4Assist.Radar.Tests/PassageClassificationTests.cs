using D4Assist.Audio;
using D4Assist.Radar;

namespace D4Assist.Radar.Tests;

/// <summary>What one ray reads on synthetic grids (PassageScanner + RadarEngine.Qualify), taken from the silent
/// baseline of a fresh engine. Diagonal frame, so the tested ray runs straight along +X.</summary>
public class PassageClassificationTests
{
    private const RadarRay South = RadarRay.Right; // Diagonal frame: (+1, 0), pinned in RadarRaysTests

    [Fact]
    public void Corridor_ReadsOpen()
    {
        var sensor = Baseline(FakeRadarWorld.Corridor(), 2);

        Assert.Equal(RadarCueKind.Open, sensor.State);
        Assert.False(sensor.Unexplored);
    }

    [Fact]
    public void CloseWall_ReadsBlocked()
    {
        var sensor = Baseline(FakeRadarWorld.Corridor(wallX: 30), 25);

        Assert.Equal(RadarCueKind.Blocked, sensor.State);
        Assert.Equal(4, sensor.Passage.Depth);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CrossingIntoAnotherRoom_ReadsExit_UnexploredUntilVisited(bool visited, bool unexplored)
    {
        var world = FakeRadarWorld.Corridor(secondRoomFromX: 10);
        if (visited) world.Visited.Add(1);

        var sensor = Baseline(world, 2);

        Assert.Equal(RadarCueKind.Exit, sensor.State);
        Assert.Equal(unexplored, sensor.Unexplored);
    }

    [Fact]
    public void TouchingAnotherRoomWithoutClearingADoorway_ReadsOpen()
    {
        // Enters room 1 at depth 8 but ends at 13, short of 8 + the doorway width (D2R radar_passage.cpp:105).
        var sensor = Baseline(FakeRadarWorld.Corridor(wallX: 16, secondRoomFromX: 10), 2);

        Assert.Equal(RadarCueKind.Open, sensor.State);
    }

    [Theory]
    [InlineData(12)]
    [InlineData(4)] // within the closing depth: still never a wall
    public void RouteIntoUnknownCells_ReadsUnexploredExit(int unknownFromX)
    {
        var sensor = Baseline(FakeRadarWorld.Corridor(unknownFromX: unknownFromX), 2);

        Assert.Equal(RadarCueKind.Exit, sensor.State);
        Assert.True(sensor.Unexplored);
    }

    [Fact]
    public void UnknownPlayerCell_GivesNoReading()
    {
        var sensor = Baseline(new FakeRadarWorld((_, _) => RadarCell.Unknown), 2);

        Assert.False(sensor.Passage.Available);
        Assert.Null(sensor.State);
    }

    [Fact]
    public void ThinDiagonalWall_DoesNotLeak()
    {
        // A staircase wall x + y = 8 across the ray: its open cells touch only at corners.
        var sensor = Baseline(FakeRadarWorld.Field((x, y) => x + y == 8), 2);

        Assert.Equal(RadarCueKind.Blocked, sensor.State);
    }

    private static RadarSensor Baseline(IRadarWorld world, int playerX)
    {
        var engine = new RadarEngine(new RadarSettings { Frame = RadarFrame.Diagonal });
        Assert.Empty(engine.Tick(world, playerX + 0.5, 2.5, nowMs: 1000));
        return engine.Sensor(South);
    }
}
