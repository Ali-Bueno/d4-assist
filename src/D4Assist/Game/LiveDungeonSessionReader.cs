using D4Assist.Navigation;
using D4Memory.Actors;
using D4Memory.Safety;
using D4Memory.Scan;
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
    private readonly long _positionAddress;
    private bool _actorFailureLogged;

    private LiveDungeonSessionReader(
        ProcessReader process, CandidateIndex candidates, GameBuildOffsets? offsets, long moduleBase, long positionAddress)
    {
        _process = process;
        _candidates = candidates;
        _offsets = offsets;
        _moduleBase = moduleBase;
        _positionAddress = positionAddress;
    }

    /// <summary>Null when the process isn't running, the module can't be located, or no live player record is found.</summary>
    public static LiveDungeonSessionReader? TryOpen(
        string installPath, CandidateIndex candidates, int worldSno, IReadOnlySet<int> sceneSnos)
    {
        var process = ProcessReader.OpenDiabloIv(); // throws InvalidOperationException if not running; caller decides how to react
        string exePath = Path.Combine(installPath, ExecutableFileName);
        var regions = process.ImageRegions();
        var modules = ModuleLocator.LocateAll(regions, process, exePath);
        if (modules.Count == 0) { process.Dispose(); return null; }

        long? liveModuleBase = null;
        long positionAddress = 0;
        foreach (var module in modules)
        {
            var hit = FindLivePlayerRecord(process, ModuleLocator.WritableRegionsAt(regions, module.Base), worldSno, sceneSnos);
            if (hit is null) continue;
            liveModuleBase = module.Base;
            positionAddress = hit.Value.PositionAddress;
            break;
        }
        if (liveModuleBase is not { } moduleBase) { process.Dispose(); return null; }

        GameBuildOffsets? offsets = GameBuildOffsetsTable.TryGet(PeHeaderReader.ReadFromFile(exePath).TimeDateStamp, out var found) ? found : null;
        return new LiveDungeonSessionReader(process, candidates, offsets, moduleBase, positionAddress);
    }

    private static PlayerRecordHit? FindLivePlayerRecord(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> regions, int worldSno, IReadOnlySet<int> sceneSnos)
    {
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in regions)
            foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
                foreach (var hit in PlayerRecordMatcher.FindHits(data.Span, address, PlayerActorSnos.Known, worldSno, sceneSnos))
                    if (hit.TrailingWord == PlayerRecordMatcher.LiveRecordFlag)
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

    public IReadOnlyList<ActorState>? ReadActors()
    {
        if (_offsets is { } offsets && RActorReader.TryRead(_process, _moduleBase, offsets, out var records))
            return ActorFilter.Placed(records);

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
