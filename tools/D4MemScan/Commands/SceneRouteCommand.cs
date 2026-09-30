using D4Data.Readers;
using D4Memory.Safety;
using D4Memory.Scan;
using D4Memory.SceneRoute;
using D4MemScan.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// `scene-route`: prints tiles found via the static scene route (research/client-memory.md §1) instead of
/// scanning all of readable memory. `--compare` also runs the full `scenes` heap scan and reports how well
/// the two agree — this is the live validation step for the route (STATUS.md "static route ... live
/// validation pending").
/// </summary>
internal static class SceneRouteCommand
{
    private const int MaxTileLines = 15;

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        bool compare = reader.Has("--compare");

        var (store, schema, install) = GameData.Open(reader);
        using (store)
        {
            var sceneReader = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);
            var candidateDict = CandidateSceneBuilder.Build(store, sceneReader, sceneReader.Group, tilesOnly: false);
            var candidates = new CandidateIndex(candidateDict);

            string exePath = Path.Combine(install, GameData.MainModuleFileName);
            var signature = PeHeaderReader.ReadFromFile(exePath);
            using var process = ProcessReader.OpenDiabloIv();
            if (Offsets.DiscoverAny(process, exePath).Offsets is not { } offsets)
            {
                Console.WriteLine($"Route unavailable: addresses not found for build TimeDateStamp 0x{signature.TimeDateStamp:X}. Fall back to `scenes`.");
                return 1;
            }
            if (reader.Has("--diagnose"))
            {
                var bases = ModuleLocator.LocateAll(process.ImageRegions(), process, exePath).Select(m => m.Base).ToList();
                SceneRouteDiagnostics.Run(process, bases, offsets, candidates, slotsOnly: reader.Has("--slots"));
                return 0;
            }
            var (tiles, moduleBase) = ReadViaRoute(process, exePath, offsets, candidates);

            Console.WriteLine(tiles.Count > 0
                ? $"Route: {tiles.Count} tile(s) via module base 0x{moduleBase:X}."
                : "Route: unavailable, or empty, for every module view.");
            foreach (var t in tiles.Take(MaxTileLines))
                Console.WriteLine($"  {t.Sno} ({t.Name}) wp=({t.Wp.X:F1},{t.Wp.Y:F1},{t.Wp.Z:F1}) exit={t.ExitMask}");
            if (tiles.Count > MaxTileLines)
                Console.WriteLine($"  ... {tiles.Count - MaxTileLines} more");

            if (compare) CompareAgainstFullScan(process, candidates, tiles);
            return 0;
        }
    }

    /// <summary>The live record lives in exactly one of the game's module views (see PlayerCommand); try each.</summary>
    private static (IReadOnlyList<CSceneHit> Tiles, long ModuleBase) ReadViaRoute(
        ProcessReader process, string exePath, GameBuildOffsets offsets, CandidateIndex candidates)
    {
        var regions = process.ImageRegions();
        var modules = ModuleLocator.LocateAll(regions, process, exePath);
        if (modules.Count == 0)
            throw new InvalidOperationException($"Main module not found among the process's image regions ({exePath}).");

        foreach (var module in modules)
            if (SceneRouteReader.TryRead(process, module.Base, offsets, candidates, out var tiles) && tiles.Count > 0)
                return (tiles, module.Base);
        return ([], modules[0].Base);
    }

    private static void CompareAgainstFullScan(IMemoryReader process, CandidateIndex candidates, IReadOnlyList<CSceneHit> routeTiles)
    {
        var scan = new SceneScanner().Scan(process, candidates, ProgressPrinter.Create());
        var scanKeys = scan.Tiles.Select(TileKey).ToHashSet();
        var routeKeys = routeTiles.Select(TileKey).ToHashSet();
        int agreeing = routeKeys.Count(scanKeys.Contains);

        Console.WriteLine($"Scan: {scan.Tiles.Count} tile(s). Agreement: {agreeing}/{routeKeys.Count} route tile(s) confirmed by the scan, " +
                           $"{scanKeys.Count(k => !routeKeys.Contains(k))} scan tile(s) missing from the route.");
    }

    private static (int Sno, float X, float Y, float Z) TileKey(CSceneHit hit) => (hit.Sno, hit.Wp.X, hit.Wp.Y, hit.Wp.Z);
}
