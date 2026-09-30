using D4Assist.Audio;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class NpcTests
{
    private const int Monster = Conventions.ActorTypeMonster, Friendly = 2, Neutral = 0;
    private const int MonkSno = 1, FollowerSno = 2, HostileVaniSno = 3, MercenarySno = 4, CinematicSno = 5, FallenSno = 6;
    private const int NpcFamily = 100, MercenaryFamily = 101;

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [MonkSno] = ("NPC_Frac_Prologue_Monk", Npc(Neutral, NpcFamily)),
        [FollowerSno] = ("NPC_QST_Lorath_Follower", Npc(Friendly, NpcFamily)),
        [HostileVaniSno] = ("Monster_Frac_Prologue_Vani", Npc(Conventions.HostileTeam, NpcFamily)),
        [MercenarySno] = ("Mercenary_BountyHunter", Npc(Friendly, MercenaryFamily)),
        [CinematicSno] = ("IGC_lorath", Npc(Neutral, NpcFamily)),
        [FallenSno] = ("fallen_shaman", new(Monster, -1, DefaultTeam: Conventions.HostileTeam)),
    };

    private static readonly Dictionary<int, string> Families = new() { [NpcFamily] = "NPC", [MercenaryFamily] = "NPC_Mercenary" };

    private static ActorDefinitionInfo Npc(int team, int family) => new(Monster, -1, DefaultTeam: team, IsNpc: true, MonsterFamilySno: family);

    private static readonly NpcClassifier Classifier = new(
        sno => Actors.TryGetValue(sno, out var a) ? a.Info : null,
        sno => Actors.TryGetValue(sno, out var a) ? a.Name : Families.GetValueOrDefault(sno));

    [Theory]
    [InlineData(MonkSno, true)]
    [InlineData(FollowerSno, true)] // quest followers lead the way (user, 2026-09-30)
    [InlineData(HostileVaniSno, false)]
    [InlineData(MercenarySno, false)]
    [InlineData(CinematicSno, false)]
    [InlineData(FallenSno, false)]
    [InlineData(999, false)] // unreadable
    public void Friendly_npcs_tap_and_everything_else_does_not(int sno, bool expected) =>
        Assert.Equal(expected, Classifier.IsNpc(sno));

    [Fact]
    public void The_tap_follows_the_nearest_npc_within_range()
    {
        var feed = new FakeFeed();
        var mixer = new AudioMixer();
        double now = 0;
        using var tap = new NpcCuePlayer(mixer, () => now, autoTick: false);
        _ = new NpcCueBridge(feed, tap, a => Classifier.IsNpc(a.ActorSno), _ => null);

        feed.RaiseActors(new ActorState(1, MonkSno, 30, 0, 0), new ActorState(2, FallenSno, 1, 0, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 1, 0));
        tap.Tick();
        Assert.Equal(0, mixer.ActiveEntityVoiceCount); // the monk is out of range, the fallen is no NPC

        feed.RaisePlayer(new PlayerState(25, 0, 1, 0));
        tap.Tick();
        Assert.Equal(1, mixer.ActiveEntityVoiceCount);
    }

    [Theory]
    [InlineData(8, 0)]  // walks toward the player: a companion following, silent
    [InlineData(12, 1)] // walks away: leading the way, it keeps sounding
    public void An_npc_that_follows_the_player_is_silent_and_one_that_leads_is_not(float endX, int voices)
    {
        var feed = new FakeFeed();
        var mixer = new AudioMixer();
        double now = 0;
        using var tap = new NpcCuePlayer(mixer, () => now, autoTick: false);
        _ = new NpcCueBridge(feed, tap, a => Classifier.IsNpc(a.ActorSno), _ => null, nowSeconds: () => now);

        feed.RaisePlayer(new PlayerState(0, 0, 1, 0));
        feed.RaiseActors(new ActorState(1, FollowerSno, 10, 0, 0));
        now = 1;
        feed.RaiseActors(new ActorState(1, FollowerSno, endX, 0, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 1, 0));
        tap.Tick();

        Assert.Equal(voices, mixer.ActiveEntityVoiceCount);
    }
}
