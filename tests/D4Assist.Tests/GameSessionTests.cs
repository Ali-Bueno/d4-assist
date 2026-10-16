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
    public void OpenFailingWhileThePlayerIsStillLoading_IsRetriedUntilItReads()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        int openCount = 0;
        var session = NewSession(feed, classifier, (_, _) => ++openCount < 3
            ? throw new InvalidOperationException("player not found")
            : new FakeDungeonSessionReader());

        feed.Raise(DungeonName);
        session.RetryOpenOnce();
        Assert.Equal(SessionState.Idle, session.State);
        session.RetryOpenOnce();

        Assert.Equal(SessionState.InDungeon, session.State);
        session.RetryOpenOnce();
        Assert.Equal(3, openCount);
    }

    [Fact]
    public void FailedOpen_IsNotRetriedAfterLeavingForAWorldWithoutReads()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier()
            .Map(DungeonName, DungeonSno, isDungeon: true)
            .Map(OverworldName, OverworldSno, isDungeon: false);
        int openCount = 0;
        var session = NewSession(feed, classifier, (_, _) => { openCount++; return null; });

        feed.Raise(DungeonName);
        feed.Raise(OverworldName);
        session.RetryOpenOnce();

        Assert.Equal(1, openCount);
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
    public void WarpInsideTheSameInstance_KeepsTheWorldAndReopensTheSession()
    {
        var (feed, session, events, transit, opens) = TransitSession();

        feed.Raise(DungeonName, "1048578");
        feed.Raise(GameSession.TransitWorldName, "1572864");
        Assert.Equal(SessionState.Idle, session.State);
        feed.Raise(DungeonName, "1048578");

        Assert.Equal([DungeonName], events);
        Assert.Equal([true, false], transit);
        Assert.Equal(2, opens());
        Assert.Equal(SessionState.InDungeon, session.State);
    }

    [Fact]
    public void SameWorldInANewInstanceAfterLoading_IsANewWorld()
    {
        var (feed, _, events, transit, _) = TransitSession();

        feed.Raise(DungeonName, "524290");
        feed.Raise(GameSession.TransitWorldName, "524288");
        feed.Raise(DungeonName, "1048578");

        Assert.Equal([DungeonName, DungeonName], events);
        Assert.Equal([true, false], transit);
    }

    [Fact]
    public void LoadingIntoAnotherWorld_SkipsTheLoadingWorld()
    {
        var (feed, _, events, transit, _) = TransitSession();

        feed.Raise(DungeonName, "524290");
        feed.Raise(GameSession.TransitWorldName, "524288");
        feed.Raise(OverworldName, "524289");

        Assert.Equal([DungeonName, OverworldName], events);
        Assert.Equal([true, false], transit);
    }

    private static (FakeWorldNameSource Feed, GameSession Session, List<string> Events, List<bool> Transit, Func<int> Opens) TransitSession()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier()
            .Map(DungeonName, DungeonSno, isDungeon: true)
            .Map(OverworldName, OverworldSno, isDungeon: false)
            .Map(GameSession.TransitWorldName, 2, isDungeon: false);
        int opens = 0;
        var session = NewSession(feed, classifier, (_, _) => { opens++; return new FakeDungeonSessionReader(); });
        var events = new List<string>();
        var transit = new List<bool>();
        session.WorldChanged += (_, name, _) => events.Add(name);
        session.TransitChanged += transit.Add;
        return (feed, session, events, transit, () => opens);
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
    public void OpenSessionThrowingIOException_StaysIdleAndALaterRetryOpensIt()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        Exception? failure = new IOException("Diablo IV.exe is being used by another process");
        var session = NewSession(feed, classifier, (_, _) => failure is { } e ? throw e : new FakeDungeonSessionReader());

        feed.Raise(DungeonName);
        Assert.Equal(SessionState.Idle, session.State);

        failure = new UnauthorizedAccessException("Access to Diablo IV.exe is denied");
        session.RetryOpenOnce();
        Assert.Equal(SessionState.Idle, session.State);

        failure = null;
        session.RetryOpenOnce();
        Assert.Equal(SessionState.InDungeon, session.State);
    }

    [Fact]
    public void PollPlayerOnce_AReaderThatThrows_NeitherEscapesNorStopsTheNextPoll()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader { NextPlayer = new PlayerState(1f, 2f, 3f, 1234), ReadFailure = new ObjectDisposedException("handle") };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);
        int moves = 0;
        session.PlayerMoved += _ => moves++;

        session.PollPlayerOnce();
        session.PollPlayerOnce();
        reader.ReadFailure = null;
        session.PollPlayerOnce();

        Assert.Equal(1, moves);
        Assert.Equal(SessionState.InDungeon, session.State);
    }

    [Fact]
    public void PollActorsAndTilesOnce_AReaderThatThrows_NeitherEscapesNorStopsTheNextPoll()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader { ReadFailure = new ArgumentOutOfRangeException("offset") };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);
        int actorReads = 0, tileReads = 0;
        session.ActorsUpdated += _ => actorReads++;
        session.TilesUpdated += _ => tileReads++;

        session.PollActorsOnce();
        session.PollTilesOnce();
        reader.ReadFailure = null;
        session.PollActorsOnce();
        session.PollTilesOnce();

        Assert.Equal((1, 1), (actorReads, tileReads));
    }

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task APlayerReadStillRunningAtAWorldChange_NeverReachesTheListenersAndClosesTheSessionWhenDone()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true).Map(OverworldName, OverworldSno, isDungeon: false);
        var reader = new FakeDungeonSessionReader { NextPlayer = new PlayerState(1f, 2f, 3f, 1234) };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);
        var events = new List<string>();
        session.PlayerMoved += _ => { lock (events) events.Add("player"); };
        session.WorldChanged += (_, name, _) => { lock (events) events.Add(name); };
        using var inRead = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        reader.BeforeRead = () => { inRead.Set(); release.Wait(); };

        var poll = Task.Run(session.PollPlayerOnce);
        Assert.True(inRead.Wait(Patience));
        feed.Raise(OverworldName);
        Assert.Equal(0, reader.DisposeCount); // the read still uses its process handle

        release.Set();
        Assert.Same(poll, await Task.WhenAny(poll, Task.Delay(Patience)));
        lock (events) Assert.Equal([OverworldName], events);
        Assert.Equal(1, reader.DisposeCount);
    }

    [Fact]
    public async Task AnActorTickWhileTheLastReadStillRuns_IsSkipped()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader();
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);
        int reads = 0;
        using var inRead = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        reader.BeforeRead = () => { Interlocked.Increment(ref reads); inRead.Set(); release.Wait(); };

        var first = Task.Run(session.PollActorsOnce);
        Assert.True(inRead.Wait(Patience));
        session.PollActorsOnce();
        Assert.Equal(1, reads);

        release.Set();
        Assert.Same(first, await Task.WhenAny(first, Task.Delay(Patience)));
        session.PollActorsOnce();
        Assert.Equal(2, reads);
    }

    [Fact]
    public void AFullScanOverAWorldChange_DeliversNoTilesAndClosesTheSessionWhenDone()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true).Map(OverworldName, OverworldSno, isDungeon: false);
        var reader = new FakeDungeonSessionReader { NextRouteTiles = null }; // no route: the fallback scan runs
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);
        int tileEvents = 0;
        session.TilesUpdated += _ => Interlocked.Increment(ref tileEvents);
        using var inScan = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        reader.BeforeRead = () => { inScan.Set(); release.Wait(); };

        session.PollTilesOnce();
        Assert.True(inScan.Wait(Patience));
        feed.Raise(OverworldName);
        Assert.Equal(0, reader.DisposeCount);

        release.Set();
        Assert.True(SpinWait.SpinUntil(() => reader.DisposeCount == 1, Patience));
        Assert.Equal(0, tileEvents);
    }

    [Fact]
    public void AnUnreadableGame_CountsAgainstThePosition_AClosedOneDoesNot()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true).MapOverworld(OverworldName, OverworldSno);
        var log = new List<string>();
        var now = TimeSpan.Zero;
        var health = new ReadHealth(() => now, log.Add, _ => { });
        bool running = false;
        var session = new GameSession(feed, classifier,
            (_, _) => running ? throw new GameUnreadableException("player not found") : throw new InvalidOperationException("not running"),
            SceneSnos, Timeout.Infinite, Timeout.Infinite, Timeout.Infinite, openRetryIntervalMs: Timeout.Infinite, health: health);

        feed.Raise(DungeonName);
        now = TimeSpan.FromMinutes(5);
        session.RetryOpenOnce();
        Assert.Empty(log);

        running = true;
        feed.Raise(OverworldName);
        now += TimeSpan.FromMinutes(1);
        session.RetryOpenOnce();
        Assert.Single(log, line => line.Contains("Position FAILED") && line.Contains("player not found"));
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
    public void PollPlayerOnce_WorldUnloadingZeroPosition_IsSkippedWithoutLeaving()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader { NextPlayer = new PlayerState(0f, 0f, 0f, 0) };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);
        int moves = 0;
        session.PlayerMoved += _ => moves++;

        session.PollPlayerOnce();

        Assert.Equal(0, moves);
        Assert.Equal(SessionState.InDungeon, session.State);
    }

    [Fact]
    public void PollPlayerOnce_AFailingListener_NeitherThrowsNorStarvesTheOthers()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader { NextPlayer = new PlayerState(1f, 2f, 3f, 1234) };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);
        int reached = 0;
        session.PlayerMoved += _ => throw new ArgumentOutOfRangeException();
        session.PlayerMoved += _ => reached++;

        session.PollPlayerOnce();
        session.PollPlayerOnce();

        Assert.Equal(2, reached);
    }

    [Fact]
    public void PollPlayerOnce_NullPlayer_LeavesTheDungeon()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().Map(DungeonName, DungeonSno, isDungeon: true);
        var reader = new FakeDungeonSessionReader { NextPlayer = null };
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(DungeonName);

        var events = new List<(int Sno, string Name, bool Dungeon)>();
        session.WorldChanged += (sno, name, dungeon) => events.Add((sno, name, dungeon));

        session.PollPlayerOnce();

        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(1, reader.DisposeCount);
        Assert.Equal([(GameSession.NoWorldSno, GameSession.GameLostWorldName, false)], events); // bridges silence on it
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

    private const string RadarOnlyName = "Mercenary_Hideout";
    private const int RadarOnlySno = 1472231;

    [Fact]
    public void RadarOnlyWorld_OpensASession_ReportsNotADungeon_AndRaisesTheStaticTilesOnEveryPoll()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().MapRadarOnly(RadarOnlyName, RadarOnlySno);
        var reader = new FakeDungeonSessionReader();
        int opened = 0;
        var tile = new TileState(1472246, "Mercenary_Hideout_N01_W01", -96, -96, 96, 96, 0, 1, 1);
        var raised = new List<IReadOnlyList<TileState>>();
        var events = new List<(int, string, bool)>();
        var session = NewSession(feed, classifier, (_, _) => { opened++; return reader; }, staticTiles: sno => sno == RadarOnlySno ? [tile] : []);
        session.TilesUpdated += raised.Add;
        session.WorldChanged += (sno, name, dungeon) => events.Add((sno, name, dungeon));

        feed.Raise(RadarOnlyName);
        session.PollTilesOnce(); // re-raises the offline tiles for late listeners; never reads the live route here
        session.PollPlayerOnce();

        Assert.Equal(1, opened);
        Assert.Equal(SessionState.InDungeon, session.State);
        Assert.Equal(WorldKind.RadarOnly, session.CurrentWorldKind);
        Assert.True(session.CurrentWorldRadarOnly);
        Assert.Equal((RadarOnlyName, false), session.CurrentWorld);
        Assert.Equal([(RadarOnlySno, RadarOnlyName, false)], events);
        Assert.Equal(2, raised.Count); // on entry, then on the poll
        Assert.All(raised, tiles => Assert.Equal([tile], tiles));
        Assert.Equal(0, reader.RouteReads);
    }

    [Fact]
    public void LeavingARadarOnlyWorld_ClearsTheFlag()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().MapRadarOnly(RadarOnlyName, RadarOnlySno).Map(OverworldName, OverworldSno, isDungeon: false);
        var reader = new FakeDungeonSessionReader();
        var session = NewSession(feed, classifier, (_, _) => reader);
        feed.Raise(RadarOnlyName);

        feed.Raise(OverworldName);

        Assert.False(session.CurrentWorldRadarOnly);
        Assert.Equal(SessionState.Idle, session.State);
        Assert.Equal(1, reader.DisposeCount);
    }

    [Fact]
    public void Overworld_OpensASession_FeedsPlayerAndActors_ButNeverReadsTiles()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().MapOverworld(OverworldName, OverworldSno);
        var reader = new FakeDungeonSessionReader
        {
            NextPlayer = new PlayerState(-491, 45, 39, 69123), NextRouteTiles = null, NextActors = [new ActorState(7, 972175, -573, 40, 47)],
        };
        var session = NewSession(feed, classifier, (_, _) => reader);
        var players = new List<PlayerState>();
        var actors = new List<IReadOnlyList<ActorState>>();
        var tiles = new List<IReadOnlyList<TileState>>();
        session.PlayerMoved += players.Add;
        session.ActorsUpdated += actors.Add;
        session.TilesUpdated += tiles.Add;

        feed.Raise(OverworldName);
        session.PollPlayerOnce();
        session.PollActorsOnce();
        session.PollTilesOnce(); // a null route would start the full scan in a dungeon
        session.RescanTiles();

        Assert.Equal(SessionState.InDungeon, session.State);
        Assert.Equal(WorldKind.Overworld, session.CurrentWorldKind);
        Assert.Equal((OverworldName, false), session.CurrentWorld);
        Assert.Single(players);
        Assert.Single(actors);
        Assert.Empty(tiles);
        Assert.Equal(0, reader.RouteReads);
        Assert.Equal(0, reader.FullScanCount);
    }

    [Fact]
    public void TheActorPollRaisesTheCinematicStateOnlyWhenReadable()
    {
        var feed = new FakeWorldNameSource();
        var classifier = new FakeClassifier().MapOverworld(OverworldName, OverworldSno);
        var reader = new FakeDungeonSessionReader();
        var session = NewSession(feed, classifier, (_, _) => reader);
        var cinematic = new List<bool>();
        session.CinematicRead += cinematic.Add;

        feed.Raise(OverworldName);
        session.PollActorsOnce(); // unreadable: nothing raised
        reader.NextCinematic = true;
        session.PollActorsOnce();
        reader.NextCinematic = false;
        session.PollActorsOnce();

        Assert.Equal([true, false], cinematic);
    }

    private static GameSession NewSession(
        FakeWorldNameSource feed, FakeClassifier classifier,
        Func<int, IReadOnlySet<int>, IDungeonSessionReader?>? openSession = null,
        Func<int, IReadOnlyList<TileState>>? staticTiles = null) =>
        new(feed, classifier, openSession ?? ((_, _) => new FakeDungeonSessionReader()), SceneSnos,
            playerPollIntervalMs: Timeout.Infinite, tilePollIntervalMs: Timeout.Infinite, actorPollIntervalMs: Timeout.Infinite,
            staticTiles: staticTiles, openRetryIntervalMs: Timeout.Infinite);

    private sealed class FakeWorldNameSource : IWorldNameSource
    {
        public event Action<WorldEntry>? WorldChanged;
        public string? StartupWorld { get; set; }
        public WorldEntry? ReadLastWorldAtStartup() => StartupWorld is { } name ? new WorldEntry(name, null) : null;
        public void Raise(string worldName, string? instance = null) => WorldChanged?.Invoke(new WorldEntry(worldName, instance));
    }

    private sealed class FakeClassifier : IWorldClassifier
    {
        private readonly Dictionary<string, (int Sno, WorldKind Kind)> _map = [];

        public FakeClassifier Map(string worldName, int sno, bool isDungeon)
        {
            _map[worldName] = (sno, isDungeon ? WorldKind.Dungeon : WorldKind.Other);
            return this;
        }

        public FakeClassifier MapRadarOnly(string worldName, int sno)
        {
            _map[worldName] = (sno, WorldKind.RadarOnly);
            return this;
        }

        public FakeClassifier MapOverworld(string worldName, int sno)
        {
            _map[worldName] = (sno, WorldKind.Overworld);
            return this;
        }

        public (int WorldSno, WorldKind Kind) Classify(string worldName) =>
            _map.TryGetValue(worldName, out var v) ? v : throw new ArgumentException($"Unknown world '{worldName}'.");
    }

    private sealed class FakeDungeonSessionReader : IDungeonSessionReader
    {
        public int DisposeCount;
        public int FullScanCount;
        public PlayerState? NextPlayer { get; set; } = new(0, 0, 0, 0);
        public IReadOnlyList<TileState>? NextRouteTiles { get; set; } = [];
        public IReadOnlyList<ActorState>? NextActors { get; set; } = [];

        /// <summary>Thrown by every player, tile and actor read while set, like a read over memory that changed under it.</summary>
        public Exception? ReadFailure { get; set; }

        /// <summary>Runs at the start of every player, actor and full-scan read, e.g. to hold one on another thread.</summary>
        public Action? BeforeRead { get; set; }

        public PlayerState? ReadPlayer() { BeforeRead?.Invoke(); return ReadFailure is { } e ? throw e : NextPlayer; }
        public int RouteReads { get; private set; }
        public IReadOnlyList<TileState>? ReadTilesViaRoute() { RouteReads++; return ReadFailure is { } e ? throw e : NextRouteTiles; }
        public IReadOnlyList<ActorState>? ReadActors() { BeforeRead?.Invoke(); return ReadFailure is { } e ? throw e : NextActors; }
        public bool? NextCinematic { get; set; }
        public bool? ReadCinematic() => NextCinematic;

        public IReadOnlyList<TileState> ReadTilesViaFullScan()
        {
            BeforeRead?.Invoke();
            Interlocked.Increment(ref FullScanCount);
            return [];
        }

        public void Dispose() => DisposeCount++;
    }
}
