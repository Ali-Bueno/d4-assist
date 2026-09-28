namespace D4Assist.Game;

public enum SessionState { WaitingForGame, Idle, InDungeon }

/// <summary>
/// Central accessible-state machine (CLAUDE.md §9): WaitingForGame -> Idle (not in a dungeon: no memory
/// reads at all) -> InDungeon (open the process, poll position/tiles). Diff-gates on world identity so a
/// repeated log line never re-opens a session or re-announces the same world.
/// </summary>
public sealed class GameSession : IDungeonFeed, IDisposable
{
    // Poll intervals (task spec): position needs to feel responsive, tiles change far less often.
    public const int DefaultPlayerPollIntervalMs = 100;
    public const int DefaultTilePollIntervalMs = 1000;

    private readonly IWorldClassifier _classifier;
    private readonly Func<int, IReadOnlySet<int>, IDungeonSessionReader?> _openSession;
    private readonly IReadOnlySet<int> _sceneSnos;
    private readonly int _playerPollIntervalMs;
    private readonly int _tilePollIntervalMs;
    private readonly object _gate = new();

    private Timer? _playerTimer;
    private Timer? _tileTimer;
    private IDungeonSessionReader? _session;
    private int _currentWorldSno = -1;
    private bool _fallbackScanTriggered;

    public SessionState State { get; private set; } = SessionState.WaitingForGame;

    public event Action<int, string, bool>? WorldChanged;
    public event Action<IReadOnlyList<TileState>>? TilesUpdated;
    public event Action<PlayerState>? PlayerMoved;

    /// <param name="playerPollIntervalMs">Timer.Infinite disables the automatic timer entirely — tests then
    /// drive polling deterministically via PollPlayerOnce/PollTilesOnce instead of a background tick.</param>
    public GameSession(
        IWorldNameSource feed, IWorldClassifier classifier,
        Func<int, IReadOnlySet<int>, IDungeonSessionReader?> openSession, IReadOnlySet<int> sceneSnos,
        int playerPollIntervalMs = DefaultPlayerPollIntervalMs, int tilePollIntervalMs = DefaultTilePollIntervalMs)
    {
        _classifier = classifier;
        _openSession = openSession;
        _sceneSnos = sceneSnos;
        _playerPollIntervalMs = playerPollIntervalMs;
        _tilePollIntervalMs = tilePollIntervalMs;

        feed.WorldChanged += OnWorldChanged;
        if (feed.ReadLastWorldNameAtStartup() is { } lastWorld) OnWorldChanged(lastWorld);
    }

    private void OnWorldChanged(string worldName)
    {
        (int WorldSno, bool IsDungeon) classified;
        try { classified = _classifier.Classify(worldName); }
        catch (ArgumentException) { return; } // world name not in CoreTOC; ignore rather than crash the loop

        lock (_gate)
        {
            bool firstWorld = State == SessionState.WaitingForGame;
            if (!firstWorld && classified.WorldSno == _currentWorldSno) return; // diff-gate
            _currentWorldSno = classified.WorldSno;

            LeaveDungeonLocked();
            State = classified.IsDungeon ? SessionState.InDungeon : SessionState.Idle;
            WorldChanged?.Invoke(classified.WorldSno, worldName, classified.IsDungeon);
            if (classified.IsDungeon) EnterDungeonLocked();
        }
    }

    private void EnterDungeonLocked()
    {
        try { _session = _openSession(_currentWorldSno, _sceneSnos); }
        catch (InvalidOperationException) { _session = null; } // game process not running

        if (_session is null) { State = SessionState.Idle; return; } // stay accessible-but-idle, no reads

        _fallbackScanTriggered = false;
        _playerTimer = new Timer(_ => PollPlayerOnce(), null, _playerPollIntervalMs, _playerPollIntervalMs);
        _tileTimer = new Timer(_ => PollTilesOnce(), null, _tilePollIntervalMs, _tilePollIntervalMs);
    }

    private void LeaveDungeonLocked()
    {
        _playerTimer?.Dispose(); _playerTimer = null;
        _tileTimer?.Dispose(); _tileTimer = null;
        _session?.Dispose(); _session = null;
    }

    /// <summary>Runs one player-position read. Public so tests can drive it deterministically instead of waiting on the timer.</summary>
    public void PollPlayerOnce()
    {
        IDungeonSessionReader? session;
        lock (_gate) session = _session;
        if (session is null) return;

        var player = session.ReadPlayer();
        if (player is null) { HandleSessionLost(session); return; } // process likely exited
        PlayerMoved?.Invoke(player);
    }

    /// <summary>Runs one tile-route read, triggering the one-time fallback scan if the route is unavailable.
    /// Public so tests can drive it deterministically instead of waiting on the timer.</summary>
    public void PollTilesOnce()
    {
        IDungeonSessionReader? session;
        lock (_gate) session = _session;
        if (session is null) return;

        var tiles = session.ReadTilesViaRoute();
        if (tiles is not null) { TilesUpdated?.Invoke(tiles); return; }

        lock (_gate)
        {
            if (_session != session || _fallbackScanTriggered) return;
            _fallbackScanTriggered = true;
        }
        RunFullScanInBackground(session);
    }

    /// <summary>Forces an extra full scan right now (task spec: "re-scan only on demand" after the automatic one).</summary>
    public void RescanTiles()
    {
        IDungeonSessionReader? session;
        lock (_gate) session = _session;
        if (session is not null) RunFullScanInBackground(session);
    }

    private void RunFullScanInBackground(IDungeonSessionReader session) =>
        Task.Run(() =>
        {
            var tiles = session.ReadTilesViaFullScan();
            lock (_gate) { if (_session != session) return; }
            TilesUpdated?.Invoke(tiles);
        });

    private void HandleSessionLost(IDungeonSessionReader expectedSession)
    {
        lock (_gate)
        {
            if (_session != expectedSession) return;
            LeaveDungeonLocked();
            State = SessionState.Idle;
        }
    }

    public void Dispose()
    {
        lock (_gate) LeaveDungeonLocked();
    }
}
