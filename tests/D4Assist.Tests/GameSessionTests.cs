using D4Assist.Game;

namespace D4Assist.Tests;

public class GameSessionTests
{
    private const string OverworldName = "Sanctuary_Eastern_Continent";
    private const int OverworldSno = 1;
    private const string DungeonName = "DGN_Hawe_HeathensKeep";
    private const int DungeonSno = 843152;

    private static readonly IReadOnlySet<int> SceneSnos = new HashSet<int> { 1234 };

    [Fact]
    public void StartsInWaitingForGame_WhenTheFeedHasNoStartupWorld()
    {
        var session = NewSession(new FakeWorldNameSource(), new FakeClassifier());

        Assert.Equal(SessionState.WaitingForGame, session.State);
    }

    [Fact]
    public void FirstWorldChanged_ToANonDungeon_MovesToIdleAndFiresWorldChanged()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(OverworldName, OverworldSno, isDungeon: false);
        var events = new List<(int, string, bool)>();
        var session = NewSession(feed, classifier);
        session.WorldChanged += (sno, name, dungeon) => events.Add((sno, name, dungeon));

        feed.Raise(OverworldName);

        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal([(OverworldSno, OverworldName, false)], events);
    }

    [Fact]
    public void WorldChangedToADungeon_MovesToInDungeonAndOpensASession()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader();
        var session = NewSession(feed, classifier, (_, _) => reader);

        feed.Raise(DungeonName);

        Assert.Equal(SessionState.InDungeon, session.State);
    }

    [Fact]
    public void RepeatedSameWorld_IsDiffGatedAndDoesNotReopenTheSession()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        int openCount = 0;
        var session = NewSession(feed, classifier, (_, _) => { openCount++; return new FakeDungeonSessionReader(); });

        feed.Raise(DungeonName);
        feed.Raise(DungeonName);

        Assert.Equal(1, openCount);
    }

    [Fact]
    public void LeavingADungeon_DisposesTheSessionAndFiresWorldChanged()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier()
            .Map(DungeonName, DungeonSno, isDungeon: true)
            .Map(OverworldName, OverworldSno, isDungeon: false);
        var reader = new FakeDungeonSessionReader();
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);

        feed.Raise(OverworldName);

        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(1, reader.DisposeCount);
    }

    [Fact]
    public void OpenSessionReturningNull_StaysIdleInstead_OfInDungeon()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var session = NewSession(feed, classifier, (_, _) => null);

        feed.Raise(DungeonName);

        Assert.Equal(SessionState.Idle, session.State);
    }

    [Fact]
    public void OpenSessionThrowingInvalidOperationException_StaysIdleInsteadOfCrashing()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var session = NewSession(feed, classifier,
            (_, _) => throw new InvalidOperationException("Diablo IV not running"));

        feed.Raise(DungeonName);

        Assert.Equal(SessionState.Idle, session.State);
    }

    [Fact]
    public void PollPlayerOnce_FiresPlayerMovedWhileInDungeon()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader { NextPlayer = new PlayerState(1f, 2f, 3f, 1234) };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);

        PlayerState? seen = null;
        session.PlayerMoved += p => seen = p;
        session.PollPlayerOnce();

        Assert.Equal(reader.NextPlayer, seen);
    }

    [Fact]
    public void PollPlayerOnce_NullPlayer_LeavesTheDungeon()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader { NextPlayer = null };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);

        session.PollPlayerOnce();

        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(1, reader.DisposeCount);
    }

    [Fact]
    public void PollTilesOnce_RouteAvailable_FiresTilesUpdatedAndNeverRunsTheFallbackScan()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var routeTiles = new List<TileState> { new(1234, "T", 0, 0, 32, 32, 15, 0, 0) };
        var reader = new FakeDungeonSessionReader { NextRouteTiles = routeTiles };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);

        IReadOnlyList<TileState>? seen = null;
        session.TilesUpdated += t => seen = t;
        session.PollTilesOnce();

        Assert.Same(routeTiles, seen);
        Assert.Equal(0, reader.FullScanCount);
    }

    [Fact]
    public void PollTilesOnce_RouteUnavailable_RunsTheFallbackScanExactlyOncePerEntry()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader { NextRouteTiles = null };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);

        session.PollTilesOnce();
        session.PollTilesOnce(); // route still unavailable; must not trigger a second background scan

        Assert.True(SpinWait.SpinUntil(() => reader.FullScanCount >= 1, TimeSpan.FromSeconds(2)));
        Thread.Sleep(50); // let a wrongly-triggered second scan have a chance to land, if there is a bug
        Assert.Equal(1, reader.FullScanCount);
    }

    [Fact]
    public void RescanTiles_RunsAnotherFullScanOnDemand()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader { NextRouteTiles = null };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);
        session.PollTilesOnce();
        Assert.True(SpinWait.SpinUntil(() => reader.FullScanCount >= 1, TimeSpan.FromSeconds(2)));

        session.RescanTiles();

        Assert.True(SpinWait.SpinUntil(() => reader.FullScanCount >= 2, TimeSpan.FromSeconds(2)));
    }

    private static GameSession NewSession(
        FakeWorldNameSource feed, FakeClassifier classifier,
        Func<int, IReadOnlySet<int>, IDungeonSessionReader?>? openSession = null) =>
        new(feed, classifier, openSession ?? ((_, _) => new FakeDungeonSessionReader()), SceneSnos,
            playerPollIntervalMs: Timeout.Infinite, tilePollIntervalMs: Timeout.Infinite);

    private sealed class FakeWorldNameSource : IWorldNameSource
    {
        public event Action<string>? WorldChanged;
        public string? StartupWorld { get; set; }
        public string? ReadLastWorldNameAtStartup() => StartupWorld;
        public void Raise(string worldName) => WorldChanged?.Invoke(worldName);
    }

    private sealed class FakeClassifier : IWorldClassifier
    {
        private readonly Dictionary<string, (int Sno, bool IsDungeon)> _map = [];

        public FakeClassifier Map(string worldName, int sno, bool isDungeon)
        {
            _map[worldName] = (sno, isDungeon);
            return this;
        }

        public (int WorldSno, bool IsDungeon) Classify(string worldName) =>
            _map.TryGetValue(worldName, out var v) ? v : throw new ArgumentException($"Unknown world '{worldName}'.");
    }

    private sealed class FakeDungeonSessionReader : IDungeonSessionReader
    {
        public int DisposeCount;
        public int FullScanCount;
        public PlayerState? NextPlayer { get; set; } = new(0, 0, 0, 0);
        public IReadOnlyList<TileState>? NextRouteTiles { get; set; } = [];

        public PlayerState? ReadPlayer() => NextPlayer;
        public IReadOnlyList<TileState>? ReadTilesViaRoute() => NextRouteTiles;

        public IReadOnlyList<TileState> ReadTilesViaFullScan()
        {
            Interlocked.Increment(ref FullScanCount);
            return [];
        }

        public void Dispose() => DisposeCount++;
    }
}
