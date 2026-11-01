using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4Memory.Ui;

/// <summary>
/// Which of the given quests the world map holds a bounty pin for (research/map-points.md §4b). While the map is open the client keeps an array of
/// 808-byte records, one per pin: the quest's SNO, 0x10 on FLT_MAX, 0x1C on the SNO again. A world boss's and a legion's world state read up at every
/// place all the time; this table names the places of the coming event (live 2026-11-01: from 14:13 for the legion of 14:20, Carrowcrest and Chakhir,
/// and the game's cursor text agreed at Carrowcrest). No pointer path to it is known, so the heap is scanned: about 10 s the first time, then only
/// the regions it was found in.
/// </summary>
public sealed class MapBountyTableReader(IMemoryReader memory)
{
    private const int SecondSnoOffset = 0x1C, MarkOffset = 0x10, RecordBytes = 0x20;
    private const uint Mark = 0x7F7FFFFF;
    private const uint HeapProtection = NativeMethods.PageReadWrite, HeapType = NativeMethods.MemPrivate;
    private const int FilterBits = 16;
    private const uint FilterMask = (1u << FilterBits) - 1;

    private static readonly int ScanThreads = Math.Max(1, Environment.ProcessorCount / 2);

    private IReadOnlyList<MemoryRegion> _known = [];

    /// <summary>The ones of <paramref name="quests"/> with a pin; empty with the map closed. Slow when nothing is where it was last time.</summary>
    public IReadOnlySet<int> Read(IReadOnlySet<int> quests)
    {
        if (quests.Count == 0) return new HashSet<int>();
        var filter = new bool[1 << FilterBits];
        foreach (int quest in quests) filter[(uint)quest & FilterMask] = true;
        var found = Scan(_known, quests, filter);
        if (found.IsEmpty)
        {
            found = Scan(memory.Regions().Where(r => r.Type == HeapType && r.Protection == HeapProtection), quests, filter);
            _known = [.. found.Values.Distinct()];
        }
        return found.Keys.ToHashSet();
    }

    private ConcurrentDictionary<int, MemoryRegion> Scan(IEnumerable<MemoryRegion> regions, IReadOnlySet<int> quests, bool[] filter)
    {
        var found = new ConcurrentDictionary<int, MemoryRegion>();
        Parallel.ForEach(regions, new ParallelOptions { MaxDegreeOfParallelism = ScanThreads }, () => new byte[RegionWindows.DefaultWindowBytes], (region, _, buffer) =>
        {
            foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
                FindInWindow(quests, filter, found, region, address, data.Span);
            return buffer;
        }, _ => { });
        return found;
    }

    private static void FindInWindow(
        IReadOnlySet<int> quests, bool[] filter, ConcurrentDictionary<int, MemoryRegion> found, MemoryRegion region, long address, ReadOnlySpan<byte> data)
    {
        int first = (int)((sizeof(int) - address % sizeof(int)) % sizeof(int));
        if (first >= data.Length) return;
        var words = MemoryMarshal.Cast<byte, int>(data[first..]);
        int last = words.Length - RecordBytes / sizeof(int);
        for (int i = 0; i <= last; i++)
        {
            int value = words[i];
            if (!filter[(uint)value & FilterMask] || (uint)words[i + MarkOffset / sizeof(int)] != Mark || words[i + SecondSnoOffset / sizeof(int)] != value) continue;
            if (quests.Contains(value)) found.TryAdd(value, region);
        }
    }
}
