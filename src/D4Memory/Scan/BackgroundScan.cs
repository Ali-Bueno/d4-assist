namespace D4Memory.Scan;

/// <summary>Walks memory regions beside the running game: on few threads and below the game's priority, so a scan takes spare cycles
/// and never the game's. Nothing waits on one but a background reader.</summary>
public static class BackgroundScan
{
    // A quarter of the machine at most: a whole-heap miss took 9.7 s on 8 threads and 57 s on one (live 2026-09-30, 15 GB heap).
    private const int ProcessorsPerThread = 4, MaxThreads = 4;

    /// <summary>The threads a scan runs on.</summary>
    public static readonly int Threads = Math.Clamp(Environment.ProcessorCount / ProcessorsPerThread, 1, MaxThreads);

    /// <summary>Runs <paramref name="scan"/> on every region, with a window buffer of its thread's own.</summary>
    public static void ForEach(IEnumerable<MemoryRegion> regions, Action<MemoryRegion, ParallelLoopState, byte[]> scan) =>
        Parallel.ForEach(regions, new ParallelOptions { MaxDegreeOfParallelism = Threads },
            () => (Buffer: new byte[RegionWindows.DefaultWindowBytes], Priority: LowerPriority()),
            (region, loop, state) =>
            {
                scan(region, loop, state.Buffer);
                return state;
            },
            state => Thread.CurrentThread.Priority = state.Priority);

    // Pool threads and the caller run scans: each gets its own priority back.
    private static ThreadPriority LowerPriority()
    {
        var priority = Thread.CurrentThread.Priority;
        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        return priority;
    }
}
