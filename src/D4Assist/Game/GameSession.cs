using D4Memory.Ui;

namespace D4Assist.Game;

public enum SessionState { WaitingForGame, Idle, InDungeon }

/// <summary>
/// Central accessible-state machine (CLAUDE.md §9): WaitingForGame -> Idle (a menu or loading world: no memory
/// reads at all) -> InDungeon (a memory session is open: position, actors and, except in the overworld, tiles).
/// Diff-gates on world identity so a repeated log line never re-opens a session or re-announces the same world.
/// </summary>
public sealed class GameSession : IDungeonFeed, IDisposable
{
    // Poll intervals (task spec): position needs to feel responsive, tiles change far less often.
    public const int DefaultPlayerPollIntervalMs = 100;
    public const int DefaultTilePollIntervalMs = 1000;
    // D2R refreshes its object lists every 500 ms (d2r-access docs/ground-items.md).
    public const int DefaultActorPollIntervalMs = 500;
    // The log names the new world before the game places the player in it (slow PCs take longer), so a failed open is retried.
    public const int DefaultOpenRetryIntervalMs = 1000;

    /// <summary>WorldChanged's world SNO when the game process is gone (no world at all), so every bridge silences.</summary>
    public const int NoWorldSno = -1;
    public const string GameLostWorldName = "(game closed)";

    private readonly IWorldClassifier _classifier;
    private readonly Func<int, IReadOnlySet<int>, IDungeonSessionReader?> _openSession;
    private readonly IReadOnlySet<int> _sceneSnos;
    private readonly Func<int, IReadOnlyList<TileState>>? _staticTiles;
    private readonly int _playerPollIntervalMs;
    private readonly int _tilePollIntervalMs;
    private readonly int _actorPollIntervalMs;
    private readonly int _openRetryIntervalMs;
    private readonly object _gate = new();

    private Timer? _playerTimer;
    private Timer? _tileTimer;
    private Timer? _actorTimer;
    private Timer? _openRetryTimer;
    private WorldKind? _pendingOpenKind;
    private string? _lastOpenFailure;
    private IDungeonSessionReader? _session;
    private int _currentWorldSno = -1;
    private readonly HashSet<string> _reportedPollFailures = [];
    private bool _fallbackScanTriggered;

    public SessionState State { get; private set; } = SessionState.WaitingForGame;

    /// <summary>IsDungeon is true only for DRLG dungeons; a radar-only world reports false here (bridges stay silent) and true in <see cref="CurrentWorldRadarOnly"/>.</summary>
    public (string Name, bool IsDungeon)? CurrentWorld { get; private set; }

    public bool CurrentWorldRadarOnly { get; private set; }

    public WorldKind CurrentWorldKind { get; private set; }

    public event Action<int, string, bool>? WorldChanged;
    public event Action<IReadOnlyList<TileState>>? TilesUpdated;
    public event Action<PlayerState>? PlayerMoved;
    public event Action<IReadOnlyList<ActorState>>? ActorsUpdated;

    /// <summary>The dialogue menu and playing conversations, read at the player-poll rate while a memory session is open.</summary>
    public event Action<ConversationSnapshot>? ConversationRead;

    /// <summary>The open watched menu screens, read at the player-poll rate alongside the conversation.</summary>
    public event Action<IReadOnlyList<string>>? MenusRead;

    /// <param name="staticTiles">Offline tiles of a radar-only (static-grid) world, raised once on entry; null means none.</param>
    /// <param name="playerPollIntervalMs">Timer.Infinite disables the automatic timer entirely — tests then
    /// drive polling deterministically via PollPlayerOnce/PollTilesOnce instead of a background tick.</param>
    public GameSession(
        IWorldNameSource feed, IWorldClassifier classifier,
        Func<int, IReadOnlySet<int>, IDungeonSessionReader?> openSession, IReadOnlySet<int> sceneSnos,
        int playerPollIntervalMs = DefaultPlayerPollIntervalMs, int tilePollIntervalMs = DefaultTilePollIntervalMs,
        int actorPollIntervalMs = DefaultActorPollIntervalMs, Func<int, IReadOnlyList<TileState>>? staticTiles = null,
        int openRetryIntervalMs = DefaultOpenRetryIntervalMs)
    {
        _staticTiles = staticTiles;
        _classifier = classifier;
        _openSession = openSession;
        _sceneSnos = sceneSnos;
        _playerPollIntervalMs = playerPollIntervalMs;
        _tilePollIntervalMs = tilePollIntervalMs;
        _actorPollIntervalMs = actorPollIntervalMs;
        _openRetryIntervalMs = openRetryIntervalMs;

        feed.WorldChanged += OnWorldChanged;
        if (feed.ReadLastWorldNameAtStartup() is { } lastWorld) OnWorldChanged(lastWorld);
    }

