using D4Memory.Safety;
using D4MemScan.Scan;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>`pose-filter`: re-reads a snapshot's addresses and keeps the ones that changed or stayed unchanged.</summary>
internal static class PoseFilterCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        string inPath = reader.Require("--in");
        string outPath = reader.Get("--out") ?? Path.Combine(Path.GetTempPath(), "d4memscan-pose-filtered.json");
        var mode = reader.Require("--mode") switch
        {
            "changed" => PoseFilterMode.Changed,
            "unchanged" => PoseFilterMode.Unchanged,
            var other => throw new ArgumentException($"--mode must be 'changed' or 'unchanged', got '{other}'."),
        };

        var previous = JsonOutput.Read<List<PoseSample>>(inPath);
        using var process = ProcessReader.OpenDiabloIv();
        var survivors = PoseFilterer.Filter(previous, process, mode);

        JsonOutput.Write(survivors, outPath);
        Console.WriteLine($"Survivors ({mode}): {survivors.Count} of {previous.Count}.");
        foreach (var s in survivors.Take(10))
            Console.WriteLine($"  0x{s.Address:X} -> ({s.X:F2}, {s.Y:F2}, {s.Z:F2})");
        Console.WriteLine($"Filtered snapshot written to {outPath}");
        return 0;
    }
}
