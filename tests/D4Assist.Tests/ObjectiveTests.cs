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
        QuestNoteSno = 19, TownNoteSno = 20, HearthSno = 21, BeaconSno = 22, EnticementIconSno = 23, SkillOrbSno = 24, AkaratOrbSno = 25, AkaratPillarSno = 26, VesselSno = 27,
        TumorSno = 28, MummySno = 29, TumorPieceSno = 30, PlainMonsterSno = 31, CloneGateSno = 32, DpoTriggerSno = 33, SoulMarkerSno = 34, SoulRaiserSno = 35, SoulBeamSno = 36,
        ChainStationSno = 37, CapturePointSno = 38;
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
        [DpoSwitchSno] = ("DGN_Standard_Sitting_Skeleton_Switch", new(Gizmo, Conventions.GizmoSwitch)),
        [DpoMonsterSno] = ("cannibal_brute_Unique", new(Monster, NotGizmo)),
        [PlainPropSno] = ("Plain_Prop", new(Prop, NotGizmo)),
        [PrisonerDoorSno] = ("DGN_Standard_Prisoner_Cage", new(Gizmo, DoorGizmo)),
        [BloodPetalsSno] = ("Ancients_Prop_Lilith_BloodPetal", new(Gizmo, Conventions.GizmoSwitch)),
        [BonfireSno] = ("Frac_Prologue_RitualBonfire", new(Gizmo, Conventions.GizmoSwitch)),
        [QuestNoteSno] = ("QST_Frac_Glacier_Zak_Triptych_03", new(Gizmo, Conventions.GizmoSign)),
        [TownNoteSno] = ("TWN_Frac_Kyovashad_Sign_Brazier", new(Gizmo, Conventions.GizmoSign)),
        [HearthSno] = ("X1_Undercity_Enticement_SpiritHearth_Switch", new(Gizmo, Conventions.GizmoSwitch)),
        [BeaconSno] = ("X1_Undercity_Enticements_SpiritBeaconSwitch", new(Gizmo, Conventions.GizmoSwitch)),
        [EnticementIconSno] = ("X1_Undercity_Enticement_MapIcon", new(4, NotGizmo)),
        [SkillOrbSno] = ("Mephisto_Boss_P2_OrbActor", new(Monster, NotGizmo, DefaultTeam: Conventions.HostileTeam)),
        [AkaratOrbSno] = ("Akarat_Orb", new(Prop, NotGizmo)),
        [AkaratPillarSno] = ("Akarat_Orb_Pillar", new(Prop, NotGizmo)),
        [VesselSno] = ("QST_Skov_Skartara_WardBreakerShrine_Kulle", new(Gizmo, Conventions.GizmoSwitch)),
        [TumorSno] = ("Frac_Cursed_Tumor_01_Corner_2x2_01_Multistage", new(Monster, NotGizmo, DefaultTeam: Conventions.HostileTeam)),
        [MummySno] = ("MageHalls_Prop_Mummy_Mage_Any_1x1_01_Multistage", new(Monster, NotGizmo, DefaultTeam: Conventions.HostileTeam)),
        [TumorPieceSno] = ("Frac_Cursed_Tumor_Core_01_Dyn", new(Gizmo, 48)),
        [PlainMonsterSno] = ("werewolf_warg_phys", new(Monster, NotGizmo, DefaultTeam: Conventions.HostileTeam)),
        [CloneGateSno] = ("MorluGate_Triad_A_Boss_terrorCloneSource", new(Monster, NotGizmo, DefaultTeam: Conventions.HostileTeam)),
        [DpoTriggerSno] = ("Trigger_Sphere_8m", new(Gizmo, Conventions.GizmoTrigger, TriggerRadius: 4)),
        [SoulMarkerSno] = ("Rathma_Boss_SoulSplit_Location_Marker", new(Monster, NotGizmo, DefaultTeam: 19)),
        [SoulRaiserSno] = ("Rathma_SoulSplit_resurection_invisibleActor", new(Prop, NotGizmo)),
        [SoulBeamSno] = ("vfx_Rathmos_harmless_persistentCylindrical_SoulSplit_rune_lightbeams_02", new(Prop, NotGizmo)),
        [ChainStationSno] = ("inarius_chainStation", new(Gizmo, Conventions.GizmoChargeable)),
        [CapturePointSno] = ("Ancient_CapturePoint_Obelisk_01_Dyn", new(Gizmo, Conventions.GizmoChargeable)),
    };

    // Actors some quest names (DungeonObjectiveReader.ActorsNamedByAnyQuest): the hatred tumor, not the mausoleum's mummies;
    // Inarius's chain stations, not a capture point.
    private static readonly HashSet<int> QuestNamed = [TumorSno, TumorPieceSno, ChainStationSno];

    private static readonly IReadOnlySet<int> WorldADpo = new HashSet<int> { DpoSwitchSno, DpoMonsterSno, MinibossSno, UnreadableSno, DpoTriggerSno };

    private static ObjectiveClassifier Classifier() => new(
        sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, sno => Actors.TryGetValue(sno, out var a) ? a.Name : null, QuestNamed.Contains);

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
    [InlineData(HearthSno, ObjectiveReason.Enticement)]  // Undercity attunement switches, in no DPO list
    [InlineData(BeaconSno, ObjectiveReason.Enticement)]
    [InlineData(SkillOrbSno, ObjectiveReason.StolenSkill)] // a hostile monster, yet the player's to take back
    [InlineData(AkaratOrbSno, ObjectiveReason.AkaratLight)]
    [InlineData(SoulRaiserSno, ObjectiveReason.SoulBody)] // marks the body Rathma's soul split leaves behind, silent in the game
    [InlineData(VesselSno, ObjectiveReason.WardBreaker)]   // before the QST_ name rule
    [InlineData(TumorSno, ObjectiveReason.Destructible)]   // a hostile structure a quest asks the player to break
    [InlineData(MinibossSno, ObjectiveReason.KillTarget)]  // a monster the dungeon's objective asks the player to kill
    [InlineData(DpoMonsterSno, ObjectiveReason.KillTarget)]
    [InlineData(CloneGateSno, ObjectiveReason.CloneGate)] // a boss stays immune while its clone gates stand, in any world
    [InlineData(ChainStationSno, ObjectiveReason.Chargeable)] // a chargeable a quest names: Inarius's chains (user, 2026-10-21)
    public void Classify_AcceptsObjectivesInTheirDungeon(int sno, ObjectiveReason expected) =>
        Assert.Equal(expected, Classifier().Classify(sno, WorldADpo));

    [Theory]
    [InlineData(PlainMonsterSno)]     // a monster the DPO doesn't name
    [InlineData(LeverSno)]            // switch that is neither a prisoner nor in the list
    [InlineData(DeadPrisonerCageSno)] // dead prisoner, not eType 1 / switch
    [InlineData(PrisonerDoorSno)]     // door gizmo, not a switch or NPC
    [InlineData(PlainPropSno)]
    [InlineData(TownNoteSno)]         // an ordinary note stays a readable
    [InlineData(EnticementIconSno)]   // the attunement switch's minimap icon
    [InlineData(AkaratPillarSno)]     // the light's pillar, on the orb's own spot
    [InlineData(SoulMarkerSno)]       // five fixed spots round Rathma's arena from the fight's start (live 2026-10-20)
    [InlineData(UnreadableSno)]       // no ActorDefinition record
    [InlineData(MummySno)]            // a breakable prop no quest names
    [InlineData(TumorPieceSno)]       // the tumor's breakable pieces: destroyables, not the structure
    [InlineData(CapturePointSno)]     // a chargeable no quest names
    [InlineData(DpoTriggerSno)]       // an invisible "enter the trigger" zone the DPO names: looped forever at nothing (tester, 2026-10-05)
    public void Classify_RejectsMonstersAndOrdinaryActors(int sno) =>
        Assert.Null(Classifier().Classify(sno, WorldADpo));

    // The map lists a structure with the monsters (kind 10); one stays put, so its marker is its spot (the Weeping Cairns' altar,
    // tester log 2026-10-28).
    [Theory]
    [InlineData(TumorSno, true)]
    [InlineData(MummySno, true)]          // named by a quest or not: the reader asks about one the phase names
    [InlineData(PlainMonsterSno, false)]  // walks
    [InlineData(CloneGateSno, false)]
    [InlineData(TumorPieceSno, false)]    // a gizmo: the map marks it as one
    public void IsStructure_TellsBreakableStructuresFromMonsters(int sno, bool expected) =>
        Assert.Equal(expected, ObjectiveClassifier.IsStructure(Actors[sno].Name, Actors[sno].Info));

    [Fact]
    public void Classify_DpoActorsCountOnlyInTheirOwnWorld()
    {
        var classifier = Classifier();
        Assert.NotNull(classifier.Classify(DpoSwitchSno, Dpo(WorldA)));
        Assert.Null(classifier.Classify(DpoSwitchSno, Dpo(WorldB)));
        Assert.NotNull(classifier.Classify(CarryableSno, Dpo(WorldB))); // always-objectives ignore the list
    }

    [Fact]
    public void Classify_AQuestsPrisonerPropNoQuestNamesIsNotAPrisoner()
    {
        // Guulrahn's jail cells (tester 2026-10-28): the guide led to each of these before the jail's quest step.
        const int sno = 1058625;
        var classifier = new ObjectiveClassifier(_ => new ActorDefinitionInfo(Monster, NotGizmo), _ => "QST_Step_Guulrahn_Prisoner_M_01", _ => false);
        Assert.Null(classifier.Classify(sno, new HashSet<int>()));
    }

    [Fact]
    public void Classify_AQuestsPrisonerSomeQuestNamesIsAPrisoner()
    {
        const int sno = 1058625;
        var classifier = new ObjectiveClassifier(_ => new ActorDefinitionInfo(Monster, NotGizmo), _ => "QST_Step_Guulrahn_Prisoner_M_01", s => s == sno);
        Assert.Equal(ObjectiveReason.Prisoner, classifier.Classify(sno, new HashSet<int>()));
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
        Assert.Equal("killtarget", selector.Select(Placed(MinibossSno))!.Reason);
        Assert.False(selector.Select(Placed(MinibossSno))!.StopsWhenUsed); // loops until it dies and leaves the actor list
        // A companion following the player for the quest (live: S15_Warrior_NPC_Combat, which the hub quest names) never loops.
        Assert.Null(selector.Select(Placed(MinibossSno) with { QuestFollower = true, QuestStep = true }));
    }

    [Fact]
    public void Selector_TheSoulSplitBodyLoopsAtTheLightBeam_NotWithTheSpirit()
    {
        // Live 2026-10-20: the player was pulled into the beam at (-70.9,1.8); the resurrection actor appeared with the spirit
        // thrown 5.6 units off and followed it.
        var selector = Selector();
        selector.SetWorld(WorldA, isDungeon: true);
        var beam = new ActorState(4, SoulBeamSno, -70.9f, 1.8f, 1.6f, ClientOnly: true);
        var raiser = new ActorState(5, SoulRaiserSno, -68.0f, -2.1f, 1.5f, ClientOnly: true);

        var actors = selector.WithQuestGoals([beam, raiser]);
        Assert.Equal((-70.9f, 1.8f), (actors[1].X, actors[1].Y));
        actors = selector.WithQuestGoals([beam, raiser with { X = -63.1f, Y = -7.2f }]);
        Assert.Equal((-70.9f, 1.8f), (actors[1].X, actors[1].Y));
        Assert.Null(selector.Select(beam)); // the beam itself stays silent
    }

    [Fact]
    public void Selector_WithNoBeamTheSoulSplitBodyLoopsWhereItWasFirstSeen()
    {
        var selector = Selector();
        selector.SetWorld(WorldA, isDungeon: true);
        var raiser = new ActorState(5, SoulRaiserSno, -67.4f, -0.5f, 1.4f, ClientOnly: true);

        Assert.Equal((-67.4f, -0.5f), Position(selector.WithQuestGoals([raiser])));
        Assert.Equal((-67.4f, -0.5f), Position(selector.WithQuestGoals([raiser with { X = -62.3f, Y = -5.1f }])));
        Assert.Equal("soulbody", selector.Select(raiser)!.Reason);

        selector.WithQuestGoals([]); // the split is over
        Assert.Equal((-62.3f, -5.1f), Position(selector.WithQuestGoals([raiser with { X = -62.3f, Y = -5.1f }])));

        static (float, float) Position(IReadOnlyList<ActorState> actors) => (Assert.Single(actors).X, actors[0].Y);
    }

    [Fact]
    public void Selector_AttunementSwitchesHaveTheirOwnCueAndFallSilentOnceUsed()
    {
        var selector = Selector();
        selector.SetWorld(WorldB, isDungeon: true);

        var hearth = selector.Select(Placed(HearthSno))!;

        Assert.Equal(LoopKind.Objective, hearth.Kind);
        Assert.Equal(SoundCue.Enticement, hearth.Cue);
        Assert.Equal(LoopTargetSelector.EnticementSound, hearth.Sound);
        Assert.True(hearth.StopsWhenUsed);
    }

    [Fact]
    public void Selector_StolenSkillOrbsLoopUntilTheyAreGone()
    {
        var selector = Selector();
        selector.SetWorld(WorldB, isDungeon: true);

        var orb = selector.Select(Placed(SkillOrbSno))!;

        Assert.Equal(SoundCue.StolenSkill, orb.Cue);
        Assert.False(orb.StopsWhenUsed); // a monster: no gizmo "used" signal; it leaves the actor list when taken
        Assert.True(Classifier().IsStolenSkill(SkillOrbSno));
        Assert.False(Classifier().IsStolenSkill(HearthSno));
    }

    [Fact]
    public void Selector_AkaratsLightsLoopUntilTheyAreGone_TheVesselsSurviveTheirUse()
    {
        var selector = Selector();
        selector.SetWorld(WorldB, isDungeon: true);

        var light = selector.Select(Placed(AkaratOrbSno))!;
        var vessel = selector.Select(Placed(VesselSno))!;

        Assert.Equal(SoundCue.AkaratLight, light.Cue);
        Assert.False(light.StopsWhenUsed); // an effect: no gizmo "used" signal
        Assert.Equal(SoundCue.WardBreaker, vessel.Cue);
        Assert.False(vessel.StopsWhenUsed);
        Assert.True(vessel.Reusable);
    }

    [Fact]
    public void Bridge_AReusableVesselKeepsLoopingOnceUsed_ButNotWhileTheGameLocksIt()
    {
        var (feed, player, _) = CreateBridge(_ => true); // every actor reads as used
        feed.RaiseWorld(WorldB);

        feed.RaiseActors(new ActorState(1, VesselSno, 5, 0, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount);

        feed.RaiseActors(new ActorState(1, VesselSno, 5, 0, 0, ScriptDisabled: true));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(0, player.ActiveCount);
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
    public void Selector_SilencesADestroyedStructure()
    {
        var selector = Selector();
        selector.SetWorld(WorldB, isDungeon: true);

        // Live 2026-10-01 (Ancient Mausoleum): a destroyed tumor stays in the actor list without its game record (ACD).
        var tumor = Assert.IsType<LoopTarget>(selector.Select(Placed(TumorSno)));
        Assert.Equal(LoopKind.Objective, tumor.Kind);
        Assert.Equal(LoopTargetSelector.QuestItemSound, tumor.Sound);
        Assert.Null(selector.Select(Placed(TumorSno) with { ClientOnly = true }));
    }

    [Fact]
    public void Selector_WhileTheCarryableIsHeld_TheQuestLockedReceptacleTakingItIsTheQuestStep()
    {
        var selector = Selector();
        var held = new ActorState(1, CarryableSno, 0, 0, 0, ClientOnly: true);
        var altar = new ActorState(2, ReceptacleSno, 10, 0, 0, QuestLocked: true);
        var otherAltar = new ActorState(3, OtherReceptacleSno, 20, 0, 0, QuestLocked: true);
        var idolStand = new ActorState(4, QuestSwitchSno, 0, 0, 0, QuestStep: true); // the Glacier altar's Dummy, on the minimap

        var carrying = selector.WithQuestGoals([held, altar, otherAltar, idolStand]);
        var notCarrying = selector.WithQuestGoals([held with { ClientOnly = false }, altar, otherAltar, idolStand]);

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

    [Fact]
    public void Bridge_AFreedPrisonerWalkingBesideThePlayerFallsSilentAndStopsLeading_ACagedOneTheyCircleKeepsLooping()
    {
        var log = new List<string>();
        var (feed, player, bridge) = CreateBridge(log: log.Add);
        var memory = new ObjectiveMemory(feed, sno => Actors.TryGetValue(sno, out var a) ? a.Name : null);
        bridge.ObjectivesSeen += memory.Observe;
        feed.RaiseWorld(WorldA);
        var prisoner = new ActorState(5, PrisonerNpcSno, 0, 0, 0);
        // Fighting around the cage: the player moves, the prisoner doesn't.
        foreach (var (x, y) in new[] { (3f, 0f), (0f, 6f), (-6f, 0f), (0f, -6f), (6f, 0f) })
        {
            feed.RaisePlayer(new PlayerState(x, y, 0, 0));
            feed.RaiseActors(prisoner);
        }
        feed.RaisePlayer(new PlayerState(6, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount);
        Assert.Single(memory.Destinations());

        // Freed, it walks at the player's heels (tester, 2026-10-19: "it just kept following my character").
        for (float x = 6; x <= 30; x += 4)
        {
            feed.RaisePlayer(new PlayerState(x, 0, 0, 0));
            feed.RaiseActors(prisoner with { X = x - 2 });
        }
        feed.RaisePlayer(new PlayerState(30, 0, 0, 0));

        Assert.Equal(0, player.ActiveCount);
        Assert.Contains(log, line => line.Contains("stopped") && line.Contains("cause=follows"));
        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void Bridge_AFollowersNewCopyBesideThePlayerIsSilentAtOnce_OneOfItsKindFurtherOnStillLoops()
    {
        var (feed, player, _) = CreateBridge();
        feed.RaiseWorld(WorldA);
        for (float x = 0; x <= 12; x += 4)
        {
            feed.RaisePlayer(new PlayerState(x, 0, 0, 0));
            feed.RaiseActors(new ActorState(5, PrisonerNpcSno, x - 2, 0, 0));
        }

        // Through a portal the game re-creates it beside the player; a caged one of the same kind waits further on.
        feed.RaisePlayer(new PlayerState(100, 100, 0, 0));
        feed.RaiseActors(new ActorState(6, PrisonerNpcSno, 102, 100, 0), new ActorState(7, PrisonerNpcSno, 120, 100, 0));
        feed.RaisePlayer(new PlayerState(100, 100, 0, 0));

        Assert.Equal(1, player.ActiveCount);
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
