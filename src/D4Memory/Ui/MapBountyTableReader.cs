using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4Memory.Ui;

/// <summary>A legion or world boss the client knows of: where, when it shows on the map, when it starts, and a third time shortly after the start whose
/// meaning is unknown (3 minutes for a legion, 2 for a world boss). UTC.</summary>
public sealed record MapEvent(float X, float Y, DateTime Warmup, DateTime Start, DateTime Third);

/// <summary>What the client holds on the bounties asked for and the world's events.</summary>
/// <param name="Pins">The quests with a pin in the map's table; none with the map closed.</param>
/// <param name="Ends">When a quest's bounty ends, UTC; none for a legion or a world boss.</param>
/// <param name="Events">The legions and world bosses known.</param>
public sealed record MapBounties(IReadOnlySet<int> Pins, IReadOnlyDictionary<int, DateTime> Ends, IReadOnlyList<MapEvent> Events)
{
    public static readonly MapBounties None = new(new HashSet<int>(), new Dictionary<int, DateTime>(), []);
}

/// <summary>
/// Which of the given quests the world map holds a bounty pin for, when they end, and what legions and world bosses come (research/map-points.md §4b). While
/// the map is open the client keeps an array of 808-byte records, one per pin: the quest's SNO, 0x10 on FLT_MAX, 0x1C on the SNO again. A world boss's and a
/// legion's world state read up at every place all the time; this table names the places of the coming event (live 2026-11-01: from 14:13 for the legion of
/// 14:20, Carrowcrest and Chakhir, and the game's cursor text agreed at Carrowcrest). The ends stand in other objects, 168 bytes each: the SNO, 0x34 on -1,
/// 0x50 on the end in unix seconds (live 2026-11-01: the whisper `Bounty_OWC_Hawe_Verge_01` read 18:50 as the game said 56 minutes at 17:54). The events in
/// objects of their own: x, y and z floats, then warmup, start and a third time as unix seconds in 64 bits. No pointer path to any is known, so the heap is
/// scanned: about 10 s the first time and every few minutes after, in case something moved, else only the regions they were found in.
/// </summary>
/// <param name="utcNow">The clock the times are checked against; the system's by default.</param>
public sealed class MapBountyTableReader(IMemoryReader memory, Func<DateTime>? utcNow = null)
{
    private const int SecondSnoOffset = 0x1C, MarkOffset = 0x10, RecordBytes = 0x20;
    private const int EndGuardOffset = 0x34, EndOffset = 0x50;
    private const uint Mark = 0x7F7FFFFF, EndGuard = 0xFFFFFFFF;
    private const uint HeapProtection = NativeMethods.PageReadWrite, HeapType = NativeMethods.MemPrivate;
    private const int FilterBits = 16;
    private const uint FilterMask = (1u << FilterBits) - 1;

    // Words before and from an event's warmup: x, y, z; warmup, start, third, each with its upper half zero.
    private const int EventWordsBefore = 3, EventWords = 6;

    // An end outside these is some other number that happens to lie there. A whisper's ended object stays a while (17:20 still read at 17:35).
    private static readonly TimeSpan EndsAfter = TimeSpan.FromHours(-1), EndsBefore = TimeSpan.FromDays(1);

    // Likewise for an event: its warmup a legion's 10 minutes or a world boss's hour before the start, its third time minutes after it.
    private static readonly TimeSpan EventsAfter = TimeSpan.FromHours(-12), EventsBefore = TimeSpan.FromDays(1);
    private const uint MaxWarmupSeconds = 2 * 3600, MaxThirdSeconds = 3600;

    /// <summary>How often a read looks through the whole heap even with everything where it was.</summary>
    public static readonly TimeSpan FullScanInterval = TimeSpan.FromMinutes(5);

    private static readonly int ScanThreads = Math.Max(1, Environment.ProcessorCount / 2);

    private IReadOnlyList<MemoryRegion> _known = [];
    private DateTime _lastFullScan;

    private readonly record struct Times(uint EndFrom, uint EndTo, uint EventFrom, uint EventTo);

    private sealed class Hits
    {
        public readonly ConcurrentDictionary<int, MemoryRegion> Pins = new();
        public readonly ConcurrentDictionary<int, (uint End, MemoryRegion Region)> Ends = new();
        public readonly ConcurrentDictionary<MapEvent, MemoryRegion> Events = new();
    }

