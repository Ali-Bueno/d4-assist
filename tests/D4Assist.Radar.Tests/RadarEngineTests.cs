using D4Assist.Audio;
using D4Assist.Radar;

namespace D4Assist.Radar.Tests;

/// <summary>Engine behaviour over several polls: the hysteresis band, the way-back mark and the clutter filter.
/// Diagonal frame: Right runs along +X, Left along -X (RadarRaysTests).</summary>
public class RadarEngineTests
{
    private const long PollMs = 100;

    [Fact]
    public void InsideTheBand_ACommittedWallStands()
    {
        var engine = Diagonal();
        var world = FakeRadarWorld.Corridor(wallX: 30);
        engine.Tick(world, 25.5, 2.5, 0); // depth 4: wall

        long now = 0;
        for (var x = 24; x >= 21; x--) engine.Tick(world, x + 0.5, 2.5, now += PollMs);

        var sensor = engine.Sensor(RadarRay.Right);
        Assert.Equal(8, sensor.Passage.Depth); // between close 7 and open 10
        Assert.Equal(RadarCueKind.Blocked, sensor.Passage.Observed);
        Assert.Equal(RadarCueKind.Blocked, sensor.State);
    }

    [Fact]
    public void InsideTheBand_NothingCommittedReadsFloor()
    {
        var engine = Diagonal();
        engine.Tick(FakeRadarWorld.Corridor(wallX: 30), 21.5, 2.5, 0);

        Assert.Equal(RadarCueKind.Open, engine.Sensor(RadarRay.Right).State);
    }

    [Fact]
    public void TheDoorwayJustCrossed_IsTakenAsRead()
    {
        var engine = Diagonal();
        var world = FakeRadarWorld.Corridor(wallX: 60, secondRoomFromX: 20);
        var backCues = new List<RadarCue>();
        long now = 0;
        for (var x = 2; x <= 24; x++)
            backCues.AddRange(engine.Tick(world, x + 0.5, 2.5, now += PollMs).Where(c => c.Ray == RadarRay.Left && x >= 20));

        var back = engine.Sensor(RadarRay.Left);
        Assert.Equal(RadarCueKind.Exit, back.State);
        Assert.Equal(RadarHold.WayBack, back.Hold);
        Assert.Empty(backCues);
    }

    [Theory]
    [InlineData(false, RadarCueKind.Blocked)]
    [InlineData(true, RadarCueKind.Open)]
    public void LonePillar_IsErasedOnlyWhenClutterRemovalIsOn(bool removeClutter, RadarCueKind expected)
    {
        var engine = new RadarEngine(new RadarSettings { Frame = RadarFrame.Diagonal, RemoveClutter = removeClutter });
        var world = FakeRadarWorld.Field((x, y) => x == 6 && y is >= 1 and <= 3); // 1x3, across the ray

        engine.Tick(world, 2.5, 2.5, 0);

        Assert.Equal(expected, engine.Sensor(RadarRay.Right).State);
    }

    private static RadarEngine Diagonal() => new(new RadarSettings { Frame = RadarFrame.Diagonal });
}
