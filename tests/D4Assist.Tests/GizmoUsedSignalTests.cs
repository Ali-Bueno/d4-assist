using D4Assist.Game;

namespace D4Assist.Tests;

/// <summary>The chest glow and remembered-spot "used" signal (live 2026-09-30, ProtoDunKeep_Chest_01).</summary>
public class GizmoUsedSignalTests
{
    private const int ChestSno = 356000, GlowSno = 1357946, SnowPileSno = 996023;

    private static readonly Dictionary<int, string> Names = new()
    {
        [ChestSno] = "ProtoDunKeep_Chest_01",
        [GlowSno] = "vfx_genericChestGlow_hardpointLight_square",
        [SnowPileSno] = "Generic_Clicky_Snow_Pile_01_Dyn",
    };

    private readonly GizmoUsedSignal _signal = new(sno => Names.GetValueOrDefault(sno));

    private static ActorState Chest(uint id = 1, bool stateChanged = false) => new(id, ChestSno, 10, 10, 0, stateChanged);
    private static ActorState Glow() => new(99, GlowSno, 10.015f, 10, -0.33f);
    private static PlayerState At(float x) => new(x, 10, 0, 0);

    private bool Poll(ActorState chest, PlayerState player, bool glowing)
    {
        _signal.Observe(glowing ? [chest, Glow()] : [chest], player);
        return _signal.IsUsed(chest);
    }

    [Fact]
    public void A_chest_is_open_once_its_glow_is_gone_and_stays_open_when_re_created()
    {
        Assert.False(Poll(Chest(), At(12), glowing: true));

        Assert.True(Poll(Chest(), At(11), glowing: false));
        Assert.True(Poll(Chest(id: 7), At(40), glowing: false)); // back in range under a new id, judged from far away
    }

    [Fact]
    public void A_missing_glow_far_away_is_not_judged()
    {
        Assert.False(Poll(Chest(), At(12), glowing: true));

        Assert.False(Poll(Chest(), At(10 + GizmoUsedSignal.JudgeRangeUnits + 1), glowing: false));
    }

    [Fact]
    public void Kinds_never_seen_glowing_use_the_state_word_and_are_remembered_by_spot()
    {
        var pile = new ActorState(1, SnowPileSno, 5, 5, 0);
        _signal.Observe([pile], At(5));
        Assert.False(_signal.IsUsed(pile));

        Assert.True(_signal.IsUsed(pile with { StateChanged = true }));
        Assert.True(_signal.IsUsed(pile with { Id = 8 })); // re-created: state word back to 0, spot remembered
    }

    [Fact]
    public void A_switch_whose_state_never_moves_is_used_once_operated()
    {
        var petals = new ActorState(1, SnowPileSno, 5, 5, 0);
        _signal.Observe([petals], At(5));
        Assert.False(_signal.IsUsed(petals));

        Assert.True(_signal.IsUsed(petals with { Operated = true }));
    }

    [Fact]
    public void A_kind_known_from_an_earlier_run_judges_a_chest_first_seen_open()
    {
        var learned = new List<int>();
        var signal = new GizmoUsedSignal(sno => Names.GetValueOrDefault(sno), [ChestSno], learned.Add);

        signal.Observe([Chest()], At(12));

        Assert.True(signal.IsUsed(Chest()));
        Assert.Empty(learned);
    }

    [Fact]
    public void A_newly_seen_glowing_kind_is_reported_once()
    {
        var learned = new List<int>();
        var signal = new GizmoUsedSignal(sno => Names.GetValueOrDefault(sno), learned: learned.Add);

        for (int i = 0; i < 3; i++)
        {
            signal.Observe([Chest(), Glow()], At(30));
            signal.IsUsed(Chest());
        }

        Assert.Equal([ChestSno], learned);
    }

    [Fact]
    public void The_store_keeps_learned_kinds_across_runs()
    {
        string dir = Directory.CreateTempSubdirectory("d4assist-chests-").FullName;
        GlowingChestStore.Load(dir).Add(ChestSno);

        Assert.Equal([ChestSno], GlowingChestStore.Load(dir).Kinds);
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void A_new_world_forgets_the_used_spots()
    {
        Poll(Chest(), At(12), glowing: true);
        Poll(Chest(), At(11), glowing: false);

        _signal.Reset();

        Assert.False(Poll(Chest(), At(12), glowing: true));
    }
}
