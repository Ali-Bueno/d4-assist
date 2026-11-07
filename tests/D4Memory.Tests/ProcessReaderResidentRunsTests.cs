using System.Runtime.InteropServices;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4Memory.Tests;

public class ProcessReaderResidentRunsTests
{
    private const uint Commit = 0x1000, Reserve = 0x2000, Release = 0x8000, ReadWrite = 0x04;
    private const int Pages = 16;
    private static readonly int PageBytes = Environment.SystemPageSize;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint type, uint protection);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr address, nuint size, uint type);

    [Fact]
    public void Only_the_pages_a_process_touched_count_and_asking_touches_none()
    {
        long size = (long)Pages * PageBytes;
        var block = VirtualAlloc(IntPtr.Zero, (nuint)size, Commit | Reserve, ReadWrite);
        Assert.NotEqual(IntPtr.Zero, block);
        try
        {
            foreach (int page in new[] { 0, 1, 2, 7, 15 }) Marshal.WriteByte(block, page * PageBytes, 1);
            using var reader = ProcessReader.Open(Environment.ProcessId);
            var region = new MemoryRegion((long)block, size);

            var runs = reader.ResidentRuns(region).ToList();

            Assert.Equal([(0L, 3L), (7L, 1L), (15L, 1L)], runs.Select(r => ((r.Address - (long)block) / PageBytes, r.Size / PageBytes)));
            Assert.Equal(runs, reader.ResidentRuns(region).ToList());
        }
        finally { VirtualFree(block, 0, Release); }
    }

    [Fact]
    public void Memory_shared_with_the_gpu_is_no_region_to_scan()
    {
        const uint writeCombine = 0x400;
        nuint size = (nuint)PageBytes;
        var plain = VirtualAlloc(IntPtr.Zero, size, Commit | Reserve, ReadWrite);
        var shared = VirtualAlloc(IntPtr.Zero, size, Commit | Reserve, ReadWrite | writeCombine);
        Assert.NotEqual(IntPtr.Zero, shared);
        try
        {
            using var reader = ProcessReader.Open(Environment.ProcessId);

            var regions = reader.Regions();

            Assert.Contains(regions, r => r.Address == (long)plain);
            Assert.DoesNotContain(regions, r => r.Address == (long)shared);
        }
        finally
        {
            VirtualFree(plain, 0, Release);
            VirtualFree(shared, 0, Release);
        }
    }

    [Fact]
    public void Its_own_process_tells_which_pages_are_in_ram()
    {
        using var reader = ProcessReader.Open(Environment.ProcessId);

        Assert.True(reader.KnowsResidentPages);
    }

    [Fact]
    public void A_region_larger_than_one_query_is_asked_about_whole()
    {
        const int pages = 4096 + 8; // past ProcessReader's query size
        long size = (long)pages * PageBytes;
        var block = VirtualAlloc(IntPtr.Zero, (nuint)size, Commit | Reserve, ReadWrite);
        Assert.NotEqual(IntPtr.Zero, block);
        try
        {
            Marshal.WriteByte(block, (pages - 1) * PageBytes, 1);
            using var reader = ProcessReader.Open(Environment.ProcessId);

            var runs = reader.ResidentRuns(new MemoryRegion((long)block, size)).ToList();

            Assert.Equal([((long)block + (long)(pages - 1) * PageBytes, (long)PageBytes)], runs.Select(r => (r.Address, r.Size)));
        }
        finally { VirtualFree(block, 0, Release); }
    }
}
