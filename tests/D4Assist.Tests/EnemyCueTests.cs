using D4Assist.Audio;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class EnemyCueTests
{
    private const int Monster = Conventions.ActorTypeMonster, Friendly = 2;
    private const int BossSno = 1, UniqueSno = 2, OrdinarySno = 3, FriendlyBossSno = 4, GizmoSno = 5;

    private static readonly Dictionary<int, ActorDefinitionInfo> Actors = new()
    {
        [BossSno] = new(Monster, -1, DefaultTeam: Conventions.HostileTeam, MonsterRarity: Conventions.MonsterRarityBoss),
        [UniqueSno] = new(Monster, -1, DefaultTeam: Conventions.HostileTeam, MonsterRarity: Conventions.MonsterRarityUnique),
        [OrdinarySno] = new(Monster, -1, DefaultTeam: Conventions.HostileTeam),
        [FriendlyBossSno] = new(Monster, -1, DefaultTeam: Friendly, MonsterRarity: Conventions.MonsterRarityBoss),
        [GizmoSno] = new(0, 0, DefaultTeam: Conventions.HostileTeam, MonsterRarity: Conventions.MonsterRarityBoss),
    };

    private static readonly EnemyClassifier Classifier = new(sno => Actors.GetValueOrDefault(sno));

    [Theory]
    [InlineData(BossSno, EnemyClass.Boss)]
    [InlineData(UniqueSno, EnemyClass.Elite)]
    [InlineData(OrdinarySno, null)]
    [InlineData(FriendlyBossSno, null)]
    [InlineData(GizmoSno, null)]
    [InlineData(999, null)] // unreadable
    public void Rarity_picks_the_cue_of_hostile_monsters_only(int sno, EnemyClass? expected) =>
        Assert.Equal(expected, Classifier.ClassOf(sno));

    [Fact]
    public void A_boss_whose_definition_is_not_hostile_is_cued_once_its_live_team_is()
    {
        // Live 2026-10-23: the Horadric guardian's definition says team 1; it turns hostile for its fight.
        var guardian = new ActorState(7, FriendlyBossSno, 0, 0, 0);
        Assert.Null(Classifier.Of(guardian));
        Assert.Equal(EnemyClass.Boss, Classifier.Of(guardian with { Hostile = true }));
        Assert.Null(Classifier.Of(new ActorState(8, GizmoSno, 0, 0, 0, Hostile: true)));
    }

    private static (FakeFeed Feed, AudioMixer Mixer, EnemyCuePlayer Player, List<string> Log) Bridge()
    {
        var feed = new FakeFeed();
        var mixer = new AudioMixer();
        var player = new EnemyCuePlayer(mixer, () => 0, autoTick: false);
        var log = new List<string>();
        _ = new EnemyCueBridge(feed, player, Classifier.Of, _ => null, log.Add);
        return (feed, mixer, player, log);
    }

    [Fact]
    public void The_nearest_enemy_of_each_class_within_reach_is_cued()
    {
        var (feed, _, _, log) = Bridge();
        feed.RaiseActors(
            new ActorState(1, BossSno, 10, 0, 0), new ActorState(2, BossSno, 5, 0, 0),
            new ActorState(3, UniqueSno, 0, 8, 0), new ActorState(4, OrdinarySno, 1, 0, 0),
            new ActorState(5, UniqueSno, (float)EnemyCuePlayer.RangeUnits + 5, 0, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 1, 0));

        Assert.Equal(2, log.Count);
        Assert.Contains(log, l => l.Contains("(Boss)") && l.Contains("id=2"));
        Assert.Contains(log, l => l.Contains("(Elite)") && l.Contains("id=3"));
    }

    [Fact]
    public void Dead_and_invulnerable_enemies_are_silent()
    {
        var (feed, mixer, player, log) = Bridge();
        feed.RaiseActors(new ActorState(1, BossSno, 5, 0, 0, Invulnerable: true), new ActorState(2, UniqueSno, 5, 0, 0, ClientOnly: true));
        feed.RaisePlayer(new PlayerState(0, 0, 1, 0));
        player.Tick();

        Assert.Empty(log);
        Assert.Equal(0, mixer.ActiveEntityVoiceCount);
    }

    [Fact]
    public void A_world_change_silences_the_cue()
    {
        var (feed, mixer, player, _) = Bridge();
        feed.RaiseActors(new ActorState(1, BossSno, 5, 0, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 1, 0));
        feed.RaiseWorld();
        player.Tick();

        Assert.Equal(0, mixer.ActiveEntityVoiceCount);
    }
}
