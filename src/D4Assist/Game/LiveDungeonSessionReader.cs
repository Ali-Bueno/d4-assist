using D4Assist.Navigation;
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
    private readonly Func<int, bool> _isGizmo;
    private bool _actorFailureLogged;
    private GizmoOperatedReader? _operated;
    private bool _operatedMissing;

    private LiveDungeonSessionReader(
        ProcessReader process, CandidateIndex candidates, GameBuildOffsets? offsets, ModuleRange module, long positionAddress, MenuScreenTracker? menus,
        Func<int, bool> isGizmo)
    {
        _isGizmo = isGizmo;
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
    /// <param name="isGizmo">True for a gizmo actor SNO: only those get the ACD "used" read; null reads none.</param>
    public static LiveDungeonSessionReader? TryOpen(
        string installPath, CandidateIndex candidates, IReadOnlySet<int> playerActorSnos, int worldSno, IReadOnlySet<int> sceneSnos,
        MenuScreenTracker? menus = null, Func<int, bool>? isGizmo = null)
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

        GameBuildOffsets? offsets = GameBuildOffsetsTable.TryGet(build, out var known) ? known : null;
        Console.WriteLine($"Memory: player actor {found.ActorSno}, game build 0x{build:X8}" +
            (offsets is null ? " (unknown build: no rooms, radar or actor sounds)." : "."));
        return new LiveDungeonSessionReader(process, candidates, offsets, liveModule, found.PositionAddress, menus, isGizmo ?? (_ => false));
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

    public ConversationSnapshot? ReadConversation() =>
        _offsets?.Conversation is { } conversation ? ConversationReader.TryRead(_process, _moduleBase, conversation) : null;

    public IReadOnlyList<string>? ReadMenus() => _menus?.Read(_process, _module);

    // Opened on first use and retried while the pool is unreadable; a build whose table lacks the attribute gives up once.
    private GizmoOperatedReader? OperatedReader(GameBuildOffsets offsets)
    {
        if (_operated is not null || _operatedMissing || offsets.Acd is null) return _operated;
        if (!AcdReader.TryOpen(_process, _moduleBase, offsets, out _)) return null;
        _operated = GizmoOperatedReader.TryOpen(_process, _moduleBase, offsets);
        if (_operated is null)
        {
            _operatedMissing = true;
            Console.WriteLine($"ACD attribute {GizmoOperatedReader.AttributeName} not found; used gizmos keep the older signals.");
        }
        return _operated;
    }

    public IReadOnlyList<ActorState>? ReadActors()
    {
        if (_offsets is { } offsets && RActorReader.TryRead(_process, _moduleBase, offsets, out var records))
        {
            var operated = OperatedReader(offsets);
            return ActorFilter.Placed(records, operated is null ? null
                : r => r.AcdId != RActorRecord.NoAcd && _isGizmo(r.ActorSno) && operated.IsOperated(r.AcdId));
        }

        if (!_actorFailureLogged) // once per session: the route is either unknown for this build or not yet populated
        {
            _actorFailureLogged = true;
            Console.WriteLine("Actor pool unavailable; beacons wait for it.");
        }
        return null;
    }

    public IReadOnlyList<TileState> ReadTilesViaFullScan() =>
        ToTileStates(new SceneScanner().Scan(_process, _candidates).Tiles);

    private List<TileState> ToTileStates(IReadOnlyList<CSceneHit> hits)
    {
        var result = new List<TileState>(hits.Count);
        foreach (var hit in hits)
        {
            var size = _candidates.TryGet(hit.Sno, out var candidate) ? candidate.TileSize : [];
            float sizeX = size.Length > 0 ? size[0] : 0f;
            float sizeY = size.Length > 1 ? size[1] : 0f;
            float yaw = TileRotation.YawDegrees(hit.Q.Z, hit.Q.W);
            result.Add(new TileState(hit.Sno, hit.Name, hit.Wp.X, hit.Wp.Y, sizeX, sizeY, hit.ExitMask, hit.NodeId, hit.GroupId, yaw));
        }
        return result;
    }

    public void Dispose() => _process.Dispose();
}
