using System.Numerics;
using D4Assist.Navigation;
using D4Data.Readers;
using D4Memory.Actors;
using D4Memory.Safety;
using D4Memory.Scan;
using D4Memory.Ui;
using D4Memory.SceneRoute;

namespace D4Assist.Game;

/// <summary>
/// Production IDungeonSessionReader: opens the game process read-only, locates the main module and the
/// static player record once, then serves cheap repeated reads (research/client-memory.md §1/§3).
/// </summary>
public sealed class LiveDungeonSessionReader : IDungeonSessionReader
{
    // Matches D4MemScan's Commands/GameData.MainModuleFileName — the exe's on-disk file name, next to
    // the install directory each command/session is pointed at.
    public const string ExecutableFileName = "Diablo IV.exe";

    private readonly ProcessReader _process;
    private readonly CandidateIndex _candidates;
    private GameBuildOffsets? _offsets;
    private DateTime _nextSceneRecordCheck = DateTime.MinValue;
    private readonly long _moduleBase;
    private readonly ModuleRange _module;
    private readonly MenuScreenTracker? _menus;
    private readonly long _positionAddress;
    private readonly Func<int, bool> _readsFlags;
    private readonly int _playerActorSno;
    private readonly ActiveQuests? _activeQuests;
    private readonly int _worldSno;
    // Where each scene was placed this visit (tile reads), to land scene-local quest markers in the world.
    private readonly Dictionary<int, List<(Vector3 Wp, Quaternion Q)>> _placements = [];
    private IReadOnlyList<PlacedMarker> _questPlaces = [];
    private readonly BlueprintPlacement _blueprints = new();
    private IReadOnlyList<(int Sno, Vector3 Position, bool Marked)> _questGizmosAhead = [];
    private IReadOnlySet<int> _questActorSnos = new HashSet<int>();
    private readonly Func<int, IReadOnlyList<TileActor>>? _tileGizmos;
    private readonly MapMarkerTracker? _mapMarkers;
    private readonly ConversationAddresses? _conversations;
    private readonly uint _build;
    private readonly IReadOnlyList<MemoryRegion> _moduleData;
    private bool _actorFailureLogged;
    private AcdFlagReader? _flags;
    private bool _flagsMissing;
    private uint? _playerAcdId;
    private readonly ReadHealth? _health;
    private readonly SwitchStateLog? _switches;
    private readonly Func<int, int?>? _questOwner;
    private readonly SteadyInvulnerability _invulnerability = new();

    // The actors' flags, plus the player's cinematic flag.
    private static readonly string[] AcdNames = [.. ActorFilter.AcdFlags, AcdFlagReader.RtcPlaying];

    // With the switch log, its attributes too, when the game's table has them.
    private static readonly string[] DiagnosticAcdNames = [.. AcdNames, .. SwitchStateLog.Attributes.Except(AcdNames)];

    private LiveDungeonSessionReader(
        ProcessReader process, CandidateIndex candidates, GameBuildOffsets? offsets, ModuleRange module, long positionAddress, MenuScreenTracker? menus,
        Func<int, bool> readsFlags, int playerActorSno, Func<int, IReadOnlyList<QuestPhaseObjects>>? questMarkers,
        ConversationAddresses? conversations, uint build, IReadOnlyList<MemoryRegion> moduleData, int worldSno,
        Func<QuestSearchArea, IReadOnlyList<int>>? questsHere, ReadHealth? health, SwitchStateLog? switches,
        Func<int, IReadOnlyList<TileActor>>? tileGizmos, MapMarkerTracker? mapMarkers, Func<int, int?>? questOwner,
        QuestPhaseHistory? questHistory)
    {
        _questOwner = questOwner;
        _health = health;
        _switches = switches;
        _tileGizmos = tileGizmos;
        _mapMarkers = mapMarkers;
        (_conversations, _build, _moduleData) = (conversations, build, moduleData);
        _worldSno = worldSno;
        _readsFlags = readsFlags;
        _playerActorSno = playerActorSno;
        _activeQuests = questMarkers is null ? null : new ActiveQuests(process, moduleData, questMarkers, questsHere, worldSno: worldSno, history: questHistory);
        _process = process;
        _candidates = candidates;
        _offsets = offsets;
        _moduleBase = module.Base;
        _module = module;
        _menus = menus;
        _positionAddress = positionAddress;
    }

