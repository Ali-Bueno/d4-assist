using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

/// <summary>Quest steps the game's flags don't mark: receptacles, the doors they open, boss monoliths, bosses a quest names.</summary>
public class QuestGoalTests
{
    private const int Monster = Conventions.ActorTypeMonster, Gizmo = 2, NotGizmo = -1;
    private const int StatueSno = 1, PedestalSno = 2, CarvingSno = 3, DoorSno = 4, DpoDoorSno = 5, MonolithSno = 6, BossSno = 7,
        UniqueSno = 8, ZombieSno = 9, LeverSno = 10;
    private const int DpoWorld = 100, OtherWorld = 200;
    private const float Near = CarryGoals.ReceptacleDoorUnits - 1, Far = CarryGoals.ReceptacleDoorUnits + 1;

    // Hakan's Refuge, Ancients' Lament, the Shifting City: two statue pedestals beside a blocker (research/dungeon-objectives.md §14).
    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [StatueSno] = ("Carryable_AncientsStatue", new(Gizmo, Conventions.GizmoCarryable)),
        [PedestalSno] = ("Receptacle_AncientsStatue", new(Gizmo, Conventions.GizmoCarryableReceptacle, AcceptedCarryables: [StatueSno])),
        [CarvingSno] = ("Carryable_StoneCarving", new(Gizmo, Conventions.GizmoCarryable)),
        [DoorSno] = ("DRLG_Blocker_Ancients_Sand_01", new(Gizmo, Conventions.GizmoDoor)),
        [DpoDoorSno] = ("DGN_Frac_KorValarRamparts_Door_Boss", new(Gizmo, Conventions.GizmoDoor)),
        [MonolithSno] = ("SilentMonolith_DYN", new(Gizmo, Conventions.GizmoDoor)),
        [BossSno] = ("Frac_Prologue_demon_mega_miniboss",
            new(Monster, NotGizmo, DefaultTeam: Conventions.HostileTeam, MonsterRarity: Conventions.MonsterRarityBoss)),
        [UniqueSno] = ("goatman_brute_frac_unique", new(Monster, NotGizmo, DefaultTeam: Conventions.HostileTeam, MonsterRarity: Conventions.MonsterRarityUnique)),
        [ZombieSno] = ("zombie_brute_phys", new(Monster, NotGizmo, DefaultTeam: Conventions.HostileTeam)),
        [LeverSno] = ("DGN_FloodedDepths_Lever_01_Dyn", new(Gizmo, Conventions.GizmoSwitch)),
    };

    private static string? NameOf(int sno) => Actors.TryGetValue(sno, out var a) ? a.Name : null;

    private static ObjectiveClassifier Classifier() => new(sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, NameOf);

    private static LoopTargetSelector Selector(int world = OtherWorld)
    {
        var selector = new LoopTargetSelector(
            new HashSet<int>(), Classifier(), w => w == DpoWorld ? new HashSet<int> { DpoDoorSno } : new HashSet<int>());
        selector.SetWorld(world, isDungeon: true);
        return selector;
    }

    private static ActorState Pedestal(float x, uint id = 10, bool filled = false) => new(id, PedestalSno, x, 0, 0, Operated: filled);
    private static ActorState Door(int sno = DoorSno, float x = 0, bool questStep = true) => new(20, sno, x, 0, 0, QuestStep: questStep);
    private static ActorState Held(int sno = StatueSno) => new(30, sno, 0, 0, 0, ClientOnly: true);

    [Fact]
    public void A_receptacle_leads_only_while_the_player_holds_what_it_takes()
    {
        var selector = Selector();

        selector.WithQuestGoals([Pedestal(40)]);
        Assert.Null(selector.ReceptacleTakesHeld(StatueSno)); // not a receptacle
        Assert.False(selector.ReceptacleTakesHeld(PedestalSno));

        selector.WithQuestGoals([Pedestal(40), Held(CarvingSno)]);
        Assert.False(selector.ReceptacleTakesHeld(PedestalSno)); // the wrong item

        selector.WithQuestGoals([Pedestal(40), Held()]);
        Assert.True(selector.ReceptacleTakesHeld(PedestalSno));

        selector.SetWorld(OtherWorld, isDungeon: true);
        Assert.False(selector.ReceptacleTakesHeld(PedestalSno));
    }

    [Fact]
    public void A_door_beside_an_empty_receptacle_waits_for_it_whatever_its_quest_flags()
    {
        var selector = Selector();

        var waiting = selector.WithQuestGoals([Pedestal(Near), Pedestal(-Near, id: 11, filled: true), Door(), Door(questStep: false) with { Id = 21 }]);

        Assert.All(waiting.Where(a => a.ActorSno == DoorSno), door => Assert.True(door.Unusable && !door.QuestStep));
    }

    [Theory]
    [InlineData(DoorSno, Far, false, false)]  // the next room's door
    [InlineData(DoorSno, Near, true, false)]  // the receptacle is filled: the door opens by script
    [InlineData(DoorSno, Near, false, true)]  // already open
    [InlineData(DpoDoorSno, Near, false, false)] // the dungeon's objective names it: the player opens it
    public void Other_doors_keep_their_quest_step(int doorSno, float distance, bool filled, bool open)
    {
        var selector = Selector(DpoWorld);

        var actors = selector.WithQuestGoals([Pedestal(distance, filled: filled), Door(doorSno) with { GizmoStateSet = open }]);

        Assert.True(actors[1].QuestStep);
    }

    [Fact]
    public void A_monolith_is_an_objective_in_every_dungeon_and_is_used_once_operated_or_open()
    {
        var target = Selector().Select(new ActorState(1, MonolithSno, 0, 0, 0))!;

        Assert.Equal(ObjectiveReason.Monolith, Classifier().Classify(MonolithSno, new HashSet<int>()));
        Assert.Equal("monolith", target.Reason);
        Assert.True(target.StopsWhenUsed);
        Assert.True(target.UsedWhenOpened);
        Assert.False(Selector().Select(new ActorState(1, LeverSno, 0, 0, 0) with { QuestStep = true })!.UsedWhenOpened);
    }

    [Fact]
    public void Bridge_a_door_objective_ignores_the_state_word_and_stops_on_the_games_marks()
    {
        var log = new List<string>();
        var (feed, player) = Bridge(log);
        var monolith = new ActorState(1, MonolithSno, 5, 0, 0, StateChanged: true);

        feed.RaiseActors(monolith);
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount);

        feed.RaiseActors(monolith with { Operated = true });
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(0, player.ActiveCount);
        Assert.Contains(log, l => l.Contains("stopped") && l.Contains("cause=used"));
    }

    [Fact]
    public void Bridge_logs_a_gizmo_already_used_at_first_sight_once()
    {
        var log = new List<string>();
        var (feed, _) = Bridge(log);
        var used = new ActorState(1, MonolithSno, 5, 0, 0, GizmoStateSet: true);

        feed.RaiseActors(used);
        feed.RaiseActors(used);

        Assert.Single(log, l => l.Contains("LOOP silent") && l.Contains("cause=used") && l.Contains("SilentMonolith_DYN"));
    }

    [Fact]
    public void A_boss_or_unique_the_current_phase_names_is_a_quest_step_until_it_dies()
    {
        var selector = Selector();
        var boss = new ActorState(1, BossSno, 0, 0, 0, NamedByQuest: true);

        var actors = selector.WithQuestGoals([boss, new ActorState(2, UniqueSno, 0, 0, 0, NamedByQuest: true), new ActorState(3, BossSno, 0, 0, 0)]);

        Assert.Equal([true, true, false], actors.Select(a => a.QuestStep));
        var target = selector.Select(actors[0])!;
        Assert.Equal("killtarget", target.Reason);
        Assert.False(target.StopsWhenUsed); // loops until it leaves the actor list
        Assert.Null(selector.Select(selector.WithQuestGoals([boss with { ClientOnly = true }])[0])); // its remains
    }

    [Fact]
    public void An_ordinary_monster_a_phase_names_stays_silent()
    {
        var selector = Selector();

        var zombie = selector.WithQuestGoals([new ActorState(1, ZombieSno, 0, 0, 0, NamedByQuest: true)])[0];

        Assert.False(zombie.QuestStep);
        Assert.Null(selector.Select(zombie));
    }

    private static (FakeFeed Feed, D4Assist.Audio.SpatialLoopPlayer Player) Bridge(List<string> log)
    {
        var feed = new FakeFeed();
        var player = LoopTestKit.Player();
        _ = new ActorLoopBridge(feed, player, Selector(), NameOf, isUsed: _ => true, log: log.Add);
        feed.RaiseWorld(OtherWorld);
        return (feed, player);
    }
}
