using D4Assist.Audio;
using D4Assist.Game;

namespace D4Assist.Tests;

// Live 2026-10-23/24 (the Horadric guardian): spirit warnings of five effects on one spot, a wind-up of twenty effects on the
// guardian, a wave wall of 13 segments sweeping the room.
public class HazardLoopBridgeTests
{
    private const int GuardianSno = 1, SwirlSno = 2, LightSno = 3, WallSno = 6, WindupSno = 8, PropSno = 9;

    private static readonly Dictionary<int, string> Names = new()
    {
        [GuardianSno] = "QST_Horadric_Guardian_boss",
        [SwirlSno] = "fxKit_damaging_persistentWarning_spirits_swirl_fxMesh",
        [LightSno] = "fxKit_damaging_persistentWarning_spirits_lightParent",
        [WallSno] = "Horadric_Guardian_wall_demonic",
        [WindupSno] = "fxKit_damaging_burstAttractor_demonic_parent",
        [PropSno] = "MageHalls_Prop_Scroll_01_Dyn",
    };

    private static ActorState Guardian() => new(100, GuardianSno, 16, 16, 0);

    private static ActorState Effect(uint id, int sno, float x, float y) => new(id, sno, x, y, 0, ClientOnly: true);

    private static ActorState Wall(uint id, float x, float y) => new(id, WallSno, x, y, 0, Yaw: MathF.PI / 2);

    private static (FakeFeed Feed, List<IReadOnlyCollection<LoopSource>> Updates) Create()
    {
        var feed = new FakeFeed();
        var updates = new List<IReadOnlyCollection<LoopSource>>();
        _ = new HazardLoopBridge(feed, new TelegraphClassifier(sno => Names.GetValueOrDefault(sno)),
            a => a.ActorSno == GuardianSno ? EnemyClass.Boss : null, sno => sno == WallSno, updates.Add);
        feed.RaisePlayer(new PlayerState(10, 10, 0, 1));
        return (feed, updates);
    }

    [Fact]
    public void A_warnings_effects_on_one_spot_sound_as_one_loop_there()
    {
        var (feed, updates) = Create();
        feed.RaiseActors(Guardian(), Effect(2, SwirlSno, 12, 10), Effect(3, SwirlSno, 12, 10.5f), Effect(4, LightSno, 12, 10), Effect(5, PropSno, 11, 10));

        var loop = Assert.Single(updates[^1]);
        Assert.Equal(HazardLoopBridge.HazardSound, loop.Sound);
        Assert.Equal(2, Math.Sqrt(loop.Right * loop.Right + loop.Up * loop.Up), 3);
    }

    [Fact]
    public void A_wall_sounds_once_at_its_segment_nearest_the_player_and_moves_with_it()
    {
        var (feed, updates) = Create();
        feed.RaiseActors([Guardian(), .. Enumerable.Range(0, 13).Select(i => Wall((uint)(200 + i), 3.5f + 2 * i, 4))]);
        var first = Assert.Single(updates[^1]);
        feed.RaiseActors([Guardian(), .. Enumerable.Range(0, 13).Select(i => Wall((uint)(200 + i), 3.5f + 2 * i, 7))]);
        var later = Assert.Single(updates[^1]);

        Assert.Equal(first.Key, later.Key); // the same loop, retargeted
        Assert.Equal(6, Math.Sqrt(first.Right * first.Right + first.Up * first.Up), 0); // nearest segment (9.5,4)
        Assert.Equal(3, Math.Sqrt(later.Right * later.Right + later.Up * later.Up), 0);
    }

    [Fact]
    public void The_loop_follows_the_player_between_actor_reads()
    {
        var (feed, updates) = Create();
        feed.RaiseActors(Guardian(), Effect(500, WindupSno, 16, 16));
        feed.RaisePlayer(new PlayerState(16, 13, 0, 1));

        var loop = Assert.Single(updates[^1]);
        Assert.Equal(3, Math.Sqrt(loop.Right * loop.Right + loop.Up * loop.Up), 3);
    }

    [Fact]
    public void Outside_a_fight_and_once_the_attack_is_gone_nothing_sounds()
    {
        var (feed, updates) = Create();
        feed.RaiseActors(Effect(2, SwirlSno, 12, 10)); // no boss near
        Assert.Empty(updates[^1]);
        feed.RaiseActors(Guardian(), Effect(2, SwirlSno, 12, 10));
        Assert.Single(updates[^1]);
        feed.RaiseActors(Guardian());
        Assert.Empty(updates[^1]);
    }
}
