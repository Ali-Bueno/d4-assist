using D4Memory.Ui;

namespace D4Memory.Tests;

public class MapBountyTableReaderTests
{
    private const int Carrowcrest = 2000001, Chakhir = 2000002, Alcarnus = 2000003;
    private const uint HeapType = 0x20000, HeapProtection = 0x04; // MEM_PRIVATE, PAGE_READWRITE

    private static readonly DateTime Now = new(2026, 11, 1, 17, 54, 0, DateTimeKind.Utc);

    private static byte[] Heap(params (int Offset, int Quest)[] records)
    {
        var bytes = new byte[0x1000];
        foreach (var (offset, quest) in records)
        {
            BitConverter.TryWriteBytes(bytes.AsSpan(offset), quest);
            BitConverter.TryWriteBytes(bytes.AsSpan(offset + 0x10), 0x7F7FFFFFu);
            BitConverter.TryWriteBytes(bytes.AsSpan(offset + 0x1C), quest);
        }
        return bytes;
    }

    private static void WriteEnd(byte[] bytes, int offset, int quest, DateTime end, uint guard = 0xFFFFFFFF)
    {
        BitConverter.TryWriteBytes(bytes.AsSpan(offset), quest);
        BitConverter.TryWriteBytes(bytes.AsSpan(offset + 0x34), guard);
        BitConverter.TryWriteBytes(bytes.AsSpan(offset + 0x50), (uint)new DateTimeOffset(end).ToUnixTimeSeconds());
    }

    private static void WriteEvent(byte[] bytes, int offset, float x, float y, DateTime warmup, DateTime start, DateTime third)
    {
        BitConverter.TryWriteBytes(bytes.AsSpan(offset), x);
        BitConverter.TryWriteBytes(bytes.AsSpan(offset + 4), y);
        BitConverter.TryWriteBytes(bytes.AsSpan(offset + 12), new DateTimeOffset(warmup).ToUnixTimeSeconds());
        BitConverter.TryWriteBytes(bytes.AsSpan(offset + 20), new DateTimeOffset(start).ToUnixTimeSeconds());
        BitConverter.TryWriteBytes(bytes.AsSpan(offset + 28), new DateTimeOffset(third).ToUnixTimeSeconds());
    }

    [Fact]
    public void It_finds_the_pins_by_their_record_not_any_mention_of_the_quest()
    {
        var bytes = Heap((0x104, Carrowcrest), (0x42C, Chakhir));
        BitConverter.TryWriteBytes(bytes.AsSpan(0x800), Alcarnus); // only named: the bounty table's own copy, or a text
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, bytes, HeapType, HeapProtection);

        var held = new MapBountyTableReader(memory).Read(new HashSet<int> { Carrowcrest, Chakhir, Alcarnus });

