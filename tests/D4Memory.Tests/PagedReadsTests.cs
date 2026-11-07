using D4Memory.Scan;

namespace D4Memory.Tests;

public class PagedReadsTests
{
    private const long Base = 0x10000;
    private static readonly int PageBytes = Environment.SystemPageSize;

    private sealed class CountingReader(IMemoryReader inner) : IMemoryReader
    {
        public int Calls { get; private set; }

        public IReadOnlyList<MemoryRegion> Regions() => inner.Regions();

        public bool TryRead(long address, byte[] buffer, int count)
        {
            Calls++;
            return inner.TryRead(address, buffer, count);
        }
    }

    private static (CountingReader Counted, FakeMemoryReader Memory, byte[] Data) Memory(int bytes)
    {
        var data = new byte[bytes];
        for (int i = 0; i < bytes; i++) data[i] = (byte)(i * 7);
        var memory = new FakeMemoryReader();
        memory.AddRegion(Base, data);
        return (new CountingReader(memory), memory, data);
    }

    [Fact]
    public void Reads_in_one_page_cost_one_call_and_see_what_lies_there()
    {
        var (counted, _, data) = Memory(PageBytes);
        var pages = new PagedReads(counted);
        var buffer = new byte[16];

        Assert.True(pages.TryRead(Base + 32, buffer, 16));
        Assert.Equal(data.AsSpan(32, 16).ToArray(), buffer);
        Assert.True(pages.TryRead(Base + 64, buffer, 16));
        Assert.Equal(data.AsSpan(64, 16).ToArray(), buffer);

        Assert.Equal(1, counted.Calls);
    }

    [Fact]
    public void A_read_across_pages_joins_them()
    {
        var (counted, _, data) = Memory(PageBytes * 2);
        var buffer = new byte[32];

        Assert.True(new PagedReads(counted).TryRead(Base + PageBytes - 16, buffer, 32));

        Assert.Equal(data.AsSpan(PageBytes - 16, 32).ToArray(), buffer);
    }

    [Fact]
    public void Forgetting_shows_memory_as_it_is_now()
    {
        var (counted, _, data) = Memory(PageBytes);
        var pages = new PagedReads(counted);
        var buffer = new byte[1];

        pages.TryRead(Base, buffer, 1);
        data[0] = 0xAB;
        pages.TryRead(Base, buffer, 1);
        Assert.Equal(0, buffer[0]);

        pages.Forget();
        pages.TryRead(Base, buffer, 1);
        Assert.Equal(0xAB, buffer[0]);
    }

    [Fact]
    public void A_page_only_partly_readable_still_answers_for_what_can_be_read()
    {
        var (counted, _, data) = Memory(64);
        var pages = new PagedReads(counted);
        var buffer = new byte[8];

        Assert.True(pages.TryRead(Base + 8, buffer, 8));
        Assert.Equal(data.AsSpan(8, 8).ToArray(), buffer);
        Assert.False(pages.TryRead(Base + 60, buffer, 8));
    }
}
