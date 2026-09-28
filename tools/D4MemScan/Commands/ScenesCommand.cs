using D4Data.Model;
using D4Data.Readers;
using D4Memory.Safety;
using D4MemScan.Scan;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>`scenes`: scans live memory for CScene tile records and revealed-scene list entries, and self-validates the result.</summary>
internal static class ScenesCommand
{
    // Console summary budget (task spec: <= 25 lines including the ASCII map).
    private const int MaxSummaryLines = 25;

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        string outPath = reader.Get("--out") ?? Path.Combine(Path.GetTempPath(), "d4memscan-scenes.json");

        var (store, schema, install) = GameData.Open(reader);
        using (store)
        {
            var sceneReader = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);
            // All Scene SNOs by default: transition/filler tiles (zero exit mask) matter for the layout too.
            var candidates = CandidateSceneBuilder.Build(store, sceneReader, sceneReader.Group, tilesOnly: reader.Has("--tiles-only"));
            var world = TryResolveWorld(store, schema, install);
            Console.WriteLine($"Candidates: {candidates.Count} scenes (min SNO {candidates.Keys.DefaultIfEmpty().Min()})");

            var focus = BuildFocus(reader.Get("--focus"), candidates);

            // --replay re-runs the matchers over a saved --focus capture instead of the live process.
            string? replay = reader.Get("--replay");
            using IDisposable? process = replay is null ? ProcessReader.OpenDiabloIv() : null;
            IMemoryReader memory = replay is null ? (ProcessReader)process! : CaptureMemoryReader.Load(replay);
            var scan = new SceneScanner().Scan(memory, new CandidateIndex(candidates), ProgressPrinter.Create(), focus);
            DumpModuleIfRequested(reader.Get("--dump-module"), memory, install);
            var report = ScanReportBuilder.Build(scan, candidates, world);

            JsonOutput.Write(report, outPath);
            PrintSummary(report, outPath);
            if (focus is not null)
            {
                string focusPath = Path.ChangeExtension(outPath, ".focus.json");
                JsonOutput.Write(focus.Occurrences, focusPath);
                Console.WriteLine($"Focus occurrences: {focus.Occurrences.Count} -> {focusPath}");
            }
            return 0;
        }
    }

    // Context window and cap for --focus: enough bytes to see a whole scene record, few enough to keep the file small.
    private const int FocusBytesBefore = 256;
    private const int FocusBytesAfter = 512;
    private const int FocusMaxOccurrences = 5000;

    /// <summary>Focus set = candidate scenes whose name matches the --focus regex (e.g. a tile family like "^Step_City").</summary>
    private static FocusCollector? BuildFocus(string? pattern, IReadOnlyDictionary<int, CandidateScene> candidates)
    {
        if (pattern is null) return null;
        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var selected = candidates.Where(kv => kv.Value.Name is { } n && regex.IsMatch(n)).ToDictionary();
        Console.WriteLine($"Focus: {selected.Count} scenes match '{pattern}'");
        return new FocusCollector(new CandidateIndex(selected), FocusBytesBefore, FocusBytesAfter, FocusMaxOccurrences);
    }

    private static SnoRef? TryResolveWorld(D4Data.Casc.SnoStore store, D4Data.Schema.TypeSchema schema, string install) =>
        WorldResolver.TryResolve(store, schema, install) is { } w ? new SnoRef(w.Sno, w.Name) : null;

    /// <summary>Right after the heap scan, so a CScene report and a module dump come from the same moment
    /// (for later pointer-chain analysis) — no-op when replaying a capture (no live process to dump from).</summary>
    private static void DumpModuleIfRequested(string? outPath, IMemoryReader memory, string install)
    {
        if (outPath is null) return;
        if (memory is not ProcessReader process)
        {
            Console.WriteLine("--dump-module ignored: no live process (replaying a capture).");
            return;
        }
        string exePath = Path.Combine(install, GameData.MainModuleFileName);
        var (moduleBase, regionCount) = ModuleDumpWriter.Write(process, exePath, outPath);
        Console.WriteLine($"Module dump: base 0x{moduleBase:X}, {regionCount} writable regions -> {outPath}");
    }

    private static void PrintSummary(SceneScanReport report, string outPath)
    {
        Console.WriteLine($"World: {report.World?.Name ?? "(unknown)"}  Tiles: {report.Tiles.Count}  Revealed: {report.Revealed.Count}");
        Console.WriteLine("Vtables (value : count):");
        foreach (var v in report.Vtables.Take(3))
            Console.WriteLine($"  {v.Vtable} : {v.Count}");
        Console.WriteLine($"Exit consistency: {report.ExitConsistency.ConsistentPairs}/{report.ExitConsistency.TotalPairs} " +
                           $"adjacent pairs ({report.ExitConsistency.Percent:F1}%), {report.ExitConsistency.SkippedMultiCellTiles} multi-cell tiles skipped");
        Console.WriteLine($"Bounds: X[{report.Bounds.Min[0]:F1},{report.Bounds.Max[0]:F1}] Y[{report.Bounds.Min[1]:F1},{report.Bounds.Max[1]:F1}] " +
                           $"Z[{report.Bounds.Min[2]:F1},{report.Bounds.Max[2]:F1}]");

        // Lines above (world/vtables/exit/bounds) + the map header, the trailing "report written" line, and
        // one spare line for the "N more rows" note: cap the map itself to whatever remains of the budget.
        const int fixedLines = 10;
        int mapBudget = Math.Max(0, MaxSummaryLines - fixedLines);
        Console.WriteLine("Map (rows=south, cols=east):");
        foreach (var row in report.AsciiMap.Take(mapBudget))
            Console.WriteLine($"  {row}");
        if (report.AsciiMap.Count > mapBudget)
            Console.WriteLine($"  ... {report.AsciiMap.Count - mapBudget} more rows in {outPath}");
        Console.WriteLine($"Report written to {outPath}");
    }
}