    /// <summary>What the client holds of <paramref name="quests"/> and the events. Slow when something is not where it was last time.</summary>
    public MapBounties Read(IReadOnlySet<int> quests)
    {
        if (quests.Count == 0) return MapBounties.None;
        var filter = new bool[1 << FilterBits];
        foreach (int quest in quests) filter[(uint)quest & FilterMask] = true;
        var clock = DateTime.SpecifyKind(utcNow?.Invoke() ?? DateTime.UtcNow, DateTimeKind.Utc);
        var now = new DateTimeOffset(clock);
        var times = new Times(
            (uint)(now + EndsAfter).ToUnixTimeSeconds(), (uint)(now + EndsBefore).ToUnixTimeSeconds(),
            (uint)(now + EventsAfter).ToUnixTimeSeconds(), (uint)(now + EventsBefore).ToUnixTimeSeconds());
        var hits = Scan(_known, quests, filter, times);
        if (hits.Pins.IsEmpty || hits.Ends.IsEmpty || hits.Events.IsEmpty || clock - _lastFullScan >= FullScanInterval)
        {
            hits = Scan(memory.Regions().Where(r => r.Type == HeapType && r.Protection == HeapProtection), quests, filter, times);
            _known = [.. hits.Pins.Values.Concat(hits.Ends.Values.Select(e => e.Region)).Concat(hits.Events.Values).Distinct()];
            _lastFullScan = clock;
        }
        return new MapBounties(hits.Pins.Keys.ToHashSet(), hits.Ends.ToDictionary(e => e.Key, e => Utc(e.Value.End)), [.. hits.Events.Keys]);
    }

    private static DateTime Utc(uint seconds) => DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;

    private Hits Scan(IEnumerable<MemoryRegion> regions, IReadOnlySet<int> quests, bool[] filter, Times times)
    {
        var hits = new Hits();
        Parallel.ForEach(regions, new ParallelOptions { MaxDegreeOfParallelism = ScanThreads }, () => new byte[RegionWindows.DefaultWindowBytes], (region, _, buffer) =>
        {
            foreach (var (address, data) in RegionWindows.ReadRegion(memory, region, buffer))
                FindInWindow(quests, filter, times, hits, region, address, data.Span);
            return buffer;
        }, _ => { });
        return hits;
    }

    private static void FindInWindow(
        IReadOnlySet<int> quests, bool[] filter, Times times, Hits hits, MemoryRegion region, long address, ReadOnlySpan<byte> data)
    {
        int first = (int)((sizeof(int) - address % sizeof(int)) % sizeof(int));
        if (first >= data.Length) return;
        var words = MemoryMarshal.Cast<byte, int>(data[first..]);
        int last = words.Length - RecordBytes / sizeof(int);
        int endWord = EndOffset / sizeof(int);
        for (int i = 0; i <= last; i++)
        {
            int value = words[i];
            if ((uint)value >= times.EventFrom && (uint)value <= times.EventTo && EventAt(words, i) is { } found) hits.Events.TryAdd(found, region);
            if (!filter[(uint)value & FilterMask] || !quests.Contains(value)) continue;
            if ((uint)words[i + MarkOffset / sizeof(int)] == Mark && words[i + SecondSnoOffset / sizeof(int)] == value) hits.Pins.TryAdd(value, region);
            if (i + endWord >= words.Length || (uint)words[i + EndGuardOffset / sizeof(int)] != EndGuard) continue;
            uint end = (uint)words[i + endWord];
            if (end >= times.EndFrom && end <= times.EndTo) hits.Ends.AddOrUpdate(value, (end, region), (_, old) => end > old.End ? (end, region) : old);
        }
    }

    // The event whose warmup is the word at i, if the words around it make one.
    private static MapEvent? EventAt(ReadOnlySpan<int> words, int i)
    {
        if (i < EventWordsBefore || i + EventWords > words.Length || words[i + 1] != 0 || words[i + 3] != 0 || words[i + 5] != 0) return null;
        uint warmup = (uint)words[i], start = (uint)words[i + 2], third = (uint)words[i + 4];
        if (start <= warmup || start - warmup > MaxWarmupSeconds || third < start || third - start > MaxThirdSeconds) return null;
        float x = BitConverter.Int32BitsToSingle(words[i - 3]), y = BitConverter.Int32BitsToSingle(words[i - 2]);
        return float.IsFinite(x) && float.IsFinite(y) ? new MapEvent(x, y, Utc(warmup), Utc(start), Utc(third)) : null;
    }
}
