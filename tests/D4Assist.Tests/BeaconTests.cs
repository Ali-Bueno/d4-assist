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
    public void ActorFilter_KeepsOnlyPlacedActorsWithFinitePositions()
    {
        var records = new[]
        {
            Record(1, Vector3.One, placement: 0),
            Record(2, Vector3.One, placement: -1),
            Record(3, new Vector3(float.NaN, 0, 0), placement: 0),
            Record(4, new Vector3(0, float.PositiveInfinity, 0), placement: 0),
        };

        var placed = ActorFilter.Placed(records);

        Assert.Equal([1u], placed.Select(a => a.Id));
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
        var selector = new LoopTargetSelector(new HashSet<int> { PadSno }, 1.0, objectives: null, 1.0, _ => new HashSet<int>());
        var bridge = new ActorLoopBridge(feed, player, selector, sno => sno == PadSno ? "Pad" : null, log: log);
        return (feed, player, bridge);
    }

    private static RActorRecord Record(uint id, Vector3 position, int placement) =>
        new(SlotAddress: 0, id, AcdId: 0, ActorSno: PadSno, position, Quaternion.Identity, WorldId: 0, SceneId: 0, placement);

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
        public event Action<string>? WorldChanged { add { } remove { } }
        public string? ReadLastWorldNameAtStartup() => name;
    }

    private sealed class DungeonClassifier : IWorldClassifier
    {
        public (int WorldSno, WorldKind Kind) Classify(string worldName) => (1, WorldKind.Dungeon);
    }
}
