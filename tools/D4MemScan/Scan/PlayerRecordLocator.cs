using D4Data.Casc;
using D4Data.Schema;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4MemScan.Scan;

/// <summary>Finds the player's static position record in the game's module views (shared by `player` and `actors`).</summary>
internal static class PlayerRecordLocator
{
    private const string SceneRootType = "SceneDefinition";

    public static bool IsLive(PlayerRecordHit hit) => hit.TrailingWord == PlayerRecordMatcher.LiveRecordFlag;

    public static HashSet<int> SceneSnos(SnoStore store, TypeSchema schema) =>
        store.AllEntries(schema.SnoGroup(SceneRootType)).Select(e => e.Id).ToHashSet();

    /// <summary>Every module view of the exe in the process; throws when none matches.</summary>
    public static (IReadOnlyList<MemoryRegion> Regions, List<MainModule> Modules) LocateModules(ProcessReader process, string exePath)
    {
        var regions = process.ImageRegions();
        var modules = ModuleLocator.LocateAll(regions, process, exePath);
        if (modules.Count == 0)
            throw new InvalidOperationException($"Main module not found among the process's image regions ({exePath}).");
        return (regions, modules);
    }

    /// <summary>The live record lives in exactly one view: scans them all and returns the view holding a live hit.</summary>
    public static (MainModule? LiveModule, List<PlayerRecordHit> Hits) ScanModules(
        ProcessReader process, IReadOnlyList<MemoryRegion> regions, IEnumerable<MainModule> modules,
        int worldSno, IReadOnlySet<int> sceneSnos)
    {
        MainModule? liveModule = null;
        var hits = new List<PlayerRecordHit>();
        foreach (var module in modules)
        {
            var found = Scan(process, ModuleLocator.WritableRegionsAt(regions, module.Base), worldSno, sceneSnos);
            Console.WriteLine($"Module view 0x{module.Base:X}: {found.Count} record(s), {found.Count(IsLive)} live");
            if (liveModule is null && found.Any(IsLive)) liveModule = module;
            hits.AddRange(found);
        }
        return (liveModule, [.. hits.OrderByDescending(IsLive)]);
    }

    public static List<PlayerRecordHit> Scan(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> regions, int worldSno, IReadOnlySet<int> sceneSnos)
    {
        var hits = new List<PlayerRecordHit>();
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in regions)
            foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
                hits.AddRange(PlayerRecordMatcher.FindHits(data.Span, address, PlayerActorSnos.Known, worldSno, sceneSnos));
        return hits;
    }
}
