using System.Collections.Concurrent;
using System.Diagnostics;
using D4Data.Readers;
using D4Memory.Pools;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// `waypoints`: every waypoint of the game (offline, WaypointCatalog) with its name in --locale (default deDE). With --scan,
/// finds in the live game's memory each run of at least --min (default 2) consecutive u32 words that are all waypoint
/// LevelArea SNOs: candidates for PlayerSavedData.arWaypointsActivated. --watch S then prints each candidate that changes
/// for S seconds: activate a new waypoint while it runs.
/// </summary>
internal static class WaypointsCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var (store, schema, install) = GameData.Open(reader);
        string locale = reader.Get("--locale") ?? "deDE";
        using (store)
        {
            var catalog = new WaypointCatalog(store, schema);
            var names = catalog.All.GroupBy(w => w.LevelAreaSno).ToDictionary(g => g.Key, g => catalog.Name(g.First(), locale));
            foreach (var w in catalog.All)
                Console.WriteLine($"{w.LevelAreaSno,8} {names[w.LevelAreaSno],-28} {w.LevelAreaName,-40} {store.Name(w.WorldSno)} ({w.Position.X:F0},{w.Position.Y:F0})");
            Console.WriteLine($"{catalog.All.Count} waypoint(s).");
            if (reader.Has("--live")) Live(names, Path.Combine(install, GameData.MainModuleFileName));
            if (!reader.Has("--scan")) return 0;
            int min = int.Parse(reader.Get("--min") ?? "2");
            var runs = Scan(names.Keys.ToHashSet(), min);
            if (reader.Has("--saved")) runs = SavedData(runs);
            foreach (var (address, values) in runs) Console.WriteLine($"0x{address:X}: {Describe(values, names)}");
            if (reader.Get("--watch") is { } watch) Watch(runs, names, int.Parse(watch));
        }
        return 0;
    }

    // --live: what the app reads, the local player's activated waypoints (ActivatedWaypointsReader) and the map view.
    private static void Live(Dictionary<int, string> names, string exePath)
    {
        using var process = ProcessReader.OpenDiabloIv();
        var (offsets, module) = Offsets.DiscoverAny(process, exePath);
        if (offsets is null) { Console.WriteLine("Game object not found."); return; }
        var activated = D4Memory.Actors.ActivatedWaypointsReader.Read(process, module.Base, offsets.GameObjectPointerRva, names.Keys.ToHashSet());
        Console.WriteLine(activated is null ? "Activated waypoints: unreadable." : $"Activated waypoints ({activated.Count}): {Describe(activated.Select(a => (uint)a), names)}");
        Console.WriteLine($"Map view: {D4Memory.Ui.MapViewReader.Read(process, module.Base, offsets.GameObjectPointerRva)?.ToString() ?? "unreadable"}");
    }

    // Words read past a run's end, so a newly activated waypoint appended to it shows up in --watch.
    private const int Slack = 8;

    private static List<(long Address, uint[] Values)> Scan(HashSet<int> levelAreas, int min)
    {
        using var process = ProcessReader.OpenDiabloIv();
        var clock = Stopwatch.StartNew();
        var found = new ConcurrentBag<(long, uint[])>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
        Parallel.ForEach(process.Regions(), options, () => new byte[RegionWindows.DefaultWindowBytes], (region, _, buffer) =>
        {
            foreach (var (address, window) in RegionWindows.ReadRegion(process, region, buffer))
            {
                var span = window.Span;
                int run = 0;
                for (int i = 0; i + 4 <= span.Length; i += 4)
                {
                    if (levelAreas.Contains(BitConverter.ToInt32(span[i..]))) { run++; continue; }
                    if (run >= min)
                    {
                        int start = i - run * 4;
                        var values = new uint[run];
                        for (int k = 0; k < run; k++) values[k] = BitConverter.ToUInt32(span[(start + k * 4)..]);
                        found.Add((address + start, values));
                    }
                    run = 0;
                }
            }
            return buffer;
        }, _ => { });
        var runs = found.DistinctBy(r => r.Item1).OrderByDescending(r => r.Item2.Length).ThenBy(r => r.Item1).ToList();
        Console.WriteLine($"{runs.Count} run(s) of >= {min} waypoint LevelAreas ({clock.ElapsedMilliseconds} ms).");
        return runs;
    }

    // PlayerSavedData: u32 uSecondsPlayed at +0x70, arWaypointsActivated (64 x u32 LevelArea, unused slots 0xFFFFFFFF) at +0x78.
    private const int SecondsPlayedBefore = 8, Slots = 64;

    // --saved: the runs that fill a 64-slot array whose unused slots are all 0xFFFFFFFF, each printed with its seconds played.
    private static List<(long Address, uint[] Values)> SavedData(List<(long Address, uint[] Values)> runs)
    {
        using var process = ProcessReader.OpenDiabloIv();
        var kept = new List<(long, uint[])>();
        foreach (var (address, values) in runs)
        {
            if (values.Length > Slots) continue;
            var slots = Read(process, address, Slots);
            if (!slots.Skip(values.Length).All(v => v == uint.MaxValue)) continue;
            uint seconds = PoolContainer.TryReadU32(process, address - SecondsPlayedBefore, out uint s) ? s : 0;
            Console.WriteLine($"saved data at 0x{address - 0x78:X}: {values.Length} waypoint(s), {seconds} s played ({seconds / 3600.0:F1} h)");
            kept.Add((address - SecondsPlayedBefore, [.. Read(process, address - SecondsPlayedBefore, 1), .. values]));
        }
        return kept;
    }

    private static void Watch(List<(long Address, uint[] Values)> runs, Dictionary<int, string> names, int seconds)
    {
        using var process = ProcessReader.OpenDiabloIv();
        var last = runs.ToDictionary(r => r.Address, r => Read(process, r.Address, r.Values.Length + Slack));
        Console.WriteLine($"Watching {runs.Count} run(s) for {seconds} s. Activate a waypoint now.");
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            foreach (var (address, values) in runs)
            {
                var now = Read(process, address, values.Length + Slack);
                if (now.SequenceEqual(last[address])) continue;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} 0x{address:X}: {Describe(now, names)}");
                last[address] = now;
            }
            Thread.Sleep(500);
        }
    }

    private static uint[] Read(ProcessReader process, long address, int count) =>
        [.. Enumerable.Range(0, count).Select(i => PoolContainer.TryReadU32(process, address + i * 4, out uint v) ? v : uint.MaxValue)];

    private static string Describe(IEnumerable<uint> values, Dictionary<int, string> names) =>
        string.Join(", ", values.Select(v => names.TryGetValue((int)v, out var n) ? n : v == uint.MaxValue ? "?" : $"0x{v:X}"));
}
