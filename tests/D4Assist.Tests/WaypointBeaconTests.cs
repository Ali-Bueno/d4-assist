using System.Numerics;
using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;
using D4Data.Readers;

namespace D4Assist.Tests;

/// <summary>WaypointBeacons: loops at the current world's waypoints not activated yet, sounded by ActorLoopBridge.</summary>
public class WaypointBeaconTests
{
    private const int World = 69068, OtherWorld = 1472231;

    private static readonly Waypoint Near = new(1, "Near", World, new Vector3(10, 0, 0));
    private static readonly Waypoint Activated = new(2, "Activated", World, new Vector3(0, 10, 0));
    private static readonly Waypoint Far = new(3, "Far", World, new Vector3((float)LoopProfiles.Beacon.Reach + 1, 0, 0));
    private static readonly Waypoint Elsewhere = new(4, "Elsewhere", OtherWorld, new Vector3(5, 0, 0));
    private static readonly PlayerState Origin = new(0, 0, 0, 0);

    private static WaypointBeacons Create(Func<IReadOnlySet<int>, IReadOnlyList<int>?> readActivated) =>
        new([Near, Activated, Far, Elsewhere], readActivated, () => World, runRead: read => read());

    [Fact]
    public void Only_this_worlds_waypoints_not_activated_within_reach_loop()
    {
        var spots = Create(_ => [Activated.LevelAreaSno]).Spots(Origin);

        Assert.Equal([$"{WaypointBeacons.Reason}:{Near.LevelAreaSno}"], spots.Select(s => s.Key));
        Assert.All(spots, s => Assert.Equal(SoundCue.Waypoint, s.Target.Cue));
    }

    [Fact]
    public void Nothing_loops_while_the_activated_list_is_unreadable()
    {
        Assert.Empty(Create(_ => null).Spots(Origin));
    }

    [Fact]
    public void The_activated_list_is_asked_for_this_games_waypoints_only()
    {
        IReadOnlySet<int>? asked = null;

        Create(levelAreas => { asked = levelAreas; return []; }).Spots(Origin);

        Assert.Equal([1, 2, 3, 4], asked!.Order());
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 0)]
    public void The_bridge_sounds_the_spots_while_their_cue_is_on(bool cueOn, int loops)
    {
        var feed = new FakeFeed();
        var player = LoopTestKit.Player();
        var selector = new LoopTargetSelector(new HashSet<int>(), null, _ => new HashSet<int>());
        _ = new ActorLoopBridge(feed, player, selector, _ => null, spots: Create(_ => []).Spots,
            levelOf: cue => cue == SoundCue.Waypoint && !cueOn ? CueLevel.Full with { Gain = 0 } : CueLevel.Full);
        feed.RaiseWorld(World);

        feed.RaisePlayer(Origin);

        Assert.Equal(loops, player.ActiveCount); // the two of this world within reach, none activated
    }
}