        Assert.Equal(new HashSet<int> { Carrowcrest, Chakhir }, held.Pins);
    }

    [Theory]
    [InlineData(0x100)] // the eight words a block tests at once: each place in it
    [InlineData(0x104)]
    [InlineData(0x108)]
    [InlineData(0x10C)]
    [InlineData(0x110)]
    [InlineData(0x114)]
    [InlineData(0x118)]
    [InlineData(0x11C)]
    [InlineData(0x1000 - 0x20)] // the last word a record fits at, past the last whole block
    public void A_pin_is_found_wherever_it_falls_among_the_words_tested_together(int offset)
    {
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, Heap((offset, Carrowcrest)), HeapType, HeapProtection);

        Assert.Equal(new HashSet<int> { Carrowcrest }, new MapBountyTableReader(memory).Read(new HashSet<int> { Alcarnus, Carrowcrest }).Pins);
    }

    [Fact]
    public void Quests_at_both_ends_of_the_range_asked_for_are_found()
    {
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, Heap((0x104, Carrowcrest), (0x42C, Alcarnus)), HeapType, HeapProtection);

        var held = new MapBountyTableReader(memory).Read(new HashSet<int> { Carrowcrest, Chakhir, Alcarnus });

        Assert.Equal(new HashSet<int> { Carrowcrest, Alcarnus }, held.Pins);
    }

    [Fact]
    public void Only_heap_regions_are_read()
    {
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, Heap((0x104, Carrowcrest)), type: 0, HeapProtection);

        Assert.Empty(new MapBountyTableReader(memory).Read(new HashSet<int> { Carrowcrest }).Pins);
    }

    [Fact]
    public void The_next_read_looks_first_where_the_last_found_them()
    {
        var memory = new SpyReader();
        memory.Inner.AddRegion(0x10000, Heap(), HeapType, HeapProtection);
        var bytes = Heap((0x104, Carrowcrest));
        WriteEnd(bytes, 0x400, Carrowcrest, Now.AddMinutes(5));
        WriteEvent(bytes, 0x600, 1f, 2f, Now.AddMinutes(-5), Now.AddMinutes(5), Now.AddMinutes(8));
        memory.Inner.AddRegion(0x20000, bytes, HeapType, HeapProtection);
        var reader = new MapBountyTableReader(memory, () => Now);
        Assert.Contains(Carrowcrest, reader.Read(new HashSet<int> { Carrowcrest }).Pins);

        memory.Reads.Clear();
        Assert.Contains(Carrowcrest, reader.Read(new HashSet<int> { Carrowcrest }).Pins);

        Assert.DoesNotContain(memory.Reads, a => a < 0x20000);
    }

    // The agent's review of PR #3: with no event about (most of the time) every read was a whole-heap scan, and each world's new
    // session started from nothing.
    [Fact]
    public void With_no_event_known_the_whole_heap_waits_for_the_event_interval_across_readers()
    {
        var memory = new SpyReader();
        memory.Inner.AddRegion(0x10000, Heap(), HeapType, HeapProtection);
        var bytes = Heap((0x104, Carrowcrest));
        WriteEnd(bytes, 0x400, Carrowcrest, Now.AddMinutes(30));
        memory.Inner.AddRegion(0x20000, bytes, HeapType, HeapProtection);
        var quests = new HashSet<int> { Carrowcrest };
        var now = Now;
        var scans = new MapBountyScanMemory();
        new MapBountyTableReader(memory, () => now, scans).Read(quests);

        memory.Reads.Clear();
        now += TimeSpan.FromMinutes(1);
        Assert.Contains(Carrowcrest, new MapBountyTableReader(memory, () => now, scans).Read(quests).Pins); // another world's session
        Assert.DoesNotContain(memory.Reads, a => a < 0x20000);

        now += MapBountyTableReader.EventScanInterval;
        new MapBountyTableReader(memory, () => now, scans).Read(quests);
        Assert.Contains(memory.Reads, a => a < 0x20000); // in case a legion came
    }

    [Fact]
    public void It_reads_a_quests_end_beside_its_sno()
    {
        var bytes = new byte[0x1000];
        var end = new DateTime(2026, 11, 1, 18, 50, 0, DateTimeKind.Utc);
        WriteEnd(bytes, 0x200, Carrowcrest, end);
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, bytes, HeapType, HeapProtection);

        var held = new MapBountyTableReader(memory, () => Now).Read(new HashSet<int> { Carrowcrest });

        Assert.Equal(end, held.Ends[Carrowcrest]);
        Assert.Empty(held.Pins);
    }

    [Fact]
    public void A_number_that_is_no_end_is_not_taken_for_one()
    {
        var bytes = new byte[0x1000];
        WriteEnd(bytes, 0x100, Carrowcrest, Now.AddDays(3)); // too far off
        WriteEnd(bytes, 0x200, Chakhir, Now.AddMinutes(30), guard: 0); // not this kind of object
        WriteEnd(bytes, 0x300, Alcarnus, Now.AddHours(-2)); // long gone
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, bytes, HeapType, HeapProtection);

        var held = new MapBountyTableReader(memory, () => Now).Read(new HashSet<int> { Carrowcrest, Chakhir, Alcarnus });

        Assert.Empty(held.Ends);
    }

    [Fact]
    public void Of_two_objects_for_a_quest_the_later_end_counts()
    {
        var bytes = new byte[0x1000];
        WriteEnd(bytes, 0x100, Carrowcrest, Now.AddMinutes(-20));
        WriteEnd(bytes, 0x200, Carrowcrest, Now.AddMinutes(40));
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, bytes, HeapType, HeapProtection);

        var held = new MapBountyTableReader(memory, () => Now).Read(new HashSet<int> { Carrowcrest });

        Assert.Equal(Now.AddMinutes(40), held.Ends[Carrowcrest]);
    }

    [Fact]
    public void It_reads_an_events_spot_and_times()
    {
        var bytes = new byte[0x1000];
        var start = Now.AddMinutes(56);
        WriteEvent(bytes, 0x200, -2072f, -600f, start.AddHours(-1), start, start.AddMinutes(2));
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, bytes, HeapType, HeapProtection);

        var held = new MapBountyTableReader(memory, () => Now).Read(new HashSet<int> { Carrowcrest });

        var found = Assert.Single(held.Events);
        Assert.Equal((-2072f, -600f), (found.X, found.Y));
        Assert.Equal((start.AddHours(-1), start, start.AddMinutes(2)), (found.Warmup, found.Start, found.Third));
    }

    [Fact]
    public void Times_that_make_no_event_are_not_taken_for_one()
    {
        var bytes = new byte[0x1000];
        WriteEvent(bytes, 0x100, 1f, 2f, Now, Now.AddHours(5), Now.AddHours(5)); // warmup too long before
        WriteEvent(bytes, 0x200, 1f, 2f, Now, Now.AddMinutes(10), Now.AddMinutes(5)); // third before the start
        WriteEvent(bytes, 0x300, float.NaN, 2f, Now, Now.AddMinutes(10), Now.AddMinutes(13)); // no spot
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, bytes, HeapType, HeapProtection);

        Assert.Empty(new MapBountyTableReader(memory, () => Now).Read(new HashSet<int> { Carrowcrest }).Events);
    }

    [Fact]
    public void Every_few_minutes_a_read_looks_through_the_whole_heap_again()
    {
        var memory = new SpyReader();
        memory.Inner.AddRegion(0x10000, Heap(), HeapType, HeapProtection);
        var bytes = Heap((0x104, Carrowcrest));
        WriteEnd(bytes, 0x400, Carrowcrest, Now.AddMinutes(30));
        WriteEvent(bytes, 0x600, 1f, 2f, Now.AddMinutes(-5), Now.AddMinutes(5), Now.AddMinutes(8));
        memory.Inner.AddRegion(0x20000, bytes, HeapType, HeapProtection);
        var clock = Now;
        var reader = new MapBountyTableReader(memory, () => clock);
        reader.Read(new HashSet<int> { Carrowcrest });

        memory.Reads.Clear();
        clock += MapBountyTableReader.FullScanInterval;
        reader.Read(new HashSet<int> { Carrowcrest });

        Assert.Contains(memory.Reads, a => a < 0x20000);
    }

    private sealed class SpyReader : D4Memory.Scan.IMemoryReader
    {
        public readonly FakeMemoryReader Inner = new();
        public readonly System.Collections.Concurrent.ConcurrentBag<long> Reads = [];
        public IReadOnlyList<D4Memory.Scan.MemoryRegion> Regions() => Inner.Regions();
        public bool TryRead(long address, byte[] buffer, int count)
        {
            Reads.Add(address);
            return Inner.TryRead(address, buffer, count);
        }
    }
}
