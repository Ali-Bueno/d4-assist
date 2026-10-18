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

    /// <summary>The loading world between two others: a warp inside one dungeon passes through it back into the same
    /// instance (the Undercity boss room, the Worldstone's sections; research/runtime-signals.md §2).</summary>
    public const string TransitWorldName = "Limbo";

    private readonly IWorldClassifier _classifier;
    private readonly Func<int, IReadOnlySet<int>, IDungeonSessionReader?> _openSession;
    private readonly IReadOnlySet<int> _sceneSnos;
    private readonly Func<int, IReadOnlyList<TileState>>? _staticTiles;
    private readonly int _playerPollIntervalMs;
    private readonly int _tilePollIntervalMs;
    private readonly int _actorPollIntervalMs;
    private readonly int _openRetryIntervalMs;
    private readonly ReadHealth? _health;
    private readonly object _gate = new();
    // Every event goes out under it, after _gate when both are taken: see Deliver.
    private readonly object _deliver = new();
    // Set while that poll runs: a timer tick that finds its last read still going skips, so one reader is never read twice at once.
    private int _playerBusy, _tilesBusy, _actorsBusy;

    private Timer? _playerTimer;
    private Timer? _tileTimer;
    private Timer? _actorTimer;
    private Timer? _openRetryTimer;
    private WorldKind? _pendingOpenKind;
    private string? _lastOpenFailure;
    private SessionLease? _session;
    // The session and its quest phase count at the last actor read: a new session starts its own count.
    private (IDungeonSessionReader? Session, int Count) _questPhases;
    private int _currentWorldSno = -1;
    private string? _currentInstance;
    private bool _inTransit;
    private readonly HashSet<string> _reportedPollFailures = [];
    private bool _fallbackScanTriggered;

    public SessionState State { get; private set; } = SessionState.WaitingForGame;

    /// <summary>IsDungeon is true only for DRLG dungeons; a radar-only world reports false here (bridges stay silent) and true in <see cref="CurrentWorldRadarOnly"/>.</summary>
    public (string Name, bool IsDungeon)? CurrentWorld { get; private set; }

    public bool CurrentWorldRadarOnly { get; private set; }

    public WorldKind CurrentWorldKind { get; private set; }

    public string? CurrentInstance { get { lock (_gate) return _currentInstance; } }

    public event Action<int, string, bool>? WorldChanged;
    public event Action<IReadOnlyList<TileState>>? TilesUpdated;
    public event Action<PlayerState>? PlayerMoved;
    public event Action<IReadOnlyList<ActorState>>? ActorsUpdated;

    /// <summary>The places the active quests' current phases name, after every actor read (IDungeonSessionReader.ReadQuestPlaces).</summary>
    public event Action<IReadOnlyList<QuestPlace>>? QuestPlacesRead;

    /// <summary>A followed quest moved on from a known phase, after an actor read (IDungeonSessionReader.QuestPhaseChanges).</summary>
    public event Action? QuestMovedOn;

    /// <summary>Every actor the game's map marks, after every actor read (IDungeonSessionReader.ReadMapMarkers).</summary>
    public event Action<IReadOnlyList<MarkedActor>>? MapMarkersRead;

    /// <summary>The actor SNOs the active quests' current phases name outright, after every actor read (IDungeonSessionReader.ReadQuestActorSnos).</summary>
    public event Action<IReadOnlySet<int>>? QuestActorsNamed;

    /// <summary>True while loading through <see cref="TransitWorldName"/> out of a read world; the bridges keep their
    /// state (no WorldChanged) until the next world shows whether it is the same instance.</summary>
    public event Action<bool>? TransitChanged;

    /// <summary>The dialogue menu and playing conversations, read at the player-poll rate while a memory session is open.</summary>
    public event Action<ConversationSnapshot>? ConversationRead;

    /// <summary>The open watched menu screens, read at the player-poll rate alongside the conversation.</summary>
    public event Action<IReadOnlyList<string>>? MenusRead;

    /// <summary>Whether an in-engine cinematic plays, read at the actor-poll rate after the actors (it needs the player's ACD).</summary>
    public event Action<bool>? CinematicRead;

    /// <param name="staticTiles">Offline tiles of a radar-only (static-grid) world, raised once on entry; null means none.</param>
    /// <param name="playerPollIntervalMs">Timer.Infinite disables the automatic timer entirely — tests then
    /// drive polling deterministically via PollPlayerOnce/PollTilesOnce instead of a background tick.</param>
    /// <param name="health">Told whether each open finds the player, and when a new world starts; null checks nothing.</param>
    public GameSession(
        IWorldNameSource feed, IWorldClassifier classifier,
        Func<int, IReadOnlySet<int>, IDungeonSessionReader?> openSession, IReadOnlySet<int> sceneSnos,
        int playerPollIntervalMs = DefaultPlayerPollIntervalMs, int tilePollIntervalMs = DefaultTilePollIntervalMs,
        int actorPollIntervalMs = DefaultActorPollIntervalMs, Func<int, IReadOnlyList<TileState>>? staticTiles = null,
        int openRetryIntervalMs = DefaultOpenRetryIntervalMs, ReadHealth? health = null)
    {
        _health = health;
        _staticTiles = staticTiles;
        _classifier = classifier;
        _openSession = openSession;
        _sceneSnos = sceneSnos;
        _playerPollIntervalMs = playerPollIntervalMs;
        _tilePollIntervalMs = tilePollIntervalMs;
        _actorPollIntervalMs = actorPollIntervalMs;
        _openRetryIntervalMs = openRetryIntervalMs;

        feed.WorldChanged += OnWorldChanged;
        if (feed.ReadLastWorldAtStartup() is { } lastWorld) OnWorldChanged(lastWorld);
    }

    private void OnWorldChanged(WorldEntry entry)
    {
        string worldName = entry.Name;
        (int WorldSno, WorldKind Kind) classified;
        try { classified = _classifier.Classify(worldName); }
        catch (ArgumentException) { return; } // world name not in CoreTOC; ignore rather than crash the loop

        lock (_gate)
        {
            bool firstWorld = State == SessionState.WaitingForGame;
            if (!firstWorld && CurrentWorldKind != WorldKind.Other && string.Equals(worldName, TransitWorldName, StringComparison.OrdinalIgnoreCase))
            {
                BeginTransitLocked();
                return;
            }
            bool afterTransit = _inTransit;
            _inTransit = false;
            if (afterTransit && classified.WorldSno == _currentWorldSno && entry.Instance is not null && entry.Instance == _currentInstance)
            {
                Console.WriteLine($"World: back in {worldName} (same instance), keeping its map.");
                EnterDungeonLocked(CurrentWorldKind);
                Raise("transit", TransitChanged, h => h(false));
                return;
            }
            if (!firstWorld && !afterTransit && classified.WorldSno == _currentWorldSno) return; // diff-gate
            _currentWorldSno = classified.WorldSno;
            _currentInstance = entry.Instance;
            _health?.NewWorld();

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
            if (afterTransit) Raise("transit", TransitChanged, h => h(false));
        }
    }

    private void BeginTransitLocked()
    {
        if (_inTransit) return;
        _inTransit = true;
        LeaveDungeonLocked();
        State = SessionState.Idle;
        Raise("transit", TransitChanged, h => h(true));
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
    public void RetryOpenOnce() => Guarded("open retry", () =>
    {
        lock (_gate)
        {
            if (_pendingOpenKind is not { } kind || !TryOpenSessionLocked()) return;
            Console.WriteLine("Memory: reading the game now.");
            StartPollingLocked(kind);
        }
    });

    private bool TryOpenSessionLocked()
    {
        string failure;
        try
        {
            if (_openSession(_currentWorldSno, _sceneSnos) is { } reader)
            {
                _session = new SessionLease(reader);
                _health?.Report(ReadCheck.Position, ok: true);
                return true;
            }
            _session = null;
            failure = "no memory session";
        }
        catch (GameUnreadableException e) // access denied, no game module, player not placed yet (or its record moved)
        {
            _session = null;
            failure = e.Message;
            _health?.Report(ReadCheck.Position, ok: false, failure);
        }
        catch (InvalidOperationException e) { _session = null; failure = e.Message; } // not running
        // The exe on disk can't be read while a game update or the launcher holds it, or it is half written: try again later.
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _session = null;
            failure = $"{e.GetType().Name}: {e.Message}";
        }

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
        if (_session is { } lease)
        {
            _session = null;
            lease.Retired = true;
            if (lease.Users == 0) lease.Reader.Dispose(); // else the last poll still reading closes it (GiveBack)
        }
    }

    // A session the polls borrow. A world change retires it at once, but its process handle closes only once no read is on
    // its way: Windows reuses a closed handle's value, so a late read could reach another object.
    private sealed class SessionLease(IDungeonSessionReader reader)
    {
        public IDungeonSessionReader Reader { get; } = reader;
        public int Users; // under _gate
        public volatile bool Retired;
    }

    private SessionLease? BorrowLocked()
    {
        if (_session is { } lease) lease.Users++;
        return _session;
    }

    private void GiveBack(SessionLease lease)
    {
        lock (_gate) if (--lease.Users > 0 || !lease.Retired) return;
        lease.Reader.Dispose(); // retired while this read was on its way: it was the last
    }

    // What a poll read goes out only while its session is still the current one. Every event takes _deliver, and a world change
    // retires the session before it raises WorldChanged: a read that started in the old world either reaches the listeners
    // before that WorldChanged or not at all.
    private void Deliver(SessionLease lease, Action raise)
    {
        lock (_deliver) if (!lease.Retired) raise();
    }

    private void StopOpenRetryLocked()
    {
        _openRetryTimer?.Dispose(); _openRetryTimer = null;
        _pendingOpenKind = null;
    }

    /// <summary>Runs <paramref name="read"/> on the current session, on the caller's thread (a key press, not a poll); false
    /// while no session is open.</summary>
    public bool Query(Action<IDungeonSessionReader> read)
    {
        SessionLease? lease;
        lock (_gate) lease = BorrowLocked();
        if (lease is null) return false;
        try { read(lease.Reader); return true; }
        finally { GiveBack(lease); }
    }

    /// <summary>Runs one player-position read. Public so tests can drive it deterministically instead of waiting on the timer.</summary>
    public void PollPlayerOnce() => Guarded("player", ref _playerBusy, ReadPlayerOnce);

    private void ReadPlayerOnce()
    {
        SessionLease? lease;
        lock (_gate) lease = BorrowLocked();
        if (lease is null) return;
        try
        {
            var session = lease.Reader;
            var player = session.ReadPlayer();
            if (player is null) { HandleSessionLost(lease); return; } // process likely exited
            if (player.IsWorldUnloaded) return;
            Deliver(lease, () => Raise("player", PlayerMoved, h => h(player)));
            if (session.ReadConversation() is { } talk) Deliver(lease, () => Raise("conversation", ConversationRead, h => h(talk)));
            if (session.ReadMenus() is { } menus) Deliver(lease, () => Raise("menus", MenusRead, h => h(menus)));
        }
        finally { GiveBack(lease); }
    }

    /// <summary>Runs one actor-pool read; an unavailable pool is skipped silently (the reader logs it once).</summary>
    public void PollActorsOnce() => Guarded("actors", ref _actorsBusy, ReadActorsOnce);

    private void ReadActorsOnce()
    {
        SessionLease? lease;
        lock (_gate) lease = BorrowLocked();
        if (lease is null) return;
        try
        {
            var session = lease.Reader;
            if (session.ReadActors() is { } actors) Deliver(lease, () => Raise("actors", ActorsUpdated, h => h(actors)));
            if (QuestMoved(session)) Deliver(lease, () => Raise("quest moved on", QuestMovedOn, h => h()));
            if (session.ReadQuestPlaces() is { } places) Deliver(lease, () => Raise("quest places", QuestPlacesRead, h => h(places)));
            if (session.ReadMapMarkers() is { } marks) Deliver(lease, () => Raise("map markers", MapMarkersRead, h => h(marks)));
            if (session.ReadQuestActorSnos() is { } named) Deliver(lease, () => Raise("quest actors", QuestActorsNamed, h => h(named)));
            if (session.ReadCinematic() is { } cinematic) Deliver(lease, () => Raise("cinematic", CinematicRead, h => h(cinematic)));
        }
        finally { GiveBack(lease); }
    }

    private void Guarded(string poll, ref int busy, Action read)
    {
        if (Interlocked.Exchange(ref busy, 1) == 1) return; // the last tick's read still runs
        try { Guarded(poll, read); }
        finally { Volatile.Write(ref busy, 0); }
    }

    // An exception out of a timer callback ends the process. A read that throws (memory changing under it, a parse of
    // unexpected data) is logged once per poll and the next tick tries again.
    private void Guarded(string poll, Action read)
    {
        try { read(); }
        catch (Exception e)
        {
            bool first;
            lock (_reportedPollFailures) first = _reportedPollFailures.Add($"{poll} poll");
            if (first) Console.Error.WriteLine($"The {poll} read failed; polling goes on. {e}");
        }
    }

    private bool QuestMoved(IDungeonSessionReader session)
    {
        int count = session.QuestPhaseChanges;
        lock (_gate)
        {
            bool moved = _questPhases.Session == session && count > _questPhases.Count;
            _questPhases = (session, count);
            return moved;
        }
    }

    // A failing listener must never kill the app from a timer thread, nor starve the other listeners
    // (live crash 2026-09-28): each one runs on its own, a failure is logged once per feed and polling goes on.
    private void Raise<THandler>(string feed, THandler? handlers, Action<THandler> call) where THandler : Delegate
    {
        if (handlers is null) return;
        lock (_deliver)
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
    public void PollTilesOnce() => Guarded("tiles", ref _tilesBusy, ReadTilesOnce);

    private void ReadTilesOnce()
    {
        SessionLease? lease;
        bool radarOnly;
        lock (_gate)
        {
            if (CurrentWorldKind == WorldKind.Overworld) return;
            radarOnly = CurrentWorldRadarOnly;
            lease = BorrowLocked();
        }
        if (lease is null) return;
        try
        {
            if (radarOnly) { Deliver(lease, RaiseStaticTiles); return; } // static worlds keep their offline tiles

            var tiles = lease.Reader.ReadTilesViaRoute();
            if (tiles is not null) { Deliver(lease, () => Raise("tiles", TilesUpdated, h => h(tiles))); return; }

            lock (_gate)
            {
                if (_session != lease || _fallbackScanTriggered) return;
                _fallbackScanTriggered = true;
            }
            RunFullScanInBackground();
        }
        finally { GiveBack(lease); }
    }

    /// <summary>Forces an extra full scan right now (task spec: "re-scan only on demand" after the automatic one).</summary>
    public void RescanTiles()
    {
        bool scans;
        lock (_gate) scans = CurrentWorldKind is not (WorldKind.RadarOnly or WorldKind.Overworld);
        if (scans) RunFullScanInBackground();
    }

    // Scans with the current session, borrowed until the scan ends (it takes seconds, a world change can come meanwhile).
    private void RunFullScanInBackground()
    {
        SessionLease? lease;
        lock (_gate) lease = BorrowLocked();
        if (lease is null) return;
        Task.Run(() =>
        {
            try
            {
                Guarded("full scan", () =>
                {
                    var tiles = lease.Reader.ReadTilesViaFullScan();
                    Deliver(lease, () => Raise("tiles", TilesUpdated, h => h(tiles)));
                });
            }
            finally { GiveBack(lease); }
        });
    }

    private void HandleSessionLost(SessionLease expectedSession)
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
