using System.Buffers.Binary;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// Depth-1 heap snapshot for UI-state searches: every 8-byte word in the live module's writable data that points
/// into readable memory, plus the first <c>blockBytes</c> at its target. Diffing snapshots taken with a panel
/// closed and open finds <c>[base+rva]+offset</c> routes (research/client-memory.md §5).
/// </summary>
/// <remarks>Binary format: "D4PS", u64 module base, u32 count, then per pointer: u32 rva, u64 target, u16 length, bytes.</remarks>
internal static class PointerSnapshotWriter
{
    private static readonly byte[] Magic = "D4PS"u8.ToArray();

    public static (int Pointers, long Bytes) Write(
        ProcessReader process, long moduleBase, IReadOnlyList<MemoryRegion> moduleRegions, string outPath, int blockBytes)
    {
        var heap = process.Regions().OrderBy(r => r.Address).ToArray();
        var starts = heap.Select(r => r.Address).ToArray();
        var targets = new List<(uint Rva, long Target, int Length)>();
        var seen = new HashSet<long>();
        foreach (var region in moduleRegions)
        {
            var bytes = new byte[region.Size];
            if (!process.TryRead(region.Address, bytes, bytes.Length)) continue;
            for (int off = 0; off + sizeof(long) <= bytes.Length; off += sizeof(long))
            {
                long value = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(off));
                if (!seen.Add(value) || RegionOf(heap, starts, value) is not { } target) continue;
                if (target.Address <= moduleBase && moduleBase < target.Address + target.Size) continue;
                int length = (int)Math.Min(blockBytes, target.Address + target.Size - value);
                targets.Add(((uint)(region.Address + off - moduleBase), value, length));
            }
        }

        long written = 0;
        using var file = new BinaryWriter(File.Create(outPath));
        file.Write(Magic);
        file.Write(moduleBase);
        file.Write(targets.Count);
        var block = new byte[blockBytes];
        foreach (var (rva, target, length) in targets)
        {
            bool ok = process.TryRead(target, block, length);
            file.Write(rva);
            file.Write(target);
            file.Write((ushort)(ok ? length : 0));
            if (ok) { file.Write(block, 0, length); written += length; }
        }
        return (targets.Count, written);
    }

    private static MemoryRegion? RegionOf(MemoryRegion[] regions, long[] starts, long address)
    {
        int i = Array.BinarySearch(starts, address);
        if (i < 0) i = ~i - 1;
        return i >= 0 && address < regions[i].Address + regions[i].Size ? regions[i] : null;
    }
}