    /// <summary>Throws InvalidOperationException when the game isn't running, and GameUnreadableException with the reason when
    /// the process can't be read, the module can't be located, or no live player record is in this world yet.</summary>
    /// <param name="readsFlags">True for an actor SNO whose ACD flags are read (gizmos and NPCs); null reads none.</param>
    /// <param name="questMarkers">The objects each phase of a quest names (QuestScriptMarkerReader.PhaseObjects); null ignores quests.</param>
    /// <param name="questsHere">The quests with markers in the world, its placed scenes or on the loaded actors
    /// (QuestScriptMarkerReader), followed with the tracked ones while in progress; null for none.</param>
    /// <param name="health">Told whether the addresses, rooms, actors, attributes and menus read; null checks nothing.</param>
    /// <param name="isActorSno">True for an actor SNO the game files hold; with it the structure layout is checked live
    /// (LayoutDiscovery), null keeps it as known.</param>
    /// <param name="switches">Debug log of the multi-use switches' state signals, one per world session; null for none.</param>
    /// <param name="tileGizmos">The gizmos a scene places that a quest may name by SNO (TileActorReader.In); null for none.</param>
    /// <param name="mapMarkers">The client's map-marker list, where server-placed objectives stand (doors, levers); null for none.</param>
    /// <param name="questOwner">The quest a quest chest belongs to by name (QuestOwnerIndex); it stays locked while that quest is not
    /// followed. Null for none.</param>
    /// <param name="questHistory">The last phase of each quest read in any world, shared by every world session; null for this one's own.</param>
    public static LiveDungeonSessionReader? TryOpen(
        string installPath, CandidateIndex candidates, IReadOnlySet<int> playerActorSnos, int worldSno, IReadOnlySet<int> sceneSnos,
        MenuScreenTracker? menus = null, Func<int, bool>? readsFlags = null, Func<int, IReadOnlyList<QuestPhaseObjects>>? questMarkers = null,
        ConversationAddresses? conversations = null, Func<QuestSearchArea, IReadOnlyList<int>>? questsHere = null,
        ReadHealth? health = null, Func<int, bool>? isActorSno = null, SwitchStateLog? switches = null,
        Func<int, IReadOnlyList<TileActor>>? tileGizmos = null, MapMarkerTracker? mapMarkers = null, Func<int, int?>? questOwner = null,
        QuestPhaseHistory? questHistory = null)
    {
        ProcessReader process;
        using (var game = ProcessLocator.Find()) // InvalidOperationException when the game isn't running; caller decides how to react
        {
            try { process = ProcessReader.Open(game.Id); }
            catch (InvalidOperationException e) { throw new GameUnreadableException(e.Message); }
        }
        // Any failure from here on (the exe locked by an update, a read that throws) must not leak the process handle.
        try
        {
            return OpenWith(process, installPath, candidates, playerActorSnos, worldSno, sceneSnos, menus, readsFlags, questMarkers,
                conversations, questsHere, health, isActorSno, switches, tileGizmos, mapMarkers, questOwner, questHistory);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private static LiveDungeonSessionReader OpenWith(
        ProcessReader process, string installPath, CandidateIndex candidates, IReadOnlySet<int> playerActorSnos, int worldSno,
        IReadOnlySet<int> sceneSnos, MenuScreenTracker? menus, Func<int, bool>? readsFlags,
        Func<int, IReadOnlyList<QuestPhaseObjects>>? questMarkers, ConversationAddresses? conversations,
        Func<QuestSearchArea, IReadOnlyList<int>>? questsHere, ReadHealth? health, Func<int, bool>? isActorSno,
        SwitchStateLog? switches, Func<int, IReadOnlyList<TileActor>>? tileGizmos, MapMarkerTracker? mapMarkers, Func<int, int?>? questOwner,
        QuestPhaseHistory? questHistory)
    {
        string exePath = Path.Combine(installPath, ExecutableFileName);
        // A store that keeps the exe outside the data root: identify the module by the running game's own file.
        if (!File.Exists(exePath) && ProcessLocator.ExecutablePath() is { } runningExe) exePath = runningExe;
        var regions = process.ImageRegions();
        var modules = ModuleLocator.LocateAll(regions, process, exePath);
        uint build = PeHeaderReader.ReadFromFile(exePath).TimeDateStamp;
        if (modules.Count == 0)
            throw new GameUnreadableException($"game module not found in the process (installed exe build 0x{build:X8}).");

        PlayerRecordHit? player = null;
        ModuleRange liveModule = default;
        foreach (var module in modules)
        {
            var hit = FindLivePlayerRecord(
                process, ModuleLocator.WritableRegionsAt(regions, module.Base), playerActorSnos, worldSno, sceneSnos);
            if (hit is null) continue;
            (player, liveModule) = (hit, new ModuleRange(module.Base, module.SizeOfImage));
            break;
        }
        if (player is not { } found)
        {
            string records = DescribePlayerRecords(process, regions, modules, playerActorSnos, worldSno, sceneSnos);
            throw new GameUnreadableException($"player not found in world {worldSno}; {records}");
        }

        string playerCopy = $"0x{liveModule.Base:X}";
        var (offsets, offsetsModule, addresses) = FindOffsets(process, regions, liveModule, modules, build);
        // The menus, conversations and quest phases are read in the copy that holds the game object too, and so is the player
        // record when that copy has it.
        if (offsets is not null && offsetsModule.Base != liveModule.Base)
        {
            if (FindLivePlayerRecord(process, ModuleLocator.WritableRegionsAt(regions, offsetsModule.Base), playerActorSnos, worldSno, sceneSnos)
                is { } there)
                (found, playerCopy) = (there, $"0x{offsetsModule.Base:X}");
            liveModule = offsetsModule;
        }
        Console.WriteLine($"Memory: player actor {found.ActorSno} in copy {playerCopy} " +
            $"(of {string.Join(", ", modules.Select(m => $"0x{m.Base:X}"))}), game build 0x{build:X8}, {addresses}" +
            (offsets is null ? " (no rooms, radar walls or actor sounds)." : "."));
        health?.Report(ReadCheck.Addresses, offsets is not null, addresses);
        if (offsets is not null && isActorSno is not null) offsets = LayoutFor(process, liveModule, build, offsets, found, isActorSno);
        return new LiveDungeonSessionReader(
            process, candidates, offsets, liveModule, found.PositionAddress, menus, readsFlags ?? (_ => false), found.ActorSno, questMarkers,
            conversations, build, ModuleLocator.WritableRegionsAt(regions, liveModule.Base), worldSno, questsHere, health, switches, tileGizmos, mapMarkers,
            questOwner, questHistory);
    }

    // The game maps its image more than once (research/client-memory.md §6), and the copy that holds the game object pointer
    // is not always the one the player record is found in (live 2026-10-01: the player in the first copy, G only in another).
    // A copy can hold G and still not be the one the game runs at (live 2026-10-05: the mapped view had G at the image's RVA
    // but no attribute table, and 0/89 screens), because the attribute names and the screen records point into the running
    // copy: the first copy where the whole discovery succeeds wins, and a copy with G alone only when none does.
    private static (GameBuildOffsets? Offsets, ModuleRange Module, string Report) FindOffsets(
        ProcessReader process, IReadOnlyList<MemoryRegion> regions, ModuleRange playerModule, List<MainModule> modules, uint build)
    {
        var misses = new List<string>();
        (GameBuildOffsets Offsets, ModuleRange Module, string Report)? partial = null;
        var copies = modules.Select(m => new ModuleRange(m.Base, m.SizeOfImage)).OrderBy(m => m.Base == playerModule.Base ? 0 : 1);
        foreach (var module in copies)
        {
            var offsets = OffsetsFor(process, regions, module, build, out string report);
            misses.Add($"0x{module.Base:X}: {report}");
            if (offsets is null) continue;
            string found = $"addresses in 0x{module.Base:X}: {report}";
            if (offsets.Acd is not null)
                return (offsets, module, misses.Count > 1 ? $"{found} (passed over {string.Join("; ", misses[..^1])})" : found);
            partial ??= (offsets, module, found);
        }
        return partial ?? (null, playerModule, string.Join("; ", misses));
    }

    // The structure layout holds for the whole build: checked once per build and copy, when the pools could be read.
    private static readonly Dictionary<(uint Build, long Base), GameBuildOffsets> CheckedLayouts = [];

    private static GameBuildOffsets LayoutFor(
        ProcessReader process, ModuleRange module, uint build, GameBuildOffsets offsets, PlayerRecordHit player, Func<int, bool> isActorSno)
    {
        lock (CheckedLayouts)
        {
            if (CheckedLayouts.TryGetValue((build, module.Base), out var known)) return known;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var checkedOffsets = LayoutDiscovery.CheckLive(
                process, module.Base, offsets, player.Position, player.ActorSno, isActorSno, out string report, out bool conclusive);
            Console.WriteLine($"Layout: {report} ({clock.ElapsedMilliseconds} ms).");
            if (conclusive) CheckedLayouts[(build, module.Base)] = checkedOffsets;
            return checkedOffsets;
        }
    }

    // A scene record that moved in its slot, found by LayoutDiscovery the first time the route reads no room.
    private static readonly Dictionary<(uint Build, long Base), int> SceneRecordOffsets = [];

    // The addresses hold for the whole build, so they are searched once per build and module copy and kept for later sessions.
    private static readonly Dictionary<(uint Build, long Base), GameBuildOffsets> DiscoveredByBuild = [];

    private static GameBuildOffsets? OffsetsFor(
        ProcessReader process, IReadOnlyList<MemoryRegion> regions, ModuleRange module, uint build, out string report)
    {
        lock (DiscoveredByBuild)
        {
            if (DiscoveredByBuild.TryGetValue((build, module.Base), out var known)) { report = "known"; return known; }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var offsets = BuildOffsetsDiscovery.Discover(process, regions, module, out report);
            report += $" ({clock.ElapsedMilliseconds} ms)";
            if (offsets is not null) DiscoveredByBuild[(build, module.Base)] = offsets;
            return offsets;
        }
    }

    // Which field stops the match: the log line stays the same while walking unless that changes.
    private static string DescribePlayerRecords(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> regions, List<MainModule> modules, IReadOnlySet<int> playerActorSnos,
        int worldSno, IReadOnlySet<int> sceneSnos)
    {
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        var hits = new List<PlayerRecordHit>();
        foreach (var module in modules)
            foreach (var region in ModuleLocator.WritableRegionsAt(regions, module.Base))
                foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
                    hits.AddRange(PlayerRecordMatcher.FindLooseHits(data.Span, address, playerActorSnos));
        if (hits.Count == 0) return $"no record holds a player actor ({playerActorSnos.Count} known).";

        var described = hits.OrderByDescending(PlayerRecordMatcher.IsLive).Take(3).Select(h =>
            $"world {h.WorldSno}{(h.WorldSno == worldSno ? "" : " (differs)")}, " +
            (sceneSnos.Contains(h.SceneSno) ? "scene known" : $"scene {h.SceneSno} unknown") +
            $", actor {h.ActorSno}, flag {h.TrailingWord}");
        return $"{hits.Count} candidate record(s): " + string.Join("; ", described) + ".";
    }

    private static PlayerRecordHit? FindLivePlayerRecord(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> regions, IReadOnlySet<int> playerActorSnos, int worldSno,
        IReadOnlySet<int> sceneSnos)
    {
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in regions)
            foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
                foreach (var hit in PlayerRecordMatcher.FindHits(data.Span, address, playerActorSnos, worldSno, sceneSnos))
                    if (PlayerRecordMatcher.IsLive(hit))
                        return hit;
        return null;
    }

    public PlayerState? ReadPlayer()
    {
        var buffer = new byte[PlayerSampleWindow.WindowSize];
        long windowStart = _positionAddress - PlayerSampleWindow.PositionOffsetInWindow;
        if (!_process.TryRead(windowStart, buffer, buffer.Length)) return null;

        var sample = PlayerSampleWindow.Parse(buffer);
        return new PlayerState(sample.Position.X, sample.Position.Y, sample.Position.Z, sample.SceneSno);
    }

    public IReadOnlyList<TileState>? ReadTilesViaRoute()
    {
        if (_offsets is not { } offsets) return null;
        lock (SceneRecordOffsets)
            if (SceneRecordOffsets.TryGetValue((_build, _moduleBase), out int recordOffset) && recordOffset != offsets.SceneRecordOffset)
                _offsets = offsets = offsets with { SceneRecordOffset = recordOffset };
        bool read = SceneRouteReader.TryRead(_process, _moduleBase, offsets, _candidates, out var hits);
        if (read && hits.Count == 0 && DateTime.UtcNow >= _nextSceneRecordCheck) CheckSceneRecord(offsets);
        _health?.Report(ReadCheck.Rooms, read && hits.Count > 0, read ? "the scene route holds no known room" : "the scene route is unreadable");
        return read ? ToTileStates(hits) : null;
    }

    // While the route reads no room, every SceneRecordCheckInterval: a scene record that moved in its slot is found again.
    private static readonly TimeSpan SceneRecordCheckInterval = TimeSpan.FromSeconds(10);

    private void CheckSceneRecord(GameBuildOffsets offsets)
    {
        _nextSceneRecordCheck = DateTime.UtcNow + SceneRecordCheckInterval;
        if (LayoutDiscovery.CheckSceneRecordLive(_process, _moduleBase, offsets, _candidates) is not { } found) return;
        if (found == offsets.SceneRecordOffset) return;
        Console.WriteLine($"Layout: scene record MOVED 0x{offsets.SceneRecordOffset:X} -> 0x{found:X}.");
        lock (SceneRecordOffsets) SceneRecordOffsets[(_build, _moduleBase)] = found;
    }

    public ConversationSnapshot? ReadConversation()
    {
        if (_conversations is null) return null;
        _conversations.SearchIfDue(_process, _moduleData, _module, _build);
        return _conversations.For(_build) is { } conversation ? ConversationReader.TryRead(_process, _moduleBase, conversation) : null;
    }

    public IReadOnlyList<string>? ReadMenus()
    {
        if (_menus is null) return null;
        var open = _menus.Read(_process, _module);
        _health?.Report(ReadCheck.Menus, open is not null, "no watched menu screen located");
        return open;
    }

    // The player's RTC Playing flag (research/runtime-signals.md §2); the ACD id comes from the last actor read.
    public bool? ReadCinematic() =>
        _flags is { } flags && _playerAcdId is { } acd && flags.Known.Contains(AcdFlagReader.RtcPlaying)
            ? flags.Values(acd).ContainsKey(AcdFlagReader.RtcPlaying)
            : null;

    // The player's own record: the closest one with the player's actor SNO (another player of the same class can be near).
    private RActorRecord? PlayerRecord(IReadOnlyList<RActorRecord> records, PlayerState? p) =>
        p is not null
            ? records.Where(r => r.ActorSno == _playerActorSno && r.AcdId != RActorRecord.NoAcd)
                .OrderBy(r => Vector2.DistanceSquared(new(r.Position.X, r.Position.Y), new(p.X, p.Y))).Select(r => (RActorRecord?)r).FirstOrDefault()
            : null;

    // The player record and the player's RActor are written at different moments; a moved field reads garbage far away.
    private const float OwnActorReach = 5f;

    // The actor pool reads right when the player's own actor is in it, where the player record says the player is.
    private void ReportOwnActor(RActorRecord? self, PlayerState? p, int count)
    {
        if (_health is null || p is null) return;
        bool found = self is { } r && Vector2.Distance(new(r.Position.X, r.Position.Y), new(p.X, p.Y)) <= OwnActorReach;
        _health.Report(ReadCheck.Actors, found, $"the player's actor {_playerActorSno} is not among the {count} actors read");
    }

    // Opened on first use and retried while the pool is unreadable; a build whose table lacks the attribute gives up once.
    private AcdFlagReader? FlagReader(GameBuildOffsets offsets)
    {
        if (_flags is not null || _flagsMissing) return _flags;
        if (offsets.Acd is null)
        {
            _health?.Report(ReadCheck.Attributes, ok: false, "no ACD pool among this build's addresses");
            return null;
        }
        if (!AcdReader.TryOpen(_process, _moduleBase, offsets, out _)) return null;
        _flags = AcdFlagReader.TryOpen(_process, _moduleBase, offsets, _switches is null ? AcdNames : DiagnosticAcdNames);
        var missing = AcdNames.Except(_flags?.Known ?? []).ToList();
        if (missing.Count > 0) Console.WriteLine($"ACD attributes not found: {string.Join(", ", missing)}.");
        _health?.Report(ReadCheck.Attributes, missing.Count == 0, $"attributes not found: {string.Join(", ", missing)}");
        _flagsMissing = _flags is null;
        return _flags;
    }

    public IReadOnlyList<ActorState>? ReadActors()
    {
        if (_offsets is { } offsets && RActorReader.TryRead(_process, _moduleBase, offsets, out var records))
        {
            var position = ReadPlayer();
            var player = PlayerRecord(records, position);
            ReportOwnActor(player, position, records.Count);
            var flags = FlagReader(offsets);
            flags?.Refresh();
            _switches?.Observe(records, r => r.AcdId == RActorRecord.NoAcd ? null : flags?.Values(r.AcdId), flags?.Known ?? []);
            if (flags is null) return ActorFilter.Placed(records);
            _playerAcdId = player?.AcdId;
            var named = NamedByActiveQuests(player, flags, records);
            var marked = _mapMarkers?.Read(_process, _moduleBase, offsets.GameObjectPointerRva) ?? [];
            var places = named.Markers.Concat(InWorld(named.SceneMarkers))
                .Concat(named.KindMarkers.Count == 0 ? [] : _blueprints.Place(named.KindMarkers,
                    [.. records.Where(r => r.IsPlaced).Select(r => (r.ActorSno, r.Position)), .. marked.Select(m => (m.ActorSno, m.Position))])).ToList();
            _questPlaces = places;
            _questActorSnos = named.ActorSnos;
            var mapped = MarkedGizmos(marked);
            _questGizmosAhead = [.. PlacedByTiles(named.ActorSnos).Select(g => (g.Sno, g.Position, false)),
                .. mapped.Where(g => named.ActorSnos.Contains(g.Sno)).Select(g => (g.Sno, g.Position, true))];
            var actors = _invulnerability.Apply(ActorFilter.Placed(records, r => r.AcdId != RActorRecord.NoAcd && _readsFlags(r.ActorSno) ? flags.Read(r.AcdId) : null,
                r => Naming(r, named, places), r => mapped.Any(g => g.Sno == r.ActorSno &&
                    Vector2.Distance(new(g.Position.X, g.Position.Y), new(r.Position.X, r.Position.Y)) <= ActiveQuests.MarkerReach)));
            return _questOwner is null || _activeQuests is null ? actors : ActorFilter.LockOthersQuests(actors, _questOwner, _activeQuests.Owned);
        }

        if (_offsets is not null) _health?.Report(ReadCheck.Actors, ok: false, "the actor pool is unreadable");
        if (!_actorFailureLogged) // once per session: the route is either unknown for this build or not yet populated
        {
            _actorFailureLogged = true;
            Console.WriteLine("Actor pool unavailable; beacons wait for it.");
        }
        return null;
    }

    // A scene-local marker lands where its scene is placed: world = wp + rotate(q, local) (research/client-memory.md §5). A
    // scene not placed yet, or placed more than once, says nothing.
    private IEnumerable<PlacedMarker> InWorld(IReadOnlyList<SceneMarker> markers)
    {
        if (markers.Count == 0) yield break;
        lock (_placements)
            foreach (var (scene, marker) in markers)
                if (_placements.TryGetValue(scene, out var at) && at.Count == 1)
                    yield return marker with { Position = at[0].Wp + Vector3.Transform(marker.Position, at[0].Q) };
    }

    // What the active quests' current phases name (research/client-memory.md §4); the player's record holds the tracked quests.
    private QuestNames NamedByActiveQuests(RActorRecord? player, AcdFlagReader flags, IReadOnlyList<RActorRecord> records)
    {
        if (_activeQuests is null) return QuestNames.None;
        int[]? tracked = player is { } self ? TrackedQuests(flags.Values(self.AcdId)) : null;
        return _activeQuests.Update(tracked, () =>
        {
            int[] scenes;
            lock (_placements) scenes = [.. _placements.Keys];
            return new QuestSearchArea(_worldSno, scenes, [.. records.Where(r => r.IsPlaced).Select(r => (r.ActorSno, r.Position))]);
        });
    }

    private static int[] TrackedQuests(IReadOnlyDictionary<string, uint> playerFlags) =>
        [.. new[] { AcdFlagReader.TrackedQuest, AcdFlagReader.OverrideTrackedQuest }
            .Select(name => playerFlags.TryGetValue(name, out uint sno) ? (int)sno : 0).Where(sno => sno > 0).Distinct().Order()];

    // A named marker of the actor's SNO at its spot, or its SNO named outright, names this actor; one elsewhere (or a blueprint's
    // not placed yet), its kind.
    private static QuestNaming Naming(RActorRecord r, QuestNames named, IReadOnlyList<PlacedMarker> places)
    {
        if (named.ActorSnos.Contains(r.ActorSno)) return QuestNaming.It;
        bool kind = false;
        foreach (var m in places)
        {
            if (m.Sno != r.ActorSno) continue;
            if (Vector2.Distance(new(m.Position.X, m.Position.Y), new(r.Position.X, r.Position.Y)) <= ActiveQuests.MarkerReach) return QuestNaming.It;
            kind = true;
        }
        return kind || named.KindMarkers.Any(m => m.Marker.Sno == r.ActorSno) ? QuestNaming.ItsKind : QuestNaming.None;
    }

    public IReadOnlyList<QuestPlace>? ReadQuestPlaces() =>
        _activeQuests is null ? null :
        [
            .. _questPlaces.Select(m => new QuestPlace(m.Sno, m.Position.X, m.Position.Y, m.Position.Z)),
            .. _questGizmosAhead.Select(g => new QuestPlace(g.Sno, g.Position.X, g.Position.Y, g.Position.Z, Ahead: true, g.Marked)),
        ];

    public int QuestPhaseChanges => _activeQuests?.PhaseChanges ?? 0;

    public IReadOnlyList<MarkedActor>? ReadMapMarkers() =>
        _offsets is { } offsets && _mapMarkers?.Read(_process, _moduleBase, offsets.GameObjectPointerRva) is { } markers
            ? [.. markers.Select(m => new MarkedActor(m.ActorSno, m.Position.X, m.Position.Y, m.Position.Z, m.LevelAreaSno, m.Kind))]
            : null;

    public IReadOnlySet<int>? ReadQuestActorSnos() => _activeQuests is null ? null : _questActorSnos;

    public IReadOnlyList<int>? ReadOwnedQuests() => _activeQuests?.Owned;

    public IReadOnlyDictionary<int, uint>? FindQuestPhases(IReadOnlyDictionary<int, IReadOnlySet<uint>> quests) =>
        _activeQuests is null ? null : QuestPhaseLocator.FindAllCandidates(_process, _moduleData, quests)
            .Select(found => (Quest: found.Key, found.Value.First(c => c.Address == QuestPhaseLocator.Pick(found.Value)).Phase))
            .Where(found => found.Phase != 0).ToDictionary(found => found.Quest, found => found.Phase);

    public IReadOnlyList<int>? ReadActivatedWaypoints(IReadOnlySet<int> waypointLevelAreas) =>
        _offsets is { } offsets ? ActivatedWaypointsReader.Read(_process, _moduleBase, offsets.GameObjectPointerRva, waypointLevelAreas) : null;

    public MapView? ReadMapView() => _offsets is { } offsets ? MapViewReader.Read(_process, _moduleBase, offsets.GameObjectPointerRva) : null;

    // The gizmos and triggers the map marks, wherever they are: those the current phases name are quest places even out of reach
    // (the server places some, the Labyrinth's lair door, and the game only streams actors in about 25 units around the player);
    // a quest gizmo marked is open to the player (ActorFilter).
    private static List<(int Sno, Vector3 Position)> MarkedGizmos(IReadOnlyList<MapMarker> markers) =>
        [.. markers.Where(m => m.Kind == MapMarkerReader.KindGizmo).Select(m => (m.ActorSno, m.Position))];

    // The gizmos the current phases name by SNO alone, wherever the loaded tiles place them.
    private List<(int Sno, Vector3 Position)> PlacedByTiles(IReadOnlySet<int> snos)
    {
        if (_tileGizmos is null || snos.Count == 0) return [];
        List<(int Scene, Vector3 Wp, Quaternion Q)> placed;
        lock (_placements) placed = [.. _placements.SelectMany(kv => kv.Value.Select(p => (kv.Key, p.Wp, p.Q)))];
        return [.. placed.SelectMany(p => _tileGizmos(p.Scene).Where(g => snos.Contains(g.ActorSno))
            .Select(g => (g.ActorSno, p.Wp + Vector3.Transform(g.Local, p.Q))))];
    }

    public IReadOnlyList<TileState> ReadTilesViaFullScan() =>
        ToTileStates(new SceneScanner().Scan(_process, _candidates).Tiles);

    private List<TileState> ToTileStates(IReadOnlyList<CSceneHit> hits)
    {
        var result = new List<TileState>(hits.Count);
        foreach (var hit in hits)
        {
            bool known = _candidates.TryGet(hit.Sno, out var candidate);
            var size = known ? candidate.TileSize : [];
            float sizeX = size.Length > 0 ? size[0] : 0f;
            float sizeY = size.Length > 1 ? size[1] : 0f;
            float yaw = TileRotation.YawDegrees(hit.Q.Z, hit.Q.W);
            lock (_placements)
            {
                var at = _placements.TryGetValue(hit.Sno, out var list) ? list : _placements[hit.Sno] = [];
                if (!at.Any(p => Vector3.Distance(p.Wp, hit.Wp) < 1f)) at.Add((hit.Wp, hit.Q));
            }
            result.Add(new TileState(
                hit.Sno, hit.Name, hit.Wp.X, hit.Wp.Y, sizeX, sizeY, hit.ExitMask, hit.NodeId, hit.GroupId, yaw, known ? candidate.ExitMask : hit.ExitMask));
        }
        return result;
    }

    public void Dispose() => _process.Dispose();
}
