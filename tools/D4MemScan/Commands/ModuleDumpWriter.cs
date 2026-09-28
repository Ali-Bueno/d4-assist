using D4Memory.Safety;
using D4MemScan.Scan;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// Writes every main-module writable page in the `--focus` capture format, for offline replay
/// (see ModuleLocator, CaptureMemoryReader). Shared by `scenes --dump-module` and `player --dump-module`.
/// </summary>
internal static class ModuleDumpWriter
{
    /// <summary>Sentinel entry name recording the module base (the per-region entries don't carry it).</summary>
    public const string ModuleBaseEntryName = "module-base";

    /// <summary>Entry name for a captured writable region, per the task's capture-format spec.</summary>
    public const string ModuleRegionEntryName = "module";

    public static (long ModuleBase, int RegionCount) Write(ProcessReader process, string exePath, string outPath)
    {
        var regions = process.ImageRegions();
        var modules = ModuleLocator.LocateAll(regions, process, exePath);
        if (modules.Count == 0)
            throw new InvalidOperationException($"Main module not found among the process's image regions ({exePath}).");

        // Every image view is dumped (the game keeps several); each view's regions follow its base entry.
        var occurrences = new List<FocusOccurrence>();
        int regionCount = 0;
        foreach (var module in modules)
        {
            var writable = ModuleLocator.WritableRegionsAt(regions, module.Base);
            occurrences.Add(BaseEntry(module.Base));
            occurrences.AddRange(writable.Select(r => RegionEntry(process, r)));
            regionCount += writable.Count;
        }

        JsonOutput.Write(occurrences, outPath);
        return (modules[0].Base, regionCount);
    }

    private static FocusOccurrence BaseEntry(long moduleBase) =>
        new(Address: $"0x{moduleBase:X}", Sno: 0, Name: ModuleBaseEntryName, ContextStart: 0, ContextHex: "");

    private static FocusOccurrence RegionEntry(ProcessReader process, MemoryRegion region)
    {
        var bytes = new byte[(int)region.Size];
        if (!process.TryRead(region.Address, bytes, bytes.Length))
            throw new InvalidOperationException($"Failed reading module region at 0x{region.Address:X} (size {region.Size}).");
        return new(Address: $"0x{region.Address:X}", Sno: 0, Name: ModuleRegionEntryName, ContextStart: 0, ContextHex: Convert.ToHexString(bytes));
    }
}
