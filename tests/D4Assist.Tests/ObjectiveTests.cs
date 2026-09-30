using D4Assist.Audio;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class ObjectiveTests
{
    private const int Monster = Conventions.ActorTypeMonster, Gizmo = 2, Prop = 3, NotGizmo = -1, DoorGizmo = 0;
    private const int PadSno = 1, MinibossSno = 2, LeverSno = 3, PrisonerNpcSno = 4, PrisonerSwitchSno = 5, DeadPrisonerCageSno = 6,
        CarryableSno = 7, ReceptacleSno = 8, QuestSwitchSno = 9, QuestChestSno = 10, DpoSwitchSno = 11, DpoMonsterSno = 12,
        PlainPropSno = 13, PrisonerDoorSno = 14, UnreadableSno = 15;
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
        [ReceptacleSno] = ("Receptacle_StoneCarving", new(Gizmo, Conventions.GizmoCarryableReceptacle)),
        [QuestSwitchSno] = ("QST_Stake", new(Gizmo, Conventions.GizmoQuestSwitch)),
        [QuestChestSno] = ("QST_Chest", new(Gizmo, Conventions.GizmoQuestChest)),
        [DpoSwitchSno] = ("DGN_Standard_Sitting_Skeleton_Switch", new(Gizmo, DoorGizmo)),
        [DpoMonsterSno] = ("cannibal_brute_Unique", new(Monster, NotGizmo)),
        [PlainPropSno] = ("Plain_Prop", new(Prop, NotGizmo)),
        [PrisonerDoorSno] = ("DGN_Standard_Prisoner_Cage", new(Gizmo, DoorGizmo)),
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
    public void Classify_AcceptsObjectivesInTheirDungeon(int sno, ObjectiveReason expected) =>
        Assert.Equal(expected, Classifier().Classify(sno, WorldADpo));

    [Theory]
    [InlineData(MinibossSno)]         // kill target in the DPO list
    [InlineData(DpoMonsterSno)]
    [InlineData(LeverSno)]            // switch that is neither a prisoner nor in the list
    [InlineData(DeadPrisonerCageSno)] // dead prisoner, not eType 1 / switch
    [InlineData(PrisonerDoorSno)]     // door gizmo, not a switch or NPC
    [InlineData(PlainPropSno)]
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
        var selector = Selector(beaconVolume: 0.5, objectiveVolume: 0.25);
        selector.SetWorld(WorldA, isDungeon: true);

        var pad = selector.Select(Placed(PadSno));
        var objective = selector.Select(Placed(CarryableSno));

        Assert.Equal(LoopKind.ExitBeacon, pad!.Kind);
        Assert.Equal(LoopProfiles.Beacon.NearGain * 0.5, pad.Profile.NearGain);
        Assert.Equal(LoopProfiles.Beacon.Reach, pad.Profile.Reach);
        Assert.Equal(LoopKind.Objective, objective!.Kind);
        Assert.Equal(LoopProfiles.Quest.NearGain * 0.25, objective.Profile.NearGain);
        Assert.Equal(LoopProfiles.Quest.Reach, objective.Profile.Reach);
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
        var selector = new LoopTargetSelector(new HashSet<int> { PadSno }, 1, objectives: null, 1, Dpo);
        selector.SetWorld(WorldA, isDungeon: true);

        Assert.NotNull(selector.Select(Placed(PadSno)));
        Assert.Null(selector.Select(Placed(CarryableSno)));
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

    private static LoopTargetSelector Selector(double beaconVolume = 1, double objectiveVolume = 1) =>
        new(new HashSet<int> { PadSno }, beaconVolume, Classifier(), objectiveVolume, Dpo);

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
