using D4Memory.Safety;
using D4MemScan.Scan;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>`pose-context`: saves the bytes around each address of a pose file, in the `--focus` capture format (replayable).</summary>
internal static class PoseContextCommand
{
    // Enough bytes on each side to see the whole record holding the position; ~1 KB per address keeps files small.
    private const int BytesBefore = 512;
    private const int BytesAfter = 512;

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var samples = JsonOutput.Read<List<PoseSample>>(reader.Require("--in"));
        string outPath = reader.Get("--out") ?? Path.Combine(Path.GetTempPath(), "d4memscan-pose.focus.json");

        var occurrences = new List<FocusOccurrence>();
        var buffer = new byte[BytesBefore + BytesAfter];
        using (var process = ProcessReader.OpenDiabloIv())
            foreach (var s in samples)
                if (process.TryRead(s.Address - BytesBefore, buffer, buffer.Length))
                    occurrences.Add(new FocusOccurrence($"0x{s.Address:X}", 0, "pose", -BytesBefore, Convert.ToHexString(buffer)));

        JsonOutput.Write(occurrences, outPath);
        Console.WriteLine($"Context saved for {occurrences.Count} of {samples.Count} addresses -> {outPath}");
        return 0;
    }
}
