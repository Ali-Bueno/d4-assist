using System.Collections.Concurrent;
using System.Diagnostics;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// `sno-scan --snos <file>`: counts, in the whole live game memory, the 4-aligned u32 words equal to each SNO listed in the file
/// (one "id name" per line, as from the CoreTOC), and prints each SNO found with its hit count and first addresses. Tells which
/// of a family of SNOs (helltide bounties per territory, world-boss quests) the client holds at all right now.
/// </summary>
internal static class SnoScanCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var names = File.ReadAllLines(reader.Require("--snos"))
            .Select(l => l.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries))
            .Where(p => p.Length > 0 && int.TryParse(p[0], out _))
            .ToDictionary(p => int.Parse(p[0]), p => p.Length > 1 ? p[1] : "");
        int show = int.Parse(reader.Get("--show") ?? "4");

        using var process = ProcessReader.OpenDiabloIv();
        var clock = Stopwatch.StartNew();
        var hits = new ConcurrentDictionary<int, ConcurrentBag<long>>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
        Parallel.ForEach(process.Regions(), options, () => new byte[RegionWindows.DefaultWindowBytes], (region, _, buffer) =>
        {
            foreach (var (address, window) in RegionWindows.ReadRegion(process, region, buffer))
            {
                var span = window.Span;
                for (int i = (int)((4 - address % 4) % 4); i + 4 <= span.Length; i += 4)
                {
                    int value = BitConverter.ToInt32(span[i..]);
                    if (names.ContainsKey(value)) hits.GetOrAdd(value, _ => []).Add(address + i);
                }
            }
            return buffer;
        }, _ => { });

        foreach (var (sno, at) in hits.OrderBy(h => names[h.Key]))
            Console.WriteLine($"{sno,8} {names[sno],-64} {at.Count,6} hit(s): {string.Join(" ", at.Order().Take(show).Select(a => $"0x{a:X}"))}");
        Console.WriteLine($"{hits.Count} of {names.Count} SNO(s) found in {clock.ElapsedMilliseconds} ms.");
        return 0;
    }
}
