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

    public static void Run(IMemoryReader memory, RActorRecord target, string label, long stride, int seconds, int intervalMs)
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
                PrintChanges(previous, current, clock.ElapsedMilliseconds);
            }
            Array.Copy(current, previous, current.Length);
        }
        Console.WriteLine("Watch finished.");
    }

    private static void PrintChanges(byte[] old, byte[] now, long elapsedMs)
    {
        int changed = 0;
        for (int off = 0; off + WordSize <= old.Length; off += WordSize)
        {
            uint a = BinaryPrimitives.ReadUInt32LittleEndian(old.AsSpan(off, WordSize));
            uint b = BinaryPrimitives.ReadUInt32LittleEndian(now.AsSpan(off, WordSize));
            if (a == b) continue;
            if (++changed <= MaxLinesPerBurst)
                Console.WriteLine($"[{elapsedMs,6}ms] +0x{off:X}: 0x{a:X8} -> 0x{b:X8}");
        }
        if (changed > MaxLinesPerBurst) Console.WriteLine($"[{elapsedMs,6}ms] ... {changed - MaxLinesPerBurst} more changed word(s)");
    }
}
