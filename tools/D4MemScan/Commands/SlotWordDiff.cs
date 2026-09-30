using System.Buffers.Binary;

namespace D4MemScan.Commands;

/// <summary>Prints the 4-byte words that differ between two reads of one slot.</summary>
internal static class SlotWordDiff
{
    private const int WordSize = sizeof(uint);

    public static void Print(byte[] old, byte[] now, long elapsedMs, int maxLines)
    {
        int changed = 0;
        for (int off = 0; off + WordSize <= Math.Min(old.Length, now.Length); off += WordSize)
        {
            uint a = BinaryPrimitives.ReadUInt32LittleEndian(old.AsSpan(off, WordSize));
            uint b = BinaryPrimitives.ReadUInt32LittleEndian(now.AsSpan(off, WordSize));
            if (a != b && ++changed <= maxLines)
                Console.WriteLine($"[{elapsedMs,6}ms] +0x{off:X}: 0x{a:X8} -> 0x{b:X8}");
        }
        if (changed > maxLines) Console.WriteLine($"[{elapsedMs,6}ms] ... {changed - maxLines} more changed word(s)");
    }
}
