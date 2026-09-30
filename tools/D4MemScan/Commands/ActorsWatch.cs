using System.Buffers.Binary;
using System.Diagnostics;
using D4Memory.Actors;
using D4Memory.Pools;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>Re-reads one RActor slot and prints which 4-byte words change (to find a "used" signal on gizmos/prisoners).</summary>
internal static class ActorsWatch
{
    private const int WordSize = sizeof(uint);
    private const int MaxLinesPerBurst = 15;
    private const int PoolIdOffset = PoolContainer.IdOffsetInSlot;

    /// <param name="maxLinesPerBurst">Changed words printed per poll; the player moves, so its watch prints them all.</param>
    public static void Run(
        IMemoryReader memory, RActorRecord target, string label, long stride, int seconds, int intervalMs, int maxLinesPerBurst = MaxLinesPerBurst)
    {
        Console.WriteLine($"Watching RActor id 0x{target.Id:X} at 0x{target.SlotAddress:X} ({label}) for {seconds}s, every {intervalMs}ms.");
        var previous = new byte[stride];
        var current = new byte[stride];
        if (!memory.TryRead(target.SlotAddress, previous, previous.Length))
        {
            Console.WriteLine("Initial slot read failed.");
            return;
        }

        uint id = target.Id;
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            Thread.Sleep(intervalMs);
            if (!memory.TryRead(target.SlotAddress, current, current.Length)) continue;

            uint currentId = BinaryPrimitives.ReadUInt32LittleEndian(current.AsSpan(PoolIdOffset, WordSize));
            if (currentId != id)
            {
                Console.WriteLine($"[{clock.ElapsedMilliseconds,6}ms] slot id 0x{id:X} -> 0x{currentId:X} (freed/reused); baseline reset");
                id = currentId;
            }
            else
            {
                SlotWordDiff.Print(previous, current, clock.ElapsedMilliseconds, maxLinesPerBurst);
            }
            Array.Copy(current, previous, current.Length);
        }
        Console.WriteLine("Watch finished.");
    }
}
