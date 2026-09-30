using D4Assist.Audio;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class InteractableTests
{
    private const int Gizmo = 2, NotGizmo = -1, Monster = Conventions.ActorTypeMonster;
    private const int DoorSno = 1, PortcullisSno = 2, BlockerSno = 3, StartOpenSno = 4, ChestSno = 5, RewardChestSno = 6, LockedChestSno = 7,
        SignSno = 8, LorebookChestSno = 9, BreakableSno = 10, PrisonerSno = 11, DpoDoorSno = 12, NpcSno = 13,
        CorpseChestSno = 14, CorpseSignSno = 15, RemainsSignSno = 16, HangedSignSno = 17;
    private const int World = 100;
    private const int BreakableContainerGizmo = 23;

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [DoorSno] = ("ProtoDun_Door", new(Gizmo, Conventions.GizmoDoor)),
        [PortcullisSno] = ("Prison_Prop_Portcullis_01_Dyn", new(Gizmo, Conventions.GizmoDoor)),
        [BlockerSno] = ("DRLG_Blocker_Mage_Halls_01", new(Gizmo, Conventions.GizmoDoor)),
        [StartOpenSno] = ("DGN_Hawe_Boss_Door_StartOpen", new(Gizmo, Conventions.GizmoDoor)),
        [ChestSno] = ("ProtoDunKeep_Chest_01", new(Gizmo, Conventions.GizmoChest)),
        [RewardChestSno] = ("ZE_Chest_Reward", new(Gizmo, Conventions.GizmoRewardChest)),
        [LockedChestSno] = ("Generic_Chest_Rare_Locked_GamblingCurrency", new(Gizmo, Conventions.GizmoLockedChest)),
        [SignSno] = ("QST_Sign_Frac_Notice", new(Gizmo, Conventions.GizmoSign)),
        [LorebookChestSno] = ("Generic_Lorebook_Chest_01_Dyn", new(Gizmo, Conventions.GizmoChest)),
        [BreakableSno] = ("Kurast_Prop_Basket_AnyLoot_01", new(Gizmo, BreakableContainerGizmo)),
        [PrisonerSno] = ("DGN_NPC_Frac_Prisoner_F_01", new(Monster, NotGizmo)),
        [DpoDoorSno] = ("DGN_Frac_KorValarRamparts_Door_Boss", new(Gizmo, Conventions.GizmoDoor)),
        [NpcSno] = ("Townsfolk", new(Monster, NotGizmo)),
        [CorpseChestSno] = ("corpse_knight_melee_frac", new(Gizmo, Conventions.GizmoChest)),
        [CorpseSignSno] = ("TWN_Frac_Margrave_Sign_MauledCorpse", new(Gizmo, Conventions.GizmoSign)),
        [HangedSignSno] = ("TWN_Frac_Yelesna_Sign_VampireHanged", new(Gizmo, Conventions.GizmoSign)),
        [RemainsSignSno] = ("QST_Class_Spiritborn_Skeletal_Remains_Sign", new(Gizmo, Conventions.GizmoSign)),
    };

    private static ActorDefinitionInfo? Info(int sno) => Actors.TryGetValue(sno, out var a) ? a.Info : null;
    private static string? Name(int sno) => Actors.TryGetValue(sno, out var a) ? a.Name : null;
    private static InteractableClassifier Classifier() => new(Info, Name);

    [Theory]
    [InlineData(DoorSno, InteractableKind.Door, InteractableClassifier.WoodenDoorSound)]
    [InlineData(PortcullisSno, InteractableKind.Door, InteractableClassifier.MetalDoorSound)]
    [InlineData(BlockerSno, InteractableKind.Door, InteractableClassifier.StoneDoorSound)]
    [InlineData(ChestSno, InteractableKind.Chest, InteractableClassifier.ChestSound)]
    [InlineData(RewardChestSno, InteractableKind.Chest, InteractableClassifier.ChestSound)]
    [InlineData(LockedChestSno, InteractableKind.Chest, InteractableClassifier.ChestSound)]
    [InlineData(SignSno, InteractableKind.Readable, InteractableClassifier.ScrollSound)]
    [InlineData(LorebookChestSno, InteractableKind.Readable, InteractableClassifier.ScrollSound)]
    [InlineData(CorpseChestSno, InteractableKind.Chest, InteractableClassifier.CorpseSound)]
    [InlineData(CorpseSignSno, InteractableKind.Readable, InteractableClassifier.CorpseSound)]
    [InlineData(RemainsSignSno, InteractableKind.Readable, InteractableClassifier.CorpseSound)]
    [InlineData(HangedSignSno, InteractableKind.Readable, InteractableClassifier.CorpseSound)]
    public void Classify_NamesTheKindAndSound(int sno, InteractableKind kind, string sound) =>
        Assert.Equal(new Interactable(kind, sound), Classifier().Classify(sno));

    [Theory]
    [InlineData(StartOpenSno)]  // opens by itself; D2R loops closed doors only
    [InlineData(BreakableSno)]  // breakables are not containers you open
    [InlineData(NpcSno)]
    [InlineData(999)]           // no ActorDefinition record
    public void Classify_IgnoresEverythingElse(int sno) => Assert.Null(Classifier().Classify(sno));

    [Fact]
    public void Selector_PrisonersLoopTheCorpseSound()
    {
        var target = Selector().Select(Placed(PrisonerSno));

        Assert.Equal(LoopKind.Objective, target!.Kind);
        Assert.Equal(InteractableClassifier.CorpseSound, target.Sound);
        Assert.Equal(LoopProfiles.Quest.Reach, target.Profile.Reach);
    }

    [Fact]
    public void Selector_InteractablesGetTheContainerProfileScaledByTheirVolume()
    {
        var selector = Selector(interactableVolume: 0.5);

        var door = selector.Select(Placed(PortcullisSno));
        var chest = selector.Select(Placed(ChestSno));
        var sign = selector.Select(Placed(SignSno));

        Assert.Equal((LoopKind.Door, InteractableClassifier.MetalDoorSound, "door"), (door!.Kind, door.Sound, door.Reason));
        Assert.Equal((LoopKind.Container, InteractableClassifier.ChestSound, "chest"), (chest!.Kind, chest.Sound, chest.Reason));
        Assert.Equal((LoopKind.Container, InteractableClassifier.ScrollSound, "readable"), (sign!.Kind, sign.Sound, sign.Reason));
        Assert.Equal(LoopProfiles.Container with { NearGain = LoopProfiles.Container.NearGain * 0.5 }, chest.Profile);
    }

    [Fact]
    public void Selector_AnObjectiveDoorKeepsTheQuestLoop()
    {
        var target = Selector().Select(Placed(DpoDoorSno));

        Assert.Equal(LoopKind.Objective, target!.Kind);
        Assert.Equal(LoopTargetSelector.QuestItemSound, target.Sound);
    }

    [Fact]
    public void Selector_SkipsDisabledKinds()
    {
        var selector = Selector(kinds: new HashSet<InteractableKind> { InteractableKind.Chest });

        Assert.NotNull(selector.Select(Placed(ChestSno)));
        Assert.Null(selector.Select(Placed(DoorSno)));
        Assert.Null(selector.Select(Placed(SignSno)));
    }

    [Fact]
    public void Bridge_SoundsOnlyTheNearestDoorsAndContainers()
    {
        var feed = new FakeFeed();
        var player = LoopTestKit.Player();
        _ = new ActorLoopBridge(feed, player, Selector(), Name);
        feed.RaiseWorld(World);

        feed.RaiseActors(
            new ActorState(1, DoorSno, 1, 0, 0), new ActorState(2, DoorSno, 2, 0, 0), new ActorState(3, DoorSno, 3, 0, 0),
            new ActorState(4, ChestSno, 0, 1, 0), new ActorState(5, ChestSno, 0, 2, 0), new ActorState(6, SignSno, 0, 3, 0),
            new ActorState(7, ChestSno, 0, 4, 0), new ActorState(8, PrisonerSno, 0, 9, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        Assert.Equal(ActorLoopBridge.MaxVoices(LoopKind.Door) + ActorLoopBridge.MaxVoices(LoopKind.Container) + 1, player.ActiveCount);
    }

    [Fact]
    public void Bridge_AUsedGizmoFallsSilent_ANpcPrisonerDoesNot()
    {
        var feed = new FakeFeed();
        var player = LoopTestKit.Player();
        _ = new ActorLoopBridge(feed, player, Selector(), Name);
        feed.RaiseWorld(World);
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        feed.RaiseActors(new ActorState(1, ChestSno, 1, 0, 0), new ActorState(2, PrisonerSno, 0, 1, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(2, player.ActiveCount);

        feed.RaiseActors(new ActorState(1, ChestSno, 1, 0, 0, StateChanged: true), new ActorState(2, PrisonerSno, 0, 1, 0, StateChanged: true));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount); // the prisoner NPC animates, so its state says nothing about being freed
    }

    [Fact]
    public void Bridge_AnUnusableGizmoStaysSilentUntilTheGameAllowsIt()
    {
        var feed = new FakeFeed();
        var player = LoopTestKit.Player();
        _ = new ActorLoopBridge(feed, player, Selector(), Name);
        feed.RaiseWorld(World);
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        feed.RaiseActors(new ActorState(1, DoorSno, 1, 0, 0, Unusable: true), new ActorState(2, PrisonerSno, 0, 1, 0, Unusable: true));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount); // the quest-restricted door is silent; NPC targets never stop

        feed.RaiseActors(new ActorState(1, DoorSno, 1, 0, 0), new ActorState(2, PrisonerSno, 0, 1, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(2, player.ActiveCount);
    }

    [Fact]
    public void Selector_TheQuestStepGetsTheQuestLoopWhateverItIs()
    {
        var target = Selector().Select(new ActorState(1, 99, 0, 0, 0, QuestStep: true)); // an unclassified gizmo

        Assert.Equal((LoopKind.Objective, LoopTargetSelector.QuestItemSound, "quest", true),
            (target!.Kind, target.Sound, target.Reason, target.StopsWhenUsed));
    }

    [Fact]
    public void Selector_OnlyGizmoTargetsStopWhenUsed()
    {
        var selector = Selector();

        Assert.True(selector.Select(Placed(ChestSno))!.StopsWhenUsed);
        Assert.True(selector.Select(Placed(DoorSno))!.StopsWhenUsed);
        Assert.True(selector.Select(Placed(DpoDoorSno))!.StopsWhenUsed);
        Assert.False(selector.Select(Placed(PrisonerSno))!.StopsWhenUsed);
    }

    [Fact]
    public void LoopPlayer_HigherPriorityClaimsTheBudgetBeforeNearerSources()
    {
        var mixer = new AudioMixer();
        var player = new SpatialLoopPlayer(mixer, new Dictionary<string, float[]> { ["s"] = Enumerable.Repeat(0.5f, 480).ToArray() });
        var far = new LoopSource("far", "s", 0, 20, LoopProfiles.Beacon, Priority: 0);
        var near = Enumerable.Range(0, AudioMixer.MaxLoopVoices)
            .Select(i => new LoopSource($"near{i}", "s", 0, 1, LoopProfiles.Beacon, Priority: 1));

        player.Update([.. near, far]);

        Assert.Equal(AudioMixer.MaxLoopVoices, player.ActiveCount);
        player.Update([far]);
        Assert.Equal(1, player.ActiveCount); // "far" kept its voice; had it been skipped, the full mixer would refuse it now
    }

    private static ActorState Placed(int sno) => new(1, sno, 0, 0, 0);

    private static LoopTargetSelector Selector(double interactableVolume = 1, IReadOnlySet<InteractableKind>? kinds = null)
    {
        var selector = new LoopTargetSelector(
            new HashSet<int>(), 1, new ObjectiveClassifier(Info, Name), 1,
            world => world == World ? new HashSet<int> { DpoDoorSno } : new HashSet<int>(),
            Classifier(), kinds, interactableVolume);
        selector.SetWorld(World, isDungeon: true);
        return selector;
    }
}
