using System.Diagnostics;
using System.Numerics;
using D4Data.Casc;
using D4Memory.Safety;
using D4MemScan.Scan;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>`player`: locates the local player's static position record in the main module and samples it.</summary>
internal static class PlayerCommand
{
    private const int DefaultSamples = 10;
    private const int DefaultIntervalMs = 250;
    private const string SceneRootType = "SceneDefinition";

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        int samples = ParseIntOrDefault(reader.Get("--samples"), DefaultSamples);
        int intervalMs = ParseIntOrDefault(reader.Get("--interval-ms"), DefaultIntervalMs);
        string? dumpModulePath = reader.Get("--dump-module");
        string? replayModulePath = reader.Get("--replay-module");

        var (store, schema, install) = GameData.Open(reader);
        using (store)
        {
            var (worldSno, worldName) = WorldResolver.Resolve(store, schema, install);
            var sceneSnos = store.AllEntries(schema.SnoGroup(SceneRootType)).Select(e => e.Id).ToHashSet();
            Console.WriteLine($"World: {worldName} -> SNO {worldSno}; {sceneSnos.Count} scene SNOs known.");

            if (replayModulePath is not null)
                return RunAgainstCapture(replayModulePath, store, worldSno, sceneSnos, samples, intervalMs);

            string exePath = Path.Combine(install, GameData.MainModuleFileName);
            using var process = ProcessReader.OpenDiabloIv();
            var regions = process.ImageRegions();
            var modules = ModuleLocator.LocateAll(regions, process, exePath);
            if (modules.Count == 0)
                throw new InvalidOperationException($"Main module not found among the process's image regions ({exePath}).");

            if (dumpModulePath is not null)
            {
                var (dumpedBase, regionCount) = ModuleDumpWriter.Write(process, exePath, dumpModulePath);
                Console.WriteLine($"Module dump: base 0x{dumpedBase:X}, {regionCount} writable regions -> {dumpModulePath}");
            }

            // The live record is in exactly one image view; scan them all and keep the one flagged live.
            MainModule? liveModule = null;
            var hits = new List<PlayerRecordHit>();
            foreach (var module in modules)
            {
                var found = ScanForPlayerRecord(process, ModuleLocator.WritableRegionsAt(regions, module.Base), worldSno, sceneSnos);
                Console.WriteLine($"Module view 0x{module.Base:X}: {found.Count} record(s), {found.Count(IsLive)} live");
                if (liveModule is null && found.Any(IsLive)) liveModule = module;
                hits.AddRange(found);
            }
            long reportBase = (liveModule ?? modules[0]).Base;
            return ReportAndSample(process, store, reportBase, [.. hits.OrderByDescending(IsLive)], samples, intervalMs);
        }
    }

    private static int RunAgainstCapture(
        string path, SnoStore store, int worldSno, HashSet<int> sceneSnos, int samples, int intervalMs)
    {
        var occurrences = CaptureMemoryReader.LoadOccurrences(path);
        var baseEntry = occurrences.FirstOrDefault(o => o.Name == ModuleDumpWriter.ModuleBaseEntryName);
        long moduleBase = baseEntry?.AddressValue ?? 0;
        Console.WriteLine(baseEntry is not null
            ? $"Module base (from capture): 0x{moduleBase:X}"
            : "Module base: unknown (capture has no module-base entry).");

        var capture = new CaptureMemoryReader(occurrences.Where(o => o.Name == ModuleDumpWriter.ModuleRegionEntryName));
        var hits = ScanForPlayerRecord(capture, capture.Regions(), worldSno, sceneSnos);
        return ReportAndSample(capture, store, moduleBase, hits, samples, intervalMs);
    }

    private static bool IsLive(PlayerRecordHit hit) => hit.TrailingWord == PlayerRecordMatcher.LiveRecordFlag;

    private static List<PlayerRecordHit> ScanForPlayerRecord(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> regions, int worldSno, IReadOnlySet<int> sceneSnos)
    {
        var hits = new List<PlayerRecordHit>();
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in regions)
            foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
                hits.AddRange(PlayerRecordMatcher.FindHits(data.Span, address, PlayerActorSnos.Known, worldSno, sceneSnos));
        return hits;
    }

    private static int ReportAndSample(
        IMemoryReader memory, SnoStore store, long moduleBase, List<PlayerRecordHit> hits, int samples, int intervalMs)
    {
        Console.WriteLine($"Player record matches: {hits.Count} ({hits.Count(IsLive)} live; sampling the first live one)");
        foreach (var hit in hits)
        {
            string rva = moduleBase == 0 ? "n/a" : $"0x{hit.PositionAddress - moduleBase:X}";
            Console.WriteLine($"  address 0x{hit.PositionAddress:X} RVA {rva} scene {hit.SceneSno} ({store.Name(hit.SceneSno)}) " +
                               $"actor {hit.ActorSno} pos ({hit.Position.X:F2}, {hit.Position.Y:F2}, {hit.Position.Z:F2})");
        }
        if (hits.Count == 0)
        {
            Console.Error.WriteLine("No player record found.");
            return 1;
        }

        SamplePosition(memory, store, hits[0].PositionAddress, samples, intervalMs);
        return 0;
    }

    private static void SamplePosition(IMemoryReader memory, SnoStore store, long positionAddress, int samples, int intervalMs)
    {
        Vector3? previous = null;
        var clock = Stopwatch.StartNew();
        var buffer = new byte[PlayerSampleWindow.WindowSize];
        long windowStart = positionAddress - PlayerSampleWindow.PositionOffsetInWindow;

        for (int i = 0; i < samples; i++)
        {
            if (i > 0) Thread.Sleep(intervalMs);
            if (!memory.TryRead(windowStart, buffer, buffer.Length))
            {
                Console.WriteLine($"  [{clock.ElapsedMilliseconds,6}ms] read failed");
                continue;
            }

            var sample = PlayerSampleWindow.Parse(buffer);
            Console.WriteLine($"  [{clock.ElapsedMilliseconds,6}ms] pos ({sample.Position.X:F2}, {sample.Position.Y:F2}, {sample.Position.Z:F2}) " +
                               $"scene {sample.SceneSno} ({store.Name(sample.SceneSno)}) {DeltaAndBearing(previous, sample.Position)}");
            previous = sample.Position;
        }
    }

    private static string DeltaAndBearing(Vector3? previous, Vector3 current)
    {
        if (previous is not { } prev) return "d=(-)";
        float dx = current.X - prev.X, dy = current.Y - prev.Y;
        string bearing = BearingFormatter.Format(dx, dy) ?? "-";
        return $"d=({dx:F2}, {dy:F2}) {bearing}";
    }

    private static int ParseIntOrDefault(string? text, int fallback) => int.TryParse(text, out var value) ? value : fallback;
}
