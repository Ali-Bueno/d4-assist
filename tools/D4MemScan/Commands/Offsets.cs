using D4Memory.Safety;
using D4Memory.SceneRoute;
using D4Memory.Ui;

namespace D4MemScan.Commands;

/// <summary>The build's addresses, found in the live process (BuildOffsetsDiscovery); prints where they were found.</summary>
internal static class Offsets
{
    public static GameBuildOffsets? Discover(ProcessReader process, MainModule module)
    {
        var offsets = BuildOffsetsDiscovery.Discover(
            process, process.ImageRegions(), new ModuleRange(module.Base, module.SizeOfImage), out string report);
        Console.WriteLine($"Module 0x{module.Base:X}: {report}.");
        return offsets;
    }

    /// <summary>The first module view whose addresses are found (the game keeps more than one view of its image).</summary>
    public static (GameBuildOffsets? Offsets, MainModule Module) DiscoverAny(ProcessReader process, string exePath)
    {
        var modules = ModuleLocator.LocateAll(process.ImageRegions(), process, exePath);
        foreach (var module in modules)
            if (Discover(process, module) is { } offsets) return (offsets, module);
        return (null, modules.FirstOrDefault());
    }
}
