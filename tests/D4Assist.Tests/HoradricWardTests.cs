using D4Assist.Game;

namespace D4Assist.Tests;

// Live 2026-10-23 (QST_Skov_Skartara_06_VaultBoss): the guardian showed Horadric_Guardian_harmless_Barrier_Zoltun, the player used
// Kulle's vessel; then Barrier_Jared, the player used Tal Rasha's and died.
public class HoradricWardTests
{
    private const int GuardianSno = 2301098, Zoltun = 2377345, Jared = 2377401, Kulle = 2480807, Cain = 2481230, Rasha = 2481232;

    private static readonly Dictionary<int, string> Names = new()
    {
        [GuardianSno] = "QST_Horadric_Guardian_boss",
        [Zoltun] = "Horadric_Guardian_harmless_Barrier_Zoltun",
        [Jared] = "Horadric_Guardian_harmless_Barrier_Jared",
        [Kulle] = "QST_Skov_Skartara_WardBreakerShrine_Kulle",
        [Cain] = "QST_Skov_Skartara_WardBreakerShrine_Cain",
        [Rasha] = "QST_Skov_Skartara_WardBreakerShrine_Rasha",
    };

    private static ActorState Guardian(params int[] buffs) => new(1, GuardianSno, 16, 16, 0, Hostile: true, Buffs: buffs);

    private static ActorState Vessel(int sno, float x, float y, bool on = false) => new((uint)sno, sno, x, y, 0, GizmoStateSet: on);

    private static readonly ActorState[] Vessels = [Vessel(Kulle, 26.7f, 5.3f), Vessel(Cain, 5.2f, 5.1f), Vessel(Rasha, 5.3f, 26.8f)];

    private static HoradricWard Ward() => new(sno => Names.GetValueOrDefault(sno));

    [Fact]
    public void Before_the_guardian_fights_the_vessels_are_left_alone()
    {
        var ward = Ward();
        var actors = ward.Apply([.. Vessels]);

        Assert.Equal(Vessels, actors);
        Assert.Empty(ward.Destinations());
    }

    [Fact]
    public void A_barrier_leads_to_the_vessel_it_names_then_into_the_barrier()
    {
        var ward = Ward();

        var actors = ward.Apply([Guardian(Zoltun), .. Vessels]);
        Assert.True(actors.Single(a => a.ActorSno == Kulle).QuestStep);
        Assert.All(actors.Where(a => a.ActorSno is Cain or Rasha), a => Assert.True(a.QuestLocked));
        var vessel = Assert.Single(ward.Destinations());
        Assert.Equal((26.7f, 5.3f), vessel.Position);

        ward.Apply([Guardian(Zoltun), Vessel(Kulle, 26.7f, 5.3f, on: true), Vessels[1], Vessels[2]]);
        var barrier = Assert.Single(ward.Destinations());
        Assert.Equal((16f, 16f), barrier.Position);

        actors = ward.Apply([Guardian(), Vessel(Kulle, 26.7f, 5.3f, on: true), Vessels[1], Vessels[2]]);
        Assert.Empty(ward.Destinations());
        Assert.All(actors.Where(a => a.ActorSno != GuardianSno), a => Assert.True(a.QuestLocked)); // the fight goes on: all silent
    }

    [Fact]
    public void The_next_barrier_names_its_own_vessel_though_another_was_used_before()
    {
        var ward = Ward();
        ward.Apply([Guardian(Zoltun), .. Vessels]);
        ward.Apply([Guardian(Zoltun), Vessel(Kulle, 26.7f, 5.3f, on: true), Vessels[1], Vessels[2]]);
        ward.Apply([Guardian(), Vessel(Kulle, 26.7f, 5.3f, on: true), Vessels[1], Vessels[2]]);

        var actors = ward.Apply([Guardian(Jared), Vessel(Kulle, 26.7f, 5.3f, on: true), Vessels[1], Vessels[2]]);

        Assert.True(actors.Single(a => a.ActorSno == Cain).QuestStep);
        Assert.True(actors.Single(a => a.ActorSno == Kulle).QuestLocked);
        Assert.Equal((5.2f, 5.1f), Assert.Single(ward.Destinations()).Position);
    }

    [Fact]
    public void The_guardian_gone_ends_the_fight()
    {
        var ward = Ward();
        ward.Apply([Guardian(Zoltun), .. Vessels]);

        var actors = ward.Apply([.. Vessels]);

        Assert.Equal(Vessels, actors);
        Assert.Empty(ward.Destinations());
    }
}
