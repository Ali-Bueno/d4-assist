using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using D4Memory.Safety;
using D4Memory.Scan;
using D4Memory.Ui;

namespace D4MemScan.Commands;

/// <summary>
/// `screens`: finds every `screen_*` name in the game's memory, locates each one's record in the heap screen registry
/// (research/client-memory.md §5) and prints every open/close with its time, e.g. to see which screens a cutscene opens.
/// </summary>
internal static class ScreensCommand
{
    private const int DefaultSeconds = 120;
    private const int DefaultIntervalMs = 200;

    private static readonly byte[] Prefix = Encoding.ASCII.GetBytes("screen_");
    private const int MaxNameLength = 96;
    private const uint MemPrivate = 0x20000; // MEM_PRIVATE

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        string install = reader.Get("--install") ?? GameData.DefaultInstallPath;
        int seconds = int.Parse(reader.Get("--watch") ?? DefaultSeconds.ToString());
        int intervalMs = int.Parse(reader.Get("--interval-ms") ?? DefaultIntervalMs.ToString());

        using var process = ProcessReader.OpenDiabloIv();
        var clock = Stopwatch.StartNew();
        var names = HarvestNames(process);
        Console.WriteLine($"{names.Count} screen_* names in memory ({clock.ElapsedMilliseconds} ms).");

        string exePath = Path.Combine(install, GameData.MainModuleFileName);
        var records = new Dictionary<string, long>();
        ModuleRange module = default;
        foreach (var candidate in ModuleLocator.LocateAll(process.ImageRegions(), process, exePath))
        {
            var range = new ModuleRange(candidate.Base, candidate.SizeOfImage);
            var located = ScreenRegistryReader.Locate(process, names, range);
            if (located.Count <= records.Count) continue;
            (records, module) = (located, range);
        }
        Console.WriteLine($"{records.Count} screen records located ({clock.ElapsedMilliseconds} ms).");
        if (records.Count == 0) return 1;

        var last = new Dictionary<string, bool?>();
        foreach (var (name, record) in records) last[name] = ScreenRegistryReader.TryReadOpen(process, record, module);
        Console.WriteLine("Open now: " + string.Join(", ", last.Where(p => p.Value == true).Select(p => p.Key).Order()));

        Console.WriteLine($"Watching for {seconds}s, every {intervalMs}ms.");
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < seconds)
        {
            foreach (var (name, record) in records)
            {
                bool? open = ScreenRegistryReader.TryReadOpen(process, record, module);
                if (open == last[name]) continue;
                string state = open switch { true => "open", false => "closed", null => "invalid" };
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{watch.Elapsed.TotalSeconds,6:F1}s] {state,-7} {name}");
                last[name] = open;
            }
            Thread.Sleep(intervalMs);
        }
        return 0;
    }

    /// <summary>Every distinct NUL-terminated `screen_[A-Za-z0-9_]+` string in private memory.</summary>
    private static List<string> HarvestNames(ProcessReader process)
    {
        var names = new ConcurrentDictionary<string, byte>();
        var regions = process.Regions().Where(r => r.Type == MemPrivate);
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
        Parallel.ForEach(regions, options, () => new byte[RegionWindows.DefaultWindowBytes], (region, state, buffer) =>
        {
            foreach (var (_, data) in RegionWindows.ReadRegion(process, region, buffer))
            {
                var span = data.Span;
                int at = 0;
                while ((at = span.IndexOf(Prefix)) >= 0)
                {
                    int end = at + Prefix.Length;
                    while (end < span.Length && end - at < MaxNameLength && IsNameChar(span[end])) end++;
                    if (end < span.Length && span[end] == 0 && end > at + Prefix.Length && (at == 0 || !IsNameChar(span[at - 1])))
                        names.TryAdd(Encoding.ASCII.GetString(span[at..end]), 0);
                    span = span[end..];
                }
            }
            return buffer;
        }, _ => { });
        return names.Keys.Order().DistinctBy(ScreenRegistryReader.Hash).ToList(); // case variants share a hash
    }

    private static bool IsNameChar(byte b) => b is >= (byte)'a' and <= (byte)'z' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)'_';
}
