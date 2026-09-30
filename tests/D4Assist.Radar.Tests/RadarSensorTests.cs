using D4Assist.Audio;
using D4Assist.Radar;

namespace D4Assist.Radar.Tests;

/// <summary>The per-direction state machine (D2R Engine::Observe/Deliver, radar.cpp:71-155) on synthetic scans
/// along +X, the player walking the axis. Default settings: open 10, close 7, confirm 1 cell, 100 ms polls.</summary>
public class RadarSensorTests
{
    private static readonly RadarSettings Settings = new();
    private static readonly RadarVector Axis = new(1, 0);

    private readonly RadarSensor _sensor = new(Settings);

    [Fact]
    public void FirstObservation_IsASilentBaseline()
    {
        Assert.Null(Walk(0, 0, Scan(RadarCueKind.Blocked, 4)));
        Assert.Equal(RadarCueKind.Blocked, _sensor.State);
    }

    [Fact]
    public void Change_ConfirmsOnlyAfterOneCellOfTravel()
    {
        Walk(0, 0, Scan(RadarCueKind.Open, 20));

        Assert.Null(Walk(100, 0.5, Scan(RadarCueKind.Blocked, 6)));
        Assert.Null(Walk(200, 1.0, Scan(RadarCueKind.Blocked, 5)));
        Assert.Equal(RadarCueKind.Blocked, Walk(300, 1.5, Scan(RadarCueKind.Blocked, 5)));
    }

    [Fact]
    public void StationaryPlayer_ConfirmsAfterTwoPolls()
    {
        Stand(0, Scan(RadarCueKind.Open, 20));

        Assert.Null(Stand(100, Scan(RadarCueKind.Blocked, 6)));
        Assert.Null(Stand(200, Scan(RadarCueKind.Blocked, 6)));
        Assert.Equal(RadarCueKind.Blocked, Stand(300, Scan(RadarCueKind.Blocked, 6)));
    }

    [Fact]
    public void Openings_WaitForTheCueInterval()
    {
        Walk(0, 0, Scan(RadarCueKind.Blocked, 5));
        Walk(100, 1, Scan(RadarCueKind.Exit, 20));
        Assert.Equal(RadarCueKind.Exit, Walk(200, 2, Scan(RadarCueKind.Exit, 20)));

        Walk(300, 3, Scan(RadarCueKind.Open, 20));
        Assert.Null(Walk(400, 4, Scan(RadarCueKind.Open, 20)));
        Assert.Equal(RadarHold.Interval, _sensor.Hold);
        Assert.Null(Walk(600, 5, Scan(RadarCueKind.Open, 20)));
        Assert.Equal(RadarCueKind.Open, Walk(700, 6, Scan(RadarCueKind.Open, 20)));
    }

    [Fact]
    public void Walls_AreNeverDelayedByAnOpening()
    {
        Walk(0, 0, Scan(RadarCueKind.Open, 20));
        Walk(100, 1, Scan(RadarCueKind.Exit, 20));
        Assert.Equal(RadarCueKind.Exit, Walk(200, 2, Scan(RadarCueKind.Exit, 20)));

        Walk(300, 3, Scan(RadarCueKind.Blocked, 5));
        Assert.Equal(RadarCueKind.Blocked, Walk(400, 4, Scan(RadarCueKind.Blocked, 4)));
    }

    [Fact]
    public void BackingStraightAwayFromAnAnnouncedWall_DoesNotReadOpen()
    {
        const int wall = 20; // the route ends at x = 20
        Walk(0, 0, Scan(RadarCueKind.Open, wall));
        Walk(100, 13, Scan(RadarCueKind.Blocked, wall - 13));
        Assert.Equal(RadarCueKind.Blocked, Walk(200, 14, Scan(RadarCueKind.Blocked, wall - 14)));
        Assert.Equal((double)wall, _sensor.Frontier!.Value.Projection);

        var cues = new List<RadarCueKind?>();
        long now = 200;
        for (var x = 13; x >= 5; x--)
        {
            var depth = wall - x;
            // Inside the band the scan keeps the committed wall (PassageScanner); beyond it, floor.
            var reading = depth < Settings.OpenDepth ? RadarCueKind.Blocked : RadarCueKind.Open;
            cues.Add(Walk(now += 100, x, Scan(reading, depth)));
        }

        Assert.All(cues, Assert.Null);
        Assert.Equal(RadarCueKind.Open, _sensor.State);
        Assert.Equal(RadarHold.Retreat, _sensor.Hold);
    }

