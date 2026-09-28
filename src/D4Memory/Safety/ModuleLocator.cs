using D4Memory.Scan;

namespace D4Memory.Safety;

/// <summary>The main module's base address and mapped size, once matched against the on-disk exe.</summary>
public readonly record struct MainModule(long Base, uint SizeOfImage);

/// <summary>
/// Finds the main module's base address without EnumProcessModules or Toolhelp: enumerate regions with
/// VirtualQueryEx (via ProcessReader.ImageRegions), take the distinct AllocationBase of every MEM_IMAGE
/// region, and match the in-memory PE header at each base against the on-disk exe's header (TimeDateStamp +
/// SizeOfImage — see PeHeaderReader). Only reads a base whose header page was already reported as a safe,
/// non-executable region; a base without one (e.g. header page is PAGE_EXECUTE_*) is skipped, never forced.
/// </summary>
public static class ModuleLocator
{
    /// <summary>Every mapping whose PE header matches the on-disk exe (the game keeps more than one view of its image).</summary>
    public static List<MainModule> LocateAll(IReadOnlyList<MemoryRegion> imageRegions, IMemoryReader reader, string exePath)
    {
        var onDisk = PeHeaderReader.ReadFromFile(exePath);
        var buffer = new byte[PeHeaderReader.HeaderReadSize];
        var found = new List<MainModule>();

        foreach (long candidateBase in DistinctImageBases(imageRegions))
        {
            if (!HasSafeHeaderPage(imageRegions, candidateBase)) continue;
            if (!reader.TryRead(candidateBase, buffer, buffer.Length)) continue;
            if (!PeHeaderReader.TryParse(buffer, out var header)) continue;
            if (header.TimeDateStamp == onDisk.TimeDateStamp && header.SizeOfImage == onDisk.SizeOfImage)
                found.Add(new MainModule(candidateBase, header.SizeOfImage));
        }
        return found;
    }

    public static MainModule? Locate(IReadOnlyList<MemoryRegion> imageRegions, IMemoryReader reader, string exePath) =>
        LocateAll(imageRegions, reader, exePath) is [var first, ..] ? first : null;

    /// <summary>The module's writable pages at <paramref name="moduleBase"/> — PAGE_READWRITE or
    /// PAGE_WRITECOPY (task spec); PAGE_READONLY pages (e.g. the header, .rdata) are excluded.</summary>
    public static IReadOnlyList<MemoryRegion> WritableRegionsAt(IReadOnlyList<MemoryRegion> imageRegions, long moduleBase) =>
        [.. imageRegions.Where(r => IsModuleMapping(r.Type) && r.AllocationBase == moduleBase
            && (r.Protection == NativeMethods.PageReadWrite || r.Protection == NativeMethods.PageWriteCopy))];

    private static IEnumerable<long> DistinctImageBases(IReadOnlyList<MemoryRegion> imageRegions) =>
        imageRegions.Where(r => IsModuleMapping(r.Type)).Select(r => r.AllocationBase).Distinct();

    private static bool HasSafeHeaderPage(IReadOnlyList<MemoryRegion> imageRegions, long moduleBase) =>
        imageRegions.Any(r => IsModuleMapping(r.Type) && r.Address == moduleBase);

    // Diablo IV's protection remaps its main image as a MEM_MAPPED section (observed 2026-09-28), not MEM_IMAGE.
    private static bool IsModuleMapping(uint type) => type == NativeMethods.MemImage || type == NativeMethods.MemMapped;
}
