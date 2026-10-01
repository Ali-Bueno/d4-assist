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
    private readonly GameBuildOffsets? _offsets;
    private readonly long _moduleBase;
    private readonly ModuleRange _module;
    private readonly MenuScreenTracker? _menus;
    private readonly long _positionAddress;
    private readonly Func<int, bool> _readsFlags;
    private readonly int _playerActorSno;
    private readonly Func<int, IReadOnlyList<QuestPhaseObjects>>? _questMarkers;
    private readonly int _worldSno;
    private readonly Func<int, IReadOnlyCollection<int>, IReadOnlyList<int>>? _worldQuests;
    // Where each scene was placed this visit (tile reads), to land scene-local quest markers in the world.
    private readonly Dictionary<int, List<(Vector3 Wp, Quaternion Q)>> _placements = [];
    private IReadOnlyList<PlacedMarker> _questPlaces = [];
    private IReadOnlyDictionary<int, long> _questsInProgressHere = new Dictionary<int, long>();
    private DateTime _nextWorldQuestSearch = DateTime.MinValue;
    private readonly ConversationAddresses? _conversations;
    private readonly uint _build;
    private readonly IReadOnlyList<MemoryRegion> _moduleData;
    private int[] _trackedQuests = [];
    private List<TrackedQuest> _quests = [];
    private QuestNames _namedByQuests = QuestNames.None;
    private bool _actorFailureLogged;
    private AcdFlagReader? _flags;
    private bool _flagsMissing;
    private uint? _playerAcdId;

    // The actors' flags, plus the player's cinematic flag.
    private static readonly string[] AcdNames = [.. ActorFilter.AcdFlags, AcdFlagReader.RtcPlaying];

    private LiveDungeonSessionReader(
        ProcessReader process, CandidateIndex candidates, GameBuildOffsets? offsets, ModuleRange module, long positionAddress, MenuScreenTracker? menus,
        Func<int, bool> readsFlags, int playerActorSno, Func<int, IReadOnlyList<QuestPhaseObjects>>? questMarkers,
        ConversationAddresses? conversations, uint build, IReadOnlyList<MemoryRegion> moduleData, int worldSno,
        Func<int, IReadOnlyCollection<int>, IReadOnlyList<int>>? worldQuests)
    {
        (_conversations, _build, _moduleData) = (conversations, build, moduleData);
        (_worldSno, _worldQuests) = (worldSno, worldQuests);
        _readsFlags = readsFlags;
        _playerActorSno = playerActorSno;
        _questMarkers = questMarkers;
        _process = process;
        _candidates = candidates;
        _offsets = offsets;
        _moduleBase = module.Base;
        _module = module;
        _menus = menus;
        _positionAddress = positionAddress;
    }

    /// <summary>Throws InvalidOperationException with the reason when the process isn't running or can't be read, the
    /// module can't be located, or no live player record is in this world yet.</summary>
    /// <param name="readsFlags">True for an actor SNO whose ACD flags are read (gizmos and NPCs); null reads none.</param>
    /// <param name="questMarkers">The objects each phase of a quest names (QuestScriptMarkerReader.PhaseObjects); null ignores the tracked quest.</param>
    /// <param name="worldQuests">The quests with markers in a world or in the scenes placed in it so far
    /// (QuestScriptMarkerReader.QuestsWithMarkersIn), followed when the player tracks none; null for none.</param>
    public static LiveDungeonSessionReader? TryOpen(
        string installPath, CandidateIndex candidates, IReadOnlySet<int> playerActorSnos, int worldSno, IReadOnlySet<int> sceneSnos,
        MenuScreenTracker? menus = null, Func<int, bool>? readsFlags = null, Func<int, IReadOnlyList<QuestPhaseObjects>>? questMarkers = null,
        ConversationAddresses? conversations = null, Func<int, IReadOnlyCollection<int>, IReadOnlyList<int>>? worldQuests = null)
    {
        var process = ProcessReader.OpenDiabloIv(); // throws InvalidOperationException if not running; caller decides how to react
        string exePath = Path.Combine(installPath, ExecutableFileName);
        var regions = process.ImageRegions();
        var modules = ModuleLocator.LocateAll(regions, process, exePath);
        uint build = PeHeaderReader.ReadFromFile(exePath).TimeDateStamp;
        if (modules.Count == 0)
        {
            process.Dispose();
            throw new InvalidOperationException($"game module not found in the process (installed exe build 0x{build:X8}).");
        }

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
            process.Dispose();
            throw new InvalidOperationException($"player not found in world {worldSno}; {records}");
        }

        GameBuildOffsets? offsets = OffsetsFor(process, regions, liveModule, build, out string addresses);
        Console.WriteLine($"Memory: player actor {found.ActorSno}, game build 0x{build:X8}, {addresses}" +
            (offsets is null ? " (no rooms, radar walls or actor sounds)." : "."));
        return new LiveDungeonSessionReader(
            process, candidates, offsets, liveModule, found.PositionAddress, menus, readsFlags ?? (_ => false), found.ActorSno, questMarkers,
            conversations, build, ModuleLocator.WritableRegionsAt(regions, liveModule.Base), worldSno, worldQuests);
    }

    // The addresses hold for the whole build, so they are searched once per build and kept for later sessions.
    private static readonly Dictionary<uint, GameBuildOffsets> DiscoveredByBuild = [];

    private static GameBuildOffsets? OffsetsFor(
        ProcessReader process, IReadOnlyList<MemoryRegion> regions, ModuleRange module, uint build, out string report)
    {
        lock (DiscoveredByBuild)
        {
            if (DiscoveredByBuild.TryGetValue(build, out var known)) { report = "addresses known"; return known; }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var offsets = BuildOffsetsDiscovery.Discover(process, regions, module, out report);
            report += $" ({clock.ElapsedMilliseconds} ms)";
            if (offsets is not null) DiscoveredByBuild[build] = offsets;
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
        return SceneRouteReader.TryRead(_process, _moduleBase, offsets, _candidates, out var hits) ? ToTileStates(hits) : null;
    }

    public ConversationSnapshot? ReadConversation()
    {
        if (_conversations is null) return null;
        _conversations.SearchIfDue(_process, _moduleData, _module, _build);
        return _conversations.For(_build) is { } conversation ? ConversationReader.TryRead(_process, _moduleBase, conversation) : null;
    }

    public IReadOnlyList<string>? ReadMenus() => _menus?.Read(_process, _module);

    // The player's RTC Playing flag (research/runtime-signals.md §2); the ACD id comes from the last actor read.
    public bool? ReadCinematic() =>
        _flags is { } flags && _playerAcdId is { } acd && flags.Known.Contains(AcdFlagReader.RtcPlaying)
            ? flags.Values(acd).ContainsKey(AcdFlagReader.RtcPlaying)
            : null;

    // The player's own record: the closest one with the player's actor SNO (another player of the same class can be near).
    private RActorRecord? PlayerRecord(IReadOnlyList<RActorRecord> records) =>
        ReadPlayer() is { } p
            ? records.Where(r => r.ActorSno == _playerActorSno && r.AcdId != RActorRecord.NoAcd)
                .OrderBy(r => Vector2.DistanceSquared(new(r.Position.X, r.Position.Y), new(p.X, p.Y))).Select(r => (RActorRecord?)r).FirstOrDefault()
            : null;

    // Opened on first use and retried while the pool is unreadable; a build whose table lacks the attribute gives up once.
    private AcdFlagReader? FlagReader(GameBuildOffsets offsets)
    {
        if (_flags is not null || _flagsMissing || offsets.Acd is null) return _flags;
        if (!AcdReader.TryOpen(_process, _moduleBase, offsets, out _)) return null;
        _flags = AcdFlagReader.TryOpen(_process, _moduleBase, offsets, AcdNames);
        if (_flags is null || _flags.Known.Count < AcdNames.Length)
            Console.WriteLine($"ACD attributes not found: {string.Join(", ", AcdNames.Except(_flags?.Known ?? []))}.");
        _flagsMissing = _flags is null;
        return _flags;
    }

    public IReadOnlyList<ActorState>? ReadActors()
    {
        if (_offsets is { } offsets && RActorReader.TryRead(_process, _moduleBase, offsets, out var records))
        {
            var flags = FlagReader(offsets);
            if (flags is null) return ActorFilter.Placed(records);
            flags.Refresh();
            var player = PlayerRecord(records);
            _playerAcdId = player?.AcdId;
            var named = NamedByTrackedQuests(player, flags);
            var places = named.Markers.Concat(InWorld(named.SceneMarkers)).ToList();
            _questPlaces = places;
            return ActorFilter.Placed(records, r => r.AcdId != RActorRecord.NoAcd && _readsFlags(r.ActorSno) ? flags.Read(r.AcdId) : null,
                r => named.ActorSnos.Contains(r.ActorSno) || places.Any(m => m.Sno == r.ActorSno
                    && Vector2.Distance(new(m.Position.X, m.Position.Y), new(r.Position.X, r.Position.Y)) <= QuestMarkerReach));
        }

        if (!_actorFailureLogged) // once per session: the route is either unknown for this build or not yet populated
        {
            _actorFailureLogged = true;
            Console.WriteLine("Actor pool unavailable; beacons wait for it.");
        }
        return null;
    }

    // A quest's marker and the actor it placed share a spot (live: the Darkened Way gate, 0.0 apart).
    private const float QuestMarkerReach = 2f;

    // What the tracked quests' current phases name: markers (world space, or scene-local until the scene is placed), matched by
    // spot, and actors named by SNO alone (any placed copy).
    private sealed record QuestNames(IReadOnlyList<PlacedMarker> Markers, IReadOnlySet<int> ActorSnos, IReadOnlyList<SceneMarker> SceneMarkers)
    {
        public static readonly QuestNames None = new([], new HashSet<int>(), []);

        public static QuestNames Of(IEnumerable<QuestPhaseObjects> phases) =>
            new([.. phases.SelectMany(p => p.Markers)], phases.SelectMany(p => p.ActorSnos).ToHashSet(), [.. phases.SelectMany(p => p.SceneMarkers)]);

        public int Count => Markers.Count + ActorSnos.Count + SceneMarkers.Count;
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

    // A tracked quest's phases (offline) and where its current phase is read (found in the module's data).
    private sealed class TrackedQuest(int sno, IReadOnlyList<QuestPhaseObjects> phases)
    {
        public int Sno { get; } = sno;
        public IReadOnlyList<QuestPhaseObjects> Phases { get; } = phases;
        public HashSet<uint> Uids { get; } = [.. phases.Select(p => p.PhaseUid)];
        public long? PhaseAddress { get; set; }
        public uint? Phase { get; set; }
        public bool PhaseSeen { get; set; }
        // Found in progress rather than tracked: names nothing until its phase is read.
        public bool RequirePhase { get; init; }
        public DateTime NextSearch { get; set; } = DateTime.MinValue;

        // The current phase. Before the phase is ever found, every phase (a later step can then sound early);
        // once seen, a lost phase names nothing: the quest ended while it stays tracked (live 2026-09-30, ~50 s).
        public IEnumerable<QuestPhaseObjects> Current =>
            Phase is { } current ? Phases.Where(p => p.PhaseUid == current) : PhaseSeen || RequirePhase ? [] : Phases;
    }

    // The phase pair can move (a new quest record); a failed search waits this long before the next scan of the module data.
    private static readonly TimeSpan PhaseSearchInterval = TimeSpan.FromSeconds(5);

    // The objects the player's tracked quests name in their current phase (research/client-memory.md §4).
    private QuestNames NamedByTrackedQuests(RActorRecord? player, AcdFlagReader flags)
    {
        if (_questMarkers is null) return _namedByQuests;
        if (UpdateQuestPhases(log: true)) _namedByQuests = QuestNames.Of(_quests.SelectMany(q => q.Current));
        if (player is not { } self) return _namedByQuests;
        var values = flags.Values(self.AcdId);
        int[] quests = [.. new[] { AcdFlagReader.TrackedQuest, AcdFlagReader.OverrideTrackedQuest }
            .Select(name => values.TryGetValue(name, out uint sno) ? (int)sno : 0).Where(sno => sno > 0).Distinct().Order()];
        bool tracked = quests.Length > 0;
        if (!tracked) quests = [.. QuestsInProgressHere().Keys.Order()];
        if (quests.SequenceEqual(_trackedQuests)) return _namedByQuests;
        _trackedQuests = quests;
        _quests = [.. quests.Select(q => new TrackedQuest(q, _questMarkers(q))
        {
            RequirePhase = !tracked, PhaseAddress = !tracked && _questsInProgressHere.TryGetValue(q, out long at) ? at : null,
        })];
        UpdateQuestPhases(log: false);
        _namedByQuests = QuestNames.Of(_quests.SelectMany(q => q.Current));
        string which = !tracked && quests.Length > 0 ? "Quest in progress here (none tracked)" : "Quest tracked";
        Console.WriteLine($"{which}: {(quests.Length == 0 ? "none" : string.Join(", ", quests))}, {_namedByQuests.Count} object(s) named{PhaseText()}.");
        return _namedByQuests;
    }

    // Without a tracked quest (the HUD then follows the campaign quest), the quests with markers in this world whose
    // {quest, phase} pair the module data holds; one pass every PhaseSearchInterval (live 2026-10-01: QST_Frac_Underworld2_02
    // at phase 80 in the Ancient Mausoleum, none tracked).
    private IReadOnlyDictionary<int, long> QuestsInProgressHere()
    {
        if (_worldQuests is null || _questMarkers is null || DateTime.UtcNow < _nextWorldQuestSearch) return _questsInProgressHere;
        _nextWorldQuestSearch = DateTime.UtcNow + PhaseSearchInterval;
        int[] scenes;
        lock (_placements) scenes = [.. _placements.Keys];
        var candidates = _worldQuests(_worldSno, scenes)
            .ToDictionary(q => q, q => (IReadOnlySet<uint>)_questMarkers(q).Select(p => p.PhaseUid).ToHashSet());
        return _questsInProgressHere = candidates.Count == 0
            ? new Dictionary<int, long>()
            : QuestPhaseLocator.FindAll(_process, _moduleData, candidates);
    }

    public IReadOnlyList<QuestPlace>? ReadQuestPlaces() =>
        _questMarkers is null ? null : [.. _questPlaces.Select(m => new QuestPlace(m.Sno, m.Position.X, m.Position.Y, m.Position.Z))];

    // True when a quest's phase changed.
    private bool UpdateQuestPhases(bool log)
    {
        bool changed = false;
        foreach (var quest in _quests)
        {
            uint? phase = quest.PhaseAddress is { } at ? QuestPhaseLocator.Read(_process, at, quest.Sno, quest.Uids) : null;
            if (phase is null && DateTime.UtcNow >= quest.NextSearch)
            {
                quest.PhaseAddress = QuestPhaseLocator.Find(_process, _moduleData, quest.Sno, quest.Uids) is [var found, ..] ? found : null;
                quest.NextSearch = DateTime.UtcNow + PhaseSearchInterval;
                phase = quest.PhaseAddress is { } a ? QuestPhaseLocator.Read(_process, a, quest.Sno, quest.Uids) : null;
            }
            if (phase == quest.Phase) continue;
            quest.Phase = phase;
            quest.PhaseSeen |= phase is not null;
            changed = true;
            if (log) Console.WriteLine($"Quest phase: {quest.Sno} {(phase is { } v ? $"at {v}" : "unknown")}, {QuestNames.Of(quest.Current).Count} object(s) named.");
        }
        return changed;
    }

    private string PhaseText() =>
        string.Concat(_quests.Select(q => q.Phase is { } v ? $", phase {v}" : ", phase unknown"));

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