    [Fact]
    public void BackingAwayFromABaselineWall_DoesNotReadOpen()
    {
        const int wall = 5;
        Walk(0, 0, Scan(RadarCueKind.Blocked, wall)); // silent baseline: the wall was never announced

        Assert.All(Retreat(wall, from: 0, to: -8, startMs: 0), Assert.Null);
        Assert.Equal(RadarCueKind.Open, _sensor.State);
        Assert.Equal(RadarHold.Retreat, _sensor.Hold);
    }

    [Fact]
    public void BackingAwayAfterWalkingAlongTheWall_DoesNotReadOpen()
    {
        const int wall = 20;
        Walk(0, 0, Scan(RadarCueKind.Open, wall));
        Walk(100, 13, Scan(RadarCueKind.Blocked, wall - 13));
        Assert.Equal(RadarCueKind.Blocked, Walk(200, 14, Scan(RadarCueKind.Blocked, wall - 14)));

        long now = 200;
        var along = Settings.BoundaryRange + 10; // past the range around the point where the wall was announced
        for (var y = 1; y <= along; y++) Move(now += 100, 14, y, Scan(RadarCueKind.Blocked, wall - 14));

        Assert.All(Retreat(wall, from: 13, to: 5, startMs: now, y: along), Assert.Null);
        Assert.Equal(RadarHold.Retreat, _sensor.Hold);
    }

    [Fact]
    public void RouteReachingPastTheWall_StillSoundsAfterARetreat()
    {
        Walk(0, 0, Scan(RadarCueKind.Open, 20));
        Walk(100, 13, Scan(RadarCueKind.Blocked, 7));
        Walk(200, 14, Scan(RadarCueKind.Blocked, 6));

        Walk(300, 13, Scan(RadarCueKind.Exit, 25)); // a doorway beside the wall proves the route beyond it
        Assert.Equal(RadarCueKind.Exit, Walk(400, 12, Scan(RadarCueKind.Exit, 26)));
    }

    [Fact]
    public void AnExitWhoseRouteEndsAtTheAnnouncedWall_StillSounds()
    {
        Walk(0, 0, Scan(RadarCueKind.Open, 20));
        Walk(100, 13, Scan(RadarCueKind.Blocked, 7));
        Walk(200, 14, Scan(RadarCueKind.Blocked, 6));

        Move(300, 14, 1, Scan(RadarCueKind.Exit, 6)); // a doorway beside the player, its route ending at the same wall
        Assert.Equal(RadarCueKind.Exit, Move(400, 14, 2, Scan(RadarCueKind.Exit, 6)));
    }

    // Steps back along the axis one cell per poll from a wall at x = wall; inside the band the scan keeps the wall.
    private List<RadarCueKind?> Retreat(int wall, int from, int to, long startMs, double y = 0)
    {
        var cues = new List<RadarCueKind?>();
        for (var x = from; x >= to; x--)
        {
            var depth = wall - x;
            var reading = depth < Settings.OpenDepth ? RadarCueKind.Blocked : RadarCueKind.Open;
            cues.Add(Move(startMs += 100, x, y, Scan(reading, depth)));
        }
        return cues;
    }

    private RadarCueKind? Walk(long now, double x, Passage scan) => Move(now, x, 0, scan);

    private RadarCueKind? Move(long now, double x, double y, Passage scan) => _sensor.Observe(scan, Axis, new RadarVector(x, y), now, lastMoveMs: now);

    private RadarCueKind? Stand(long now, Passage scan) => _sensor.Observe(scan, Axis, new RadarVector(0, 0), now, lastMoveMs: 0);

    private static Passage Scan(RadarCueKind reading, double depth) => new()
    {
        Available = true, Observed = reading, Depth = depth, OpenDepth = Settings.OpenDepth, CloseDepth = Settings.CloseDepth,
    };
}