    private void OnWorldChanged(string worldName)
    {
        (int WorldSno, WorldKind Kind) classified;
        try { classified = _classifier.Classify(worldName); }
        catch (ArgumentException) { return; } // world name not in CoreTOC; ignore rather than crash the loop

        lock (_gate)
        {
            bool firstWorld = State == SessionState.WaitingForGame;
            if (!firstWorld && classified.WorldSno == _currentWorldSno) return; // diff-gate
            _currentWorldSno = classified.WorldSno;

            LeaveDungeonLocked();
            bool isDungeon = classified.Kind == WorldKind.Dungeon;
            bool radarOnly = classified.Kind == WorldKind.RadarOnly;
            bool reads = classified.Kind != WorldKind.Other;
            State = reads ? SessionState.InDungeon : SessionState.Idle; // InDungeon = a memory session is open
            CurrentWorld = (worldName, isDungeon);
            CurrentWorldKind = classified.Kind;
            CurrentWorldRadarOnly = radarOnly;
            Raise("world", WorldChanged, h => h(classified.WorldSno, worldName, isDungeon));
            if (reads) EnterDungeonLocked(classified.Kind);
        }
    }

    private void EnterDungeonLocked(WorldKind kind)
    {
        _lastOpenFailure = null;
        if (TryOpenSessionLocked()) { StartPollingLocked(kind); return; }

        State = SessionState.Idle; CurrentWorldRadarOnly = false; // stay accessible-but-idle, no reads
        _pendingOpenKind = kind;
        if (_openRetryIntervalMs != Timeout.Infinite)
            _openRetryTimer = new Timer(_ => RetryOpenOnce(), null, _openRetryIntervalMs, _openRetryIntervalMs);
    }

    /// <summary>Retries a failed open until it succeeds or the world changes. Public so tests can drive it deterministically.</summary>
    public void RetryOpenOnce()
    {
        lock (_gate)
        {
            if (_pendingOpenKind is not { } kind || !TryOpenSessionLocked()) return;
            Console.WriteLine("Memory: reading the game now.");
            StartPollingLocked(kind);
        }
    }

    private bool TryOpenSessionLocked()
    {
        string failure;
        try
        {
            _session = _openSession(_currentWorldSno, _sceneSnos);
            if (_session is not null) return true;
            failure = "no memory session";
        }
        catch (InvalidOperationException e) { _session = null; failure = e.Message; } // not running, access denied, player not placed yet

        if (failure != _lastOpenFailure) Console.WriteLine($"Memory: cannot read the game yet, retrying: {failure}");
        _lastOpenFailure = failure;
        return false;
    }

    private void StartPollingLocked(WorldKind kind)
    {
        bool radarOnly = kind == WorldKind.RadarOnly;
        StopOpenRetryLocked();
        State = SessionState.InDungeon;
        CurrentWorldRadarOnly = radarOnly;
        _fallbackScanTriggered = false;
        _playerTimer = new Timer(_ => PollPlayerOnce(), null, _playerPollIntervalMs, _playerPollIntervalMs);
        // Static grid: the tiles come from offline data (the live CScene route cannot match them), re-raised on
        // every tile poll so listeners created after entry — e.g. when the app starts in the den — still get them.
        if (radarOnly) RaiseStaticTiles();
        // The overworld has no tiles to poll: its scenes aren't DRLG, and a failed route must not start the 16 GB fallback scan.
        if (kind != WorldKind.Overworld) _tileTimer = new Timer(_ => PollTilesOnce(), null, _tilePollIntervalMs, _tilePollIntervalMs);
        _actorTimer = new Timer(_ => PollActorsOnce(), null, _actorPollIntervalMs, _actorPollIntervalMs);
    }

