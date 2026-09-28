using System.Diagnostics;

namespace D4MemScan.Commands;

/// <summary>Thread-safe progress callback that prints to stderr at each completed percentage step.</summary>
internal static class ProgressPrinter
{
    private const int StepPercent = 10;

    public static Action<long, long> Create()
    {
        var gate = new object();
        var clock = Stopwatch.StartNew();
        int nextPercent = StepPercent;
        return (done, total) =>
        {
            int percent = total == 0 ? 100 : (int)(done * 100 / total);
            lock (gate)
            {
                if (percent < nextPercent) return;
                Console.Error.WriteLine($"  {percent}% of {total / (1L << 30)} GB in {clock.Elapsed.TotalSeconds:F0}s");
                nextPercent = percent / StepPercent * StepPercent + StepPercent;
            }
        };
    }
}
