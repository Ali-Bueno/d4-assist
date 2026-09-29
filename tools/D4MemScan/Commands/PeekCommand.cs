using System.Diagnostics;
using D4Memory.Safety;

namespace D4MemScan.Commands;

/// <summary>
/// `peek`: watches a few u32 words at absolute addresses and prints each change with its time, e.g. to confirm
/// a UI-state candidate from a module-dump diff while the player opens and closes a panel.
/// </summary>
internal static class PeekCommand
{
    private const int DefaultSeconds = 60;
    private const int DefaultIntervalMs = 100;

    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var addresses = reader.Require("--address").Split(',')
            .Select(a => Convert.ToInt64(a.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16)).ToArray();
        int seconds = int.Parse(reader.Get("--seconds") ?? DefaultSeconds.ToString());
        int intervalMs = int.Parse(reader.Get("--interval-ms") ?? DefaultIntervalMs.ToString());

        using var process = ProcessReader.OpenDiabloIv();
        var last = new uint?[addresses.Length];
        var word = new byte[sizeof(uint)];
        var clock = Stopwatch.StartNew();
        Console.WriteLine($"Peeking {addresses.Length} word(s) for {seconds}s, every {intervalMs}ms.");
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            for (int i = 0; i < addresses.Length; i++)
            {
                uint? value = process.TryRead(addresses[i], word, word.Length) ? BitConverter.ToUInt32(word) : null;
                if (value == last[i]) continue;
                Console.WriteLine($"[{clock.ElapsedMilliseconds,7}ms] 0x{addresses[i]:X}: {Format(last[i])} -> {Format(value)}");
                last[i] = value;
            }
            Thread.Sleep(intervalMs);
        }
        return 0;
    }

    private static string Format(uint? value) => value is { } v ? $"0x{v:X}" : "unreadable";
}
