using System.Diagnostics;
using D4Memory.Actors;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>Re-reads one ACD and prints its attribute and slot-word changes (to find a gizmo's "used" record).</summary>
internal static class AcdWatch
{
    private const int MaxWordLinesPerBurst = 15;

    public static void Run(
        IMemoryReader memory, AcdReader acds, AttributeNameTable names, AcdRecord target, string label, int seconds, int intervalMs)
    {
        Console.WriteLine($"Watching ACD 0x{target.Id:X} at 0x{target.SlotAddress:X} ({label}) for {seconds}s, every {intervalMs}ms.");
        var previous = new byte[acds.Stride];
        var current = new byte[acds.Stride];
        if (!memory.TryRead(target.SlotAddress, previous, previous.Length))
        {
            Console.WriteLine("Initial slot read failed.");
            return;
        }
        var previousAttributes = Attributes(memory, names, target);

        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            Thread.Sleep(intervalMs);
            if (!acds.TryGet(target.Id, out var now))
            {
                Console.WriteLine($"[{clock.ElapsedMilliseconds,6}ms] ACD 0x{target.Id:X} left the pool (freed or reused); watch ended.");
                return;
            }
            var attributes = Attributes(memory, names, now);
            PrintAttributeChanges(previousAttributes, attributes, clock.ElapsedMilliseconds);
            previousAttributes = attributes;

            if (!memory.TryRead(now.SlotAddress, current, current.Length)) continue;
            SlotWordDiff.Print(previous, current, clock.ElapsedMilliseconds, MaxWordLinesPerBurst);
            Array.Copy(current, previous, current.Length);
        }
        Console.WriteLine("Watch finished.");
    }

    /// <summary>Attributes keyed by their printed label (name, parameter, map), valued by their printed value.</summary>
    public static Dictionary<string, string> Attributes(IMemoryReader memory, AttributeNameTable names, AcdRecord acd) =>
        AcdAttributeReader.Read(memory, acd.AttributesAddress)
            .GroupBy(a => Label(a, names)).ToDictionary(g => g.Key, g => g.First().FormattedValue);

    public static string Label(AcdAttribute a, AttributeNameTable names) =>
        (names.NameOf(a.AttributeId) ?? $"0x{a.AttributeId:X}")
        + (a.Parameter != AcdAttribute.NoParameter ? $"[0x{a.Parameter:X}]" : "")
        + (a.Map != 0 ? $"@{a.Map}" : "");

    private static void PrintAttributeChanges(Dictionary<string, string> old, Dictionary<string, string> now, long elapsedMs)
    {
        foreach (var (key, value) in now)
            if (!old.TryGetValue(key, out var was) || was != value)
                Console.WriteLine($"[{elapsedMs,6}ms] attribute {key}: {was ?? "(absent)"} -> {value}");
        foreach (var key in old.Keys.Where(k => !now.ContainsKey(k)))
            Console.WriteLine($"[{elapsedMs,6}ms] attribute {key}: {old[key]} -> (removed)");
    }
}
