using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class ObjectiveTests
{
    private const int Monster = Conventions.ActorTypeMonster, Gizmo = 2, Prop = 3, NotGizmo = -1, DoorGizmo = 0;
    private const int PadSno = 1, MinibossSno = 2, LeverSno = 3, PrisonerNpcSno = 4, PrisonerSwitchSno = 5, DeadPrisonerCageSno = 6,
        CarryableSno = 7, ReceptacleSno = 8, QuestSwitchSno = 9, QuestChestSno = 10, DpoSwitchSno = 11, DpoMonsterSno = 12,
        PlainPropSno = 13, PrisonerDoorSno = 14, UnreadableSno = 15, BloodPetalsSno = 16, BonfireSno = 17, OtherReceptacleSno = 18,
        QuestNoteSno = 19, TownNoteSno = 20;
    private const int WorldA = 100, WorldB = 200;

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [PadSno] = ("X1_Undercity_WarpPad", new(Gizmo, Conventions.GizmoSwitch)),
        [MinibossSno] = ("ghost_caster_miniboss", new(Monster, NotGizmo)),
        [LeverSno] = ("Some_Lever", new(Gizmo, Conventions.GizmoSwitch)),
        [PrisonerNpcSno] = ("DGN_NPC_Frac_Prisoner_F_01", new(Monster, NotGizmo)),
        [PrisonerSwitchSno] = ("DGN_Switch_Standard_NPC_PrisonerEntangled_01", new(Gizmo, Conventions.GizmoSwitch)),
        [DeadPrisonerCageSno] = ("DGN_Standard_DeadPrisoner_Cage", new(Gizmo, DoorGizmo)),
        [CarryableSno] = ("Carryable_StoneCarving", new(Gizmo, Conventions.GizmoCarryable)),
        [ReceptacleSno] = ("Receptacle_StoneCarving", new(Gizmo, Conventions.GizmoCarryableReceptacle, AcceptedCarryables: [CarryableSno])),
        [OtherReceptacleSno] = ("Receptacle_Bloodstone", new(Gizmo, Conventions.GizmoCarryableReceptacle, AcceptedCarryables: [])),
        [QuestSwitchSno] = ("QST_Stake", new(Gizmo, Conventions.GizmoQuestSwitch)),
        [QuestChestSno] = ("QST_Chest", new(Gizmo, Conventions.GizmoQuestChest)),
        [DpoSwitchSno] = ("DGN_Standard_Sitting_Skeleton_Switch", new(Gizmo, DoorGizmo)),
        [DpoMonsterSno] = ("cannibal_brute_Unique", new(Monster, NotGizmo)),
        [PlainPropSno] = ("Plain_Prop", new(Prop, NotGizmo)),
        [PrisonerDoorSno] = ("DGN_Standard_Prisoner_Cage", new(Gizmo, DoorGizmo)),
        [BloodPetalsSno] = ("Ancients_Prop_Lilith_BloodPetal", new(Gizmo, Conventions.GizmoSwitch)),
        [BonfireSno] = ("Frac_Prologue_RitualBonfire", new(Gizmo, Conventions.GizmoSwitch)),
        [QuestNoteSno] = ("QST_Frac_Glacier_Zak_Triptych_03", new(Gizmo, Conventions.GizmoSign)),
        [TownNoteSno] = ("TWN_Frac_Kyovashad_Sign_Brazier", new(Gizmo, Conventions.GizmoSign)),
    };

    private static readonly IReadOnlySet<int> WorldADpo = new HashSet<int> { DpoSwitchSno, DpoMonsterSno, MinibossSno, UnreadableSno };

    private static ObjectiveClassifier Classifier() => new(
        sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, sno => Actors.TryGetValue(sno, out var a) ? a.Name : null);

    private static IReadOnlySet<int> Dpo(int world) => world == WorldA ? WorldADpo : new HashSet<int>();

    [Theory]
    [InlineData(CarryableSno, ObjectiveReason.Gizmo)]
    [InlineData(ReceptacleSno, ObjectiveReason.Gizmo)]
    [InlineData(QuestSwitchSno, ObjectiveReason.Gizmo)]
    [InlineData(QuestChestSno, ObjectiveReason.Gizmo)]
    [InlineData(PrisonerNpcSno, ObjectiveReason.Prisoner)]
    [InlineData(PrisonerSwitchSno, ObjectiveReason.Prisoner)]
    [InlineData(DpoSwitchSno, ObjectiveReason.Dpo)]
    [InlineData(BloodPetalsSno, ObjectiveReason.Gizmo)] // a plain switch, known by name
    [InlineData(BonfireSno, ObjectiveReason.Gizmo)]
    [InlineData(QuestNoteSno, ObjectiveReason.Gizmo)]   // a quest's message, known by name
    public void Classify_AcceptsObjectivesInTheirDungeon(int sno, ObjectiveReason expected) =>
        Assert.Equal(expected, Classifier().Classify(sno, WorldADpo));

    [Theory]
    [InlineData(MinibossSno)]         // kill target in the DPO list
    [InlineData(DpoMonsterSno)]
    [InlineData(LeverSno)]            // switch that is neither a prisoner nor in the list
    [InlineData(DeadPrisonerCageSno)] // dead prisoner, not eType 1 / switch
    [InlineData(PrisonerDoorSno)]     // door gizmo, not a switch or NPC
    [InlineData(PlainPropSno)]
    [InlineData(TownNoteSno)]         // an ordinary note stays a readable
    [InlineData(UnreadableSno)]       // no ActorDefinition record
    public void Classify_RejectsMonstersAndOrdinaryActors(int sno) =>
        Assert.Null(Classifier().Classify(sno, WorldADpo));

    [Fact]
    public void Classify_DpoActorsCountOnlyInTheirOwnWorld()
    {
        var classifier = Classifier();
        Assert.NotNull(classifier.Classify(DpoSwitchSno, Dpo(WorldA)));
        Assert.Null(classifier.Classify(DpoSwitchSno, Dpo(WorldB)));
        Assert.NotNull(classifier.Classify(CarryableSno, Dpo(WorldB))); // always-objectives ignore the list
    }

    [Fact]
    public void Classify_ADeadPrisonerNpcIsNotAPrisoner()
    {
        var classifier = new ObjectiveClassifier(_ => new ActorDefinitionInfo(Monster, NotGizmo), _ => "DGN_NPC_X_DeadPrisoner_M_01");
        Assert.Null(classifier.Classify(1, new HashSet<int>()));
    }

    [Fact]
    public void Selector_ExitPadKeepsTheBeaconProfile_ObjectivesGetTheQuestProfile()
    {
        var selector = Selector();
        selector.SetWorld(WorldA, isDungeon: true);

        var pad = selector.Select(Placed(PadSno));
        var objective = selector.Select(Placed(CarryableSno));

        Assert.Equal(LoopKind.ExitBeacon, pad!.Kind);
        Assert.Equal(LoopProfiles.Beacon, pad.Profile);
        Assert.Equal(SoundCue.Portal, pad.Cue);
        Assert.Equal(LoopKind.Objective, objective!.Kind);
        Assert.Equal(LoopProfiles.Quest, objective.Profile);
        Assert.Equal(SoundCue.QuestObjective, objective.Cue);
        Assert.Equal("gizmo", objective.Reason);
        Assert.Equal("dpo", selector.Select(Placed(DpoSwitchSno))!.Reason);
        Assert.Null(selector.Select(Placed(MinibossSno)));
    }

    [Fact]
    public void Selector_SetWorldRebuildsTheAllowList()
    {
        var selector = Selector();
        selector.SetWorld(WorldA, isDungeon: true);
        Assert.NotNull(selector.Select(Placed(DpoSwitchSno)));

        selector.SetWorld(WorldB, isDungeon: true);

        Assert.Null(selector.Select(Placed(DpoSwitchSno)));
    }

    [Fact]
    public void Selector_WithoutObjectivesLoopsOnlyTheBeacon()
    {
        var selector = new LoopTargetSelector(new HashSet<int> { PadSno }, objectives: null, Dpo);
        selector.SetWorld(WorldA, isDungeon: true);

        Assert.NotNull(selector.Select(Placed(PadSno)));
        Assert.Null(selector.Select(Placed(CarryableSno)));
    }

    [Fact]
    public void Selector_SilencesTheCarryableInThePlayersHands()
    {
        var selector = Selector();
        selector.SetWorld(WorldA, isDungeon: true);

        Assert.NotNull(selector.Select(Placed(CarryableSno)));
        Assert.Null(selector.Select(Placed(CarryableSno) with { ClientOnly = true }));
        Assert.NotNull(selector.Select(Placed(ReceptacleSno) with { ClientOnly = true })); // only the carried item goes silent
    }

    [Fact]
    public void Selector_WhileTheCarryableIsHeld_TheQuestLockedReceptacleTakingItIsTheQuestStep()
    {
        var selector = Selector();
        var held = new ActorState(1, CarryableSno, 0, 0, 0, ClientOnly: true);
        var altar = new ActorState(2, ReceptacleSno, 10, 0, 0, QuestLocked: true);
        var otherAltar = new ActorState(3, OtherReceptacleSno, 20, 0, 0, QuestLocked: true);
        var idolStand = new ActorState(4, QuestSwitchSno, 0, 0, 0, QuestStep: true); // the Glacier altar's Dummy, on the minimap

        var carrying = selector.WithCarryGoals([held, altar, otherAltar, idolStand]);
        var notCarrying = selector.WithCarryGoals([held with { ClientOnly = false }, altar, otherAltar, idolStand]);

        Assert.Equal([false, true, false, false], carrying.Select(a => a.QuestStep));
        Assert.Equal([false, false, true, true], carrying.Select(a => a.Unusable));
        Assert.Equal([false, true, true, false], notCarrying.Select(a => a.Unusable));
    }

    [Fact]
    public void Bridge_LoopsTheAltarInsteadOfTheIdolInTheHands()
    {
        var log = new List<string>();
        var (feed, player, _) = CreateBridge(log: log.Add);
        feed.RaiseWorld(WorldA);
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        var stand = new ActorState(3, QuestSwitchSno, 0, 0, 0, QuestStep: true);
        feed.RaiseActors(new ActorState(1, CarryableSno, 0, 0, 0), stand, new ActorState(2, ReceptacleSno, -10, -10, 0, QuestLocked: true));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(2, player.ActiveCount); // the idol and its stand

        feed.RaiseActors(new ActorState(1, CarryableSno, 0, 0, 0, ClientOnly: true), stand, new ActorState(2, ReceptacleSno, -10, -10, 0, QuestLocked: true));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        Assert.Equal(1, player.ActiveCount);
        Assert.Contains(log, l => l.Contains("started") && l.Contains("id=2") && l.Contains("reason=quest"));
        Assert.Contains(log, l => l.Contains("stopped") && l.Contains("id=3") && l.Contains("cause=unusable"));
    }

    [Fact]
    public void Bridge_HasTargetIsTrueForBeaconsOnly()
    {
        var (feed, player, bridge) = CreateBridge();
        feed.RaiseWorld(WorldA);
        feed.RaiseActors(new ActorState(1, CarryableSno, -10, -10, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        Assert.Equal(1, player.ActiveCount);
        Assert.True(bridge.HasTarget(LoopKind.Objective));
        Assert.False(bridge.HasTarget(LoopKind.ExitBeacon));

        feed.RaiseActors(new ActorState(1, CarryableSno, -10, -10, 0), new ActorState(2, PadSno, -10, -10, 0));
        Assert.True(bridge.HasTarget(LoopKind.ExitBeacon));
    }

    [Fact]
    public void Bridge_StopsAnObjectiveLoopWhenItIsUsed_AndLogsTheReasons()
    {
        var used = new HashSet<uint>();
        var log = new List<string>();
        var (feed, player, _) = CreateBridge(a => used.Contains(a.Id), log.Add);
        feed.RaiseWorld(WorldA);
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        var lever = new ActorState(3, DpoSwitchSno, -10, -10, 0);

        feed.RaiseActors(lever);
        feed.RaiseActors(lever); // no repeated line
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount);

        used.Add(3);
        feed.RaiseActors(lever);
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        Assert.Equal(0, player.ActiveCount);
        Assert.Equal(2, log.Count);
        Assert.Contains("started", log[0]);
        Assert.Contains("reason=dpo", log[0]);
        Assert.Contains("id=3", log[0]);
        Assert.Contains("stopped", log[1]);
        Assert.Contains("cause=used", log[1]);
    }

    [Fact]
    public void Bridge_AnActorLeavingTheListStopsWithCauseGone_AndAWorldChangeStopsEverything()
    {
        var log = new List<string>();
        var (feed, player, _) = CreateBridge(log: log.Add);
        feed.RaiseWorld(WorldA);
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        feed.RaiseActors(new ActorState(3, CarryableSno, -10, -10, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        feed.RaiseActors();
        Assert.Contains("cause=gone", log[1]);

        feed.RaiseActors(new ActorState(3, CarryableSno, -10, -10, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount);
        feed.RaiseWorld(WorldB);
        Assert.Equal(0, player.ActiveCount);
    }

    private static ActorState Placed(int sno) => new(1, sno, 0, 0, 0);

    private static LoopTargetSelector Selector() => new(new HashSet<int> { PadSno }, Classifier(), Dpo);

    private static (FakeFeed Feed, SpatialLoopPlayer Player, ActorLoopBridge Bridge) CreateBridge(
        Func<ActorState, bool>? isUsed = null, Action<string>? log = null)
    {
        var feed = new FakeFeed();
        var player = LoopTestKit.Player();
        var bridge = new ActorLoopBridge(
            feed, player, Selector(), sno => Actors.TryGetValue(sno, out var a) ? a.Name : null, isUsed, log);
        return (feed, player, bridge);
    }
}
