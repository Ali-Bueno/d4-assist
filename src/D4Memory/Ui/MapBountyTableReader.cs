using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4Memory.Ui;

/// <summary>What the open world map holds on the bounties asked for.</summary>
/// <param name="Pins">The quests with a pin in the map's table.</param>
/// <param name="Ends">When a quest's bounty ends, UTC; none for a legion or a world boss.</param>
public sealed record MapBounties(IReadOnlySet<int> Pins, IReadOnlyDictionary<int, DateTime> Ends)
{
    public static readonly MapBounties None = new(new HashSet<int>(), new Dictionary<int, DateTime>());
}

/// <summary>
/// Which of the given quests the world map holds a bounty pin for, and when they end (research/map-points.md §4b). While the map is open the client keeps
/// an array of 808-byte records, one per pin: the quest's SNO, 0x10 on FLT_MAX, 0x1C on the SNO again. A world boss's and a legion's world state read up at
/// every place all the time; this table names the places of the coming event (live 2026-11-01: from 14:13 for the legion of 14:20, Carrowcrest and Chakhir,
/// and the game's cursor text agreed at Carrowcrest). The ends stand in other objects, 168 bytes each: the SNO, 0x34 on -1, 0x50 on the end in unix seconds
/// (live 2026-11-01: the whisper `Bounty_OWC_Hawe_Verge_01` read 18:50 as the game said 56 minutes at 17:54). No pointer path to either is known, so the
/// heap is scanned: about 10 s the first time, then only the regions they were found in.
/// </summary>
/// <param name="utcNow">The clock an end is checked against; the system's by default.</param>
public sealed class MapBountyTableReader(IMemoryReader memory, Func<DateTime>? utcNow = null)
{
    private const int SecondSnoOffset = 0x1C, MarkOffset = 0x10, RecordBytes = 0x20;
    private const int EndGuardOffset = 0x34, EndOffset = 0x50;
    private const uint Mark = 0x7F7FFFFF, EndGuard = 0xFFFFFFFF;
    private const uint HeapProtection = NativeMethods.PageReadWrite, HeapType = NativeMethods.MemPrivate;
    private const int FilterBits = 16;
    private const uint FilterMask = (1u << FilterBits) - 1;

    // An end outside these is some other number that happens to lie there. A whisper's ended object stays a while (17:20 still read at 17:35).
    private static readonly TimeSpan EndsAfter = TimeSpan.FromHours(-1), EndsBefore = TimeSpan.FromDays(1);

    private static readonly int ScanThreads = Math.Max(1, Environment.ProcessorCount / 2);

    private IReadOnlyList<MemoryRegion> _known = [];

    private sealed class Hits
    {
        public readonly ConcurrentDictionary<int, MemoryRegion> Pins = new();
        public readonly ConcurrentDictionary<int, (uint End, MemoryRegion Region)> Ends = new();
    }

    /// <summary>What the map holds of <paramref name="quests"/>; empty with the map closed. Slow when something is not where it was last time.</summary>
    public MapBounties Read(IReadOnlySet<int> quests)
    {
        if (quests.Count == 0) return MapBounties.None;
        var filter = new bool[1 << FilterBits];
        foreach (int quest in quests) filter[(uint)quest & FilterMask] = true;
        var now = new DateTimeOffset(DateTime.SpecifyKind(utcNow?.Invoke() ?? DateTime.UtcNow, DateTimeKind.Utc));
        var ends = ((uint)(now + EndsAfter).ToUnixTimeSeconds(), (uint)(now + EndsBefore).ToUnixTimeSeconds());
        var hits = Scan(_known, quests, filter, ends);
        if (hits.Pins.IsEmpty || hits.Ends.IsEmpty)
        {
            hits = Scan(memory.Regions().Where(r => r.Type == HeapType && r.Protection == HeapProtection), quests, filter, ends);
            _known = [.. hits.Pins.Values.Concat(hits.Ends.Values.Select(e => e.Region)).Distinct()];
        }
        return new MapBounties(hits.Pins.Keys.ToHashSet(), hits.Ends.ToDictionary(e => e.Key, e => DateTimeOffset.FromUnixTimeSeconds(e.Value.End).UtcDateTime));
    }

    private Hits Scan(IEnumerable<MemoryRegion> regions, IReadOnlySet<int> quests, bool[] filter, (uint From, uint To) ends)
    {
        var hits = new Hits();
        Parallel.ForEach(regions, new ParallelOptions { MaxDegreeOfParallelism = ScanThreads }, () => new byte[RegionWindows.DefaultWindowBytes], (region, _, buffer) =>
        {
            foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
                FindInWindow(quests, filter, ends, hits, region, address, data.Span);
            return buffer;
        }, _ => { });
        return hits;
    }

    private static void FindInWindow(
        IReadOnlySet<int> quests, bool[] filter, (uint From, uint To) ends, Hits hits, MemoryRegion region, long address, ReadOnlySpan<byte> data)
    {
        int first = (int)((sizeof(int) - address % sizeof(int)) % sizeof(int));
        if (first >= data.Length) return;
        var words = MemoryMarshal.Cast<byte, int>(data[first..]);
        int last = words.Length - RecordBytes / sizeof(int);
        int endWord = EndOffset / sizeof(int);
        for (int i = 0; i <= last; i++)
        {
            int value = words[i];
            if (!filter[(uint)value & FilterMask] || !quests.Contains(value)) continue;
            if ((uint)words[i + MarkOffset / sizeof(int)] == Mark && words[i + SecondSnoOffset / sizeof(int)] == value) hits.Pins.TryAdd(value, region);
            if (i + endWord >= words.Length || (uint)words[i + EndGuardOffset / sizeof(int)] != EndGuard) continue;
            uint end = (uint)words[i + endWord];
            if (end >= ends.From && end <= ends.To) hits.Ends.AddOrUpdate(value, (end, region), (_, old) => end > old.End ? (end, region) : old);
        }
    }
}
