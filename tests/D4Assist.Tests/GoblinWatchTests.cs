using D4Assist.Audio;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class GoblinWatchTests
{
    // Game data, build 3.2.3: treasure_goblin respects the reveal radius; the Undercity's goblin shows from anywhere within 2048.
    private const int Goblin = 384484, UndercityGoblin = 2048580, Cultist = 1186208, GoblinFamily = 4100, CultistFamily = 4200;

    private static readonly Dictionary<int, ActorDefinitionInfo> Definitions = new()
    {
        [Goblin] = new(Conventions.ActorTypeMonster, 0, DefaultTeam: Conventions.HostileTeam, MonsterFamilySno: GoblinFamily,
            Minimap: new MinimapInfo(2609695548, ActiveByDefault: false, RespectsRevealRadius: true, MaxVisRange: 0)),
        [UndercityGoblin] = new(Conventions.ActorTypeMonster, 0, DefaultTeam: Conventions.HostileTeam, MonsterFamilySno: GoblinFamily,
            Minimap: new MinimapInfo(889161053, ActiveByDefault: true, RespectsRevealRadius: false, MaxVisRange: 2048)),
        [Cultist] = new(Conventions.ActorTypeMonster, 0, DefaultTeam: Conventions.HostileTeam, MonsterFamilySno: CultistFamily),
    };

    private static string? NameOf(int sno) => sno switch
    {
        GoblinFamily => TreasureGoblins.Family,
        CultistFamily => "cultist",
        _ => null,
    };

    private static GoblinWatch NewWatch(List<string>? log = null) =>
        new(sno => Definitions.TryGetValue(sno, out var info) ? info : null, NameOf, log is null ? null : log.Add);

    [Fact]
    public void A_goblin_gets_the_elites_cue_and_an_ordinary_monster_none()
    {
        var classes = new EnemyClassifier(sno => Definitions.TryGetValue(sno, out var info) ? info : null, NameOf);

        Assert.Equal(EnemyClass.Elite, classes.ClassOf(Goblin));
        Assert.Equal(EnemyClass.Elite, classes.ClassOf(UndercityGoblin));
        Assert.Null(classes.ClassOf(Cultist));
    }

    [Fact]
    public void A_reveal_radius_goblin_lists_while_marked_within_its_range_streamed_in_or_not()
    {
        var watch = NewWatch();
        watch.OnPlayerMoved(new PlayerState(0, 0, 0, 0));

        Assert.True(watch.IsShown(new MarkedActor(Goblin, GoblinWatch.ListedUnits, 0, 0, 1, 10)));
        Assert.False(watch.IsShown(new MarkedActor(Goblin, GoblinWatch.ListedUnits + 1, 0, 0, 1, 10)));
    }

    [Fact]
    public void An_undercity_goblin_shows_from_anywhere_within_its_range()
    {
        var watch = NewWatch();
        watch.OnPlayerMoved(new PlayerState(0, 0, 0, 0));

        Assert.True(watch.IsShown(new MarkedActor(UndercityGoblin, 1500, 0, 0, 1, 10)));
        Assert.False(watch.IsShown(new MarkedActor(UndercityGoblin, 2100, 0, 0, 1, 10)));
        Assert.False(watch.IsShown(new MarkedActor(Cultist, 10, 0, 0, 1, 10)));
    }

    [Fact]
    public void The_debug_log_says_each_change_once_not_each_step_the_goblin_runs()
    {
        var log = new List<string>();
        var watch = NewWatch(log);
        watch.OnPlayerMoved(new PlayerState(0, 0, 0, 0));

        watch.OnMarkers([new MarkedActor(Goblin, 30, 0, 0, 1, 10, Flags: 0x34)]);
        watch.OnMarkers([new MarkedActor(Goblin, 35, 0, 0, 1, 10, Flags: 0x34)]);
        watch.OnActors([new ActorState(7, Goblin, 35, 0, 0)]);
        watch.OnMarkers([new MarkedActor(Goblin, 36, 0, 0, 1, 10, Flags: 0x34)]);
        watch.OnActors([]);
        watch.OnMarkers([]);

        Assert.Equal(3, log.Count);
        Assert.Contains("flags 0x0034 30 away streamed no shown yes", log[0]);
        Assert.Contains("streamed yes shown yes", log[1]);
        Assert.Equal("GOBLIN none", log[2]);
    }
}
