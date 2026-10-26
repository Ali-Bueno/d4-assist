using System.Numerics;
using D4Assist.Audio;
using D4Assist.Game;
using D4Memory.Actors;

namespace D4Assist.Tests;

public class BeaconTests
{
    private const int PadSno = 900, OtherSno = 901;

    [Fact]
    public void Resolve_KeepsKnownNamesAndReportsUnknownOnce()
    {
        var known = new Dictionary<string, int> { ["Pad"] = PadSno };
        var unknown = new List<string>();

        var snos = BeaconTargets.Resolve(
            ["Pad", "pad", "Nope", "Nope"],
            name => known.TryGetValue(name, out var sno) ? sno : throw new ArgumentException(name),
            unknown.Add);

        Assert.Equal([PadSno], snos);
        Assert.Equal(["Nope"], unknown);
    }

    [Fact]
    public void ActorFilter_KeepsOnlyPlacedActorsWithFinitePositionsOffTheOrigin()
    {
        var records = new[]
        {
            Record(1, Vector3.One, placement: 0),
            Record(2, Vector3.One, placement: -1),
            Record(3, new Vector3(float.NaN, 0, 0), placement: 0),
            Record(4, new Vector3(0, float.PositiveInfinity, 0), placement: 0),
            Record(5, Vector3.Zero, placement: 0),      // junk read while the world unloads or the game closes
            Record(6, new Vector3(0, 0, 1), placement: 0),
            Record(7, new Vector3(1e-30f, -1e-30f, 0), placement: 0),
            Record(8, new Vector3(1.1e21f, 1.8e31f, 0), placement: 0),
            Record(9, new Vector3(-3000, 2500, 40), placement: 0),
        };

        var placed = ActorFilter.Placed(records);

        Assert.Equal([1u, 6u, 9u], placed.Select(a => a.Id));
    }

    [Fact]
    public void Bridge_LoopsOnlyTargetsInRange_AndStopsOnWorldChange()
    {
        var (feed, player, _) = Create();
        feed.RaiseActors(new ActorState(1, PadSno, -20, -20, 0), new ActorState(2, OtherSno, -20, -20, 0));

        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount);

        feed.RaiseWorld();
        Assert.Equal(0, player.ActiveCount);