    private void RaiseStaticTiles()
    {
        IReadOnlyList<TileState> tiles;
        try { tiles = _staticTiles?.Invoke(_currentWorldSno) ?? []; }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or KeyNotFoundException)
        {
            Console.Error.WriteLine($"Static tiles unavailable for world {_currentWorldSno}: {e.Message}");
            return;
        }
        Raise("tiles", TilesUpdated, h => h(tiles));
    }

    private void LeaveDungeonLocked()
    {
        StopOpenRetryLocked();
        _playerTimer?.Dispose(); _playerTimer = null;
        _tileTimer?.Dispose(); _tileTimer = null;
        _actorTimer?.Dispose(); _actorTimer = null;
        _session?.Dispose(); _session = null;
    }

    private void StopOpenRetryLocked()
    {
        _openRetryTimer?.Dispose(); _openRetryTimer = null;
        _pendingOpenKind = null;
    }

    /// <summary>Runs one player-position read. Public so tests can drive it deterministically instead of waiting on the timer.</summary>
    public void PollPlayerOnce()
    {
        IDungeonSessionReader? session;
        lock (_gate) session = _session;
        if (session is null) return;

        var player = session.ReadPlayer();
        if (player is null) { HandleSessionLost(session); return; } // process likely exited
        if (player.IsWorldUnloaded) return;
        Raise("player", PlayerMoved, h => h(player));
        if (session.ReadConversation() is { } talk) Raise("conversation", ConversationRead, h => h(talk));
        if (session.ReadMenus() is { } menus) Raise("menus", MenusRead, h => h(menus));
    }

    /// <summary>Runs one actor-pool read; an unavailable pool is skipped silently (the reader logs it once).</summary>
    public void PollActorsOnce()
    {
        IDungeonSessionReader? session;
        lock (_gate) session = _session;
        if (session?.ReadActors() is { } actors) Raise("actors", ActorsUpdated, h => h(actors));
    }

    // A failing listener must never kill the app from a timer thread, nor starve the other listeners
    // (live crash 2026-09-28): each one runs on its own, a failure is logged once per feed and polling goes on.
    private void Raise<THandler>(string feed, THandler? handlers, Action<THandler> call) where THandler : Delegate
    {
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList().Cast<THandler>())
        {
            try { call(handler); }
            catch (Exception e)
            {
                bool first;
                lock (_reportedPollFailures) first = _reportedPollFailures.Add(feed);
                if (first) Console.Error.WriteLine($"A {feed} listener failed; the feed keeps running. {e}");
            }
        }
    }

    /// <summary>Runs one tile-route read, triggering the one-time fallback scan if the route is unavailable.
    /// Public so tests can drive it deterministically instead of waiting on the timer.</summary>
    public void PollTilesOnce()
    {
        IDungeonSessionReader? session;
        bool radarOnly;
        lock (_gate) (session, radarOnly) = (CurrentWorldKind == WorldKind.Overworld ? null : _session, CurrentWorldRadarOnly);
        if (session is null) return;
        if (radarOnly) { RaiseStaticTiles(); return; } // static worlds keep their offline tiles

        var tiles = session.ReadTilesViaRoute();
        if (tiles is not null) { Raise("tiles", TilesUpdated, h => h(tiles)); return; }

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
        lock (_gate) session = CurrentWorldKind is WorldKind.RadarOnly or WorldKind.Overworld ? null : _session;
        if (session is not null) RunFullScanInBackground(session);
    }

    private void RunFullScanInBackground(IDungeonSessionReader session) =>
        Task.Run(() =>
        {
            var tiles = session.ReadTilesViaFullScan();
            lock (_gate) { if (_session != session) return; }
            Raise("tiles", TilesUpdated, h => h(tiles));
        });

    private void HandleSessionLost(IDungeonSessionReader expectedSession)
    {
        lock (_gate)
        {
            if (_session != expectedSession) return;
            LeaveDungeonLocked();
            State = SessionState.Idle;
            _currentWorldSno = NoWorldSno;
            CurrentWorld = (GameLostWorldName, false);
            CurrentWorldRadarOnly = false;
            CurrentWorldKind = WorldKind.Other;
            Raise("world", WorldChanged, h => h(NoWorldSno, GameLostWorldName, false));
        }
    }

    public void Dispose()
    {
        lock (_gate) LeaveDungeonLocked();
    }
}
