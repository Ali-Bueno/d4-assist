using D4Memory.Ui;

namespace D4Memory.Tests;

public class MapBountyTableReaderTests
{
    private const int Carrowcrest = 2000001, Chakhir = 2000002, Alcarnus = 2000003;
    private const uint HeapType = 0x20000, HeapProtection = 0x04; // MEM_PRIVATE, PAGE_READWRITE

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

    [Fact]
    public void It_finds_the_pins_by_their_record_not_any_mention_of_the_quest()
    {
        var bytes = Heap((0x104, Carrowcrest), (0x42C, Chakhir));
        BitConverter.TryWriteBytes(bytes.AsSpan(0x800), Alcarnus); // only named: the bounty table's own copy, or a text
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, bytes, HeapType, HeapProtection);

        var pins = new MapBountyTableReader(memory).Read(new HashSet<int> { Carrowcrest, Chakhir, Alcarnus });

        Assert.Equal(new HashSet<int> { Carrowcrest, Chakhir }, pins);
    }

    [Fact]
    public void Only_heap_regions_are_read()
    {
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x10000, Heap((0x104, Carrowcrest)), type: 0, HeapProtection);

        Assert.Empty(new MapBountyTableReader(memory).Read(new HashSet<int> { Carrowcrest }));
    }

    [Fact]
    public void The_next_read_looks_first_where_the_last_found_them()
    {
        var memory = new SpyReader();
        memory.Inner.AddRegion(0x10000, Heap(), HeapType, HeapProtection);
        memory.Inner.AddRegion(0x20000, Heap((0x104, Carrowcrest)), HeapType, HeapProtection);
        var reader = new MapBountyTableReader(memory);
        Assert.Contains(Carrowcrest, reader.Read(new HashSet<int> { Carrowcrest }));

        memory.Reads.Clear();
        Assert.Contains(Carrowcrest, reader.Read(new HashSet<int> { Carrowcrest }));

        Assert.DoesNotContain(memory.Reads, a => a < 0x20000);
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