        feed.RaisePlayer(new PlayerState(0, 0, 0, 0)); // targets were cleared with the world
        Assert.Equal(0, player.ActiveCount);
    }

    [Fact]
    public void Bridge_StopsALoopWhenItsActorDisappearsOrTheListIsEmpty()
    {
        var (feed, player, _) = Create();
        feed.RaiseActors(new ActorState(1, PadSno, -20, -20, 0));
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount);

        feed.RaiseActors();
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        Assert.Equal(0, player.ActiveCount);
    }

    [Fact]
    public void ActorFilter_AQuestGizmoOnTheMinimapOrNamedByTheTrackedQuestIsTheQuestStep_ElseItIsUnusable()
    {
        const string quest = D4Memory.Actors.AcdFlagReader.GizmoQuestRestricted, minimap = D4Memory.Actors.AcdFlagReader.MinimapActive;
        var records = Enumerable.Range(1, 6).Select(id => Record((uint)id, Vector3.One, placement: 0)).ToArray();
        var flags = new Dictionary<uint, IReadOnlyDictionary<string, uint>>
        {
            [1] = Flags(quest),                                               // the locked quest gate
            [2] = Flags(quest, minimap),                                      // the demon body to use
            [3] = Flags(D4Memory.Actors.AcdFlagReader.GizmoDisabledByScript),
            [4] = Flags(minimap),                                             // an elevator
            [5] = Flags(quest),                                               // the gate, once the quest names it
            [6] = Flags(quest),                                               // the same gate placed elsewhere
        };
        var naming = new Dictionary<uint, QuestNaming> { [5] = QuestNaming.It, [6] = QuestNaming.ItsKind };

        var actors = ActorFilter.Placed(records, r => flags.GetValueOrDefault(r.Id), namedByQuest: r => naming.GetValueOrDefault(r.Id));

        Assert.Equal([true, false, true, false, false, true], actors.Select(a => a.Unusable));
        Assert.Equal([false, true, false, false, true, false], actors.Select(a => a.QuestStep));
    }

    [Fact]
    public void ActorFilter_AQuestGizmoTheMapMarksIsTheQuestStep()
    {
        // The warlock lair's binding altar keeps its lock flag as it opens; only the map marking it shows it (live 2026-10-19).
        var records = Enumerable.Range(1, 3).Select(id => Record((uint)id, Vector3.One, placement: 0)).ToArray();
        var quest = Flags(AcdFlagReader.GizmoQuestRestricted);
        var none = Flags();

        var actors = ActorFilter.Placed(records, r => r.Id == 3 ? none : quest, markedOnMap: r => r.Id != 1);

        Assert.Equal([true, false, false], actors.Select(a => a.Unusable));
        Assert.Equal([false, true, false], actors.Select(a => a.QuestStep)); // a marked gizmo without the lock is no quest step
    }

    [Fact]
    public void ActorFilter_AnNpcAQuestNamesIsTheQuestStepWhileItShowsAConversationIcon()
    {
        var records = Enumerable.Range(1, 6).Select(id => Record((uint)id, Vector3.One, placement: 0)).ToArray();
        var flags = new Dictionary<uint, IReadOnlyDictionary<string, uint>>
        {
            [1] = Flags(AcdFlagReader.ConversationIcon),                        // the NPC at its marker
            [2] = Flags(AcdFlagReader.ConversationIconQuest),                   // walked off its marker
            [3] = Flags(AcdFlagReader.NpcHasInteractOptions),                   // a vendor the quest names
            [4] = Flags(AcdFlagReader.ConversationIcon, AcdFlagReader.FollowerQuest), // a companion
            [5] = Flags(AcdFlagReader.ConversationIcon),                        // a quest giver no quest names
            [6] = Flags(AcdFlagReader.ConversationIcon),                        // named by its SNO alone
        };
        var naming = new Dictionary<uint, QuestNaming>
            { [1] = QuestNaming.It, [2] = QuestNaming.ItsKind, [3] = QuestNaming.It, [4] = QuestNaming.It, [6] = QuestNaming.It };

        var actors = ActorFilter.Placed(records, r => flags[r.Id], namedByQuest: r => naming.GetValueOrDefault(r.Id));

        Assert.Equal([true, true, false, false, false, true], actors.Select(a => a.QuestStep));
        Assert.All(actors, a => Assert.False(a.Unusable));
    }

    [Fact]
    public void ActorFilter_AnNpcWhoseConversationIconIsAFollowedQuestsIsTheQuestStep()
    {
        // "Speak with Raheir in the Den": quest 1478570's phase 140 names nobody (tester, 2026-10-23).
        const uint followed = 1478570, other = 577793;
        var records = Enumerable.Range(1, 4).Select(id => Record((uint)id, Vector3.One, placement: 0)).ToArray();
        var flags = new Dictionary<uint, IReadOnlyDictionary<string, uint>>
        {
            [1] = new Dictionary<string, uint> { [AcdFlagReader.ConversationIconQuest] = followed },
            [2] = new Dictionary<string, uint> { [AcdFlagReader.ConversationIconQuest] = other },
            [3] = new Dictionary<string, uint> { [AcdFlagReader.ConversationIconQuest] = followed, [AcdFlagReader.FollowerQuest] = followed },
            [4] = Flags(AcdFlagReader.ConversationIcon),
        };

        var actors = ActorFilter.Placed(records, r => flags[r.Id], followedQuests: [(int)followed]);

        Assert.Equal([true, false, false, false], actors.Select(a => a.QuestStep));
    }

    [Fact]
    public void ActorFilter_OnlyAnActorTheCurrentPhaseNamesItselfIsNamedByTheQuest()
    {
        var records = Enumerable.Range(1, 3).Select(id => Record((uint)id, Vector3.One, placement: 0)).ToArray();
        var naming = new Dictionary<uint, QuestNaming> { [1] = QuestNaming.It, [2] = QuestNaming.ItsKind };

        // A monster has no flags read, yet the phase may name it (the prologue tower's boss).
        var actors = ActorFilter.Placed(records, _ => null, namedByQuest: r => naming.GetValueOrDefault(r.Id));

        Assert.Equal([true, false, false], actors.Select(a => a.NamedByQuest));
        Assert.All(actors, a => Assert.False(a.QuestStep));
    }

    [Fact]
    public void ActorFilter_APreviousStateMarksTheActorChanged()
    {
        var used = Record(1, Vector3.One, placement: 0) with { PreviousState = 0x61 };

        Assert.Equal([true, false], ActorFilter.Placed([used, Record(2, Vector3.One, placement: 0)]).Select(a => a.StateChanged));
    }

    [Fact]
    public void ActorFilter_AnActorWithoutAGameRecordIsClientOnly()
    {
        var held = Record(1, Vector3.One, placement: 0) with { AcdId = RActorRecord.NoAcd };

        Assert.Equal([true, false], ActorFilter.Placed([held, Record(2, Vector3.One, placement: 0)]).Select(a => a.ClientOnly));
    }

    [Fact]
    public void ActorFilter_TheGizmoStateFlagMarksTheStateSet()
    {
        var records = new[] { Record(1, Vector3.One, placement: 0), Record(2, Vector3.One, placement: 0) };
        var open = Flags(AcdFlagReader.GizmoState);

        Assert.Equal([true, false], ActorFilter.Placed(records, r => r.Id == 1 ? open : null).Select(a => a.GizmoStateSet));
    }

    [Fact]
    public void ActorFilter_AnNpcWithInteractOptionsOrAConversationIconIsTalkable()
    {
        var records = Enumerable.Range(1, 4).Select(id => Record((uint)id, Vector3.One, placement: 0)).ToArray();
        var flags = new Dictionary<uint, IReadOnlyDictionary<string, uint>>
        {
            [1] = Flags(AcdFlagReader.NpcHasInteractOptions),
            [2] = Flags(AcdFlagReader.ConversationIcon),
            [3] = Flags(AcdFlagReader.ConversationIconQuest),
            [4] = Flags(), // a hell spirit: Conversation_Icon 0 is not set
        };

        Assert.Equal([true, true, true, false], ActorFilter.Placed(records, r => flags[r.Id]).Select(a => a.Talkable));
    }

    [Fact]
    public void Bridge_KeepsTheBeaconWhenThePadChangesState()
    {
        var (feed, player, _) = Create();
        feed.RaiseActors(new ActorState(1, PadSno, -20, -20, 0, StateChanged: true));

        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        Assert.Equal(1, player.ActiveCount);
    }

    [Fact]
    public void Bridge_IsSilentBeyondTheBeaconReach()
    {
        var (feed, player, _) = Create();
        feed.RaiseActors(new ActorState(1, PadSno, -500, -500, 0));

        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        Assert.Equal(0, player.ActiveCount);
    }

    [Fact]
    public void Bridge_LogsAppearAndDisappearOnce()
    {
        var log = new List<string>();
        var (feed, _, _) = Create(log.Add);
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));

        feed.RaiseActors(new ActorState(7, PadSno, -10, -10, 0));
        feed.RaiseActors(new ActorState(7, PadSno, -10, -10, 0));
        feed.RaiseActors();

        Assert.Equal(2, log.Count);
        Assert.Contains("started", log[0]);
        Assert.Contains("Pad", log[0]);
        Assert.Contains("id=7", log[0]);
        Assert.Contains("stopped", log[1]);
    }

    [Fact]
    public void GameSession_PollActorsOnce_RaisesActorsAndSkipsAnUnavailablePool()
    {
        var reader = new ActorReader();
        var feed = new SingleWorldSource("DGN_X");
        using var session = new GameSession(
            feed, new DungeonClassifier(), (_, _) => reader, new HashSet<int>(),
            Timeout.Infinite, Timeout.Infinite, Timeout.Infinite);
        var received = new List<int>();
        session.ActorsUpdated += a => received.Add(a.Count);

        reader.Actors = null;
        session.PollActorsOnce();
        reader.Actors = [new ActorState(1, PadSno, 0, 0, 0)];
        session.PollActorsOnce();

        Assert.Equal([1], received);
    }

    private static (FakeFeed Feed, SpatialLoopPlayer Player, ActorLoopBridge Bridge) Create(Action<string>? log = null)
    {
        var feed = new FakeFeed();
        var player = LoopTestKit.Player();
        var selector = new LoopTargetSelector(new HashSet<int> { PadSno }, objectives: null, _ => new HashSet<int>());
        var bridge = new ActorLoopBridge(feed, player, selector, sno => sno == PadSno ? "Pad" : null, log: log);
        return (feed, player, bridge);
    }

    private static RActorRecord Record(uint id, Vector3 position, int placement) =>
        new(SlotAddress: 0, id, AcdId: 0, ActorSno: PadSno, position, Quaternion.Identity, WorldId: 0, SceneId: 0, placement);

    // ACD flags set to 1, as AcdFlagReader.Values reports them.
    private static IReadOnlyDictionary<string, uint> Flags(params string[] names) => names.ToDictionary(name => name, _ => 1u);

    private sealed class ActorReader : IDungeonSessionReader
    {
        public IReadOnlyList<ActorState>? Actors { get; set; }
        public PlayerState? ReadPlayer() => new(0, 0, 0, 0);
        public IReadOnlyList<TileState>? ReadTilesViaRoute() => [];
        public IReadOnlyList<ActorState>? ReadActors() => Actors;
        public IReadOnlyList<TileState> ReadTilesViaFullScan() => [];
        public void Dispose() { }
    }

    private sealed class SingleWorldSource(string name) : IWorldNameSource
    {
        public event Action<WorldEntry>? WorldChanged { add { } remove { } }
        public WorldEntry? ReadLastWorldAtStartup() => new(name, null);
    }

    private sealed class DungeonClassifier : IWorldClassifier
    {
        public (int WorldSno, WorldKind Kind) Classify(string worldName) => (1, WorldKind.Dungeon);
    }
}
