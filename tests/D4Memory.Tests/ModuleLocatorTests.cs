using System.Buffers.Binary;
using D4Memory.Safety;
using D4Memory.Scan;

namespace D4Memory.Tests;

public class ModuleLocatorTests
{
    // winnt.h constants the test builds synthetic regions with; ModuleLocator itself reads the same
    // values from Safety.NativeMethods (internal to the main assembly, not visible from this test project).
    private const uint MemImage = 0x1000000;
    private const uint PageReadOnly = 0x02;
    private const uint PageReadWrite = 0x04;
    private const uint PageWriteCopy = 0x08;
    private const uint PageExecuteRead = 0x20;

    private const int PeOffset = 0x80;
    private const uint MatchingTimeDateStamp = 0x60ABCDEF;
    private const uint MatchingSizeOfImage = 0x2FE6000;

    private sealed class FakeReader(Dictionary<long, byte[]> pages) : IMemoryReader
    {
        public IReadOnlyList<MemoryRegion> Regions() => [];

        public bool TryRead(long address, byte[] buffer, int count)
        {
            if (!pages.TryGetValue(address, out var bytes))
                throw new InvalidOperationException($"Unexpected read at 0x{address:X} — ModuleLocator must only read a header page VirtualQueryEx already reported as safe.");
            Array.Copy(bytes, buffer, count);
            return true;
        }
    }

    private static byte[] MakeHeader(uint timeDateStamp, uint sizeOfImage)
    {
        var buffer = new byte[PeHeaderReader.HeaderReadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, 0x5A4D);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0x3C), PeOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(PeOffset), 0x00004550);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(PeOffset + 8), timeDateStamp);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(PeOffset + 24 + 56), sizeOfImage);
        return buffer;
    }

    private static string WriteExeFile(uint timeDateStamp, uint sizeOfImage)
    {
        string path = Path.GetTempFileName();
        File.WriteAllBytes(path, MakeHeader(timeDateStamp, sizeOfImage));
        return path;
    }

    [Fact]
    public void FindsTheBaseWhoseHeaderMatchesTheOnDiskExe()
    {
        string exePath = WriteExeFile(MatchingTimeDateStamp, MatchingSizeOfImage);
        try
        {
            var regions = new[]
            {
                new MemoryRegion(0x1000, 0x1000, MemImage, AllocationBase: 0x1000, Protection: PageReadOnly), // wrong module
                new MemoryRegion(0x5000, 0x1000, MemImage, AllocationBase: 0x5000, Protection: PageReadOnly), // the one we want
            };
            var reader = new FakeReader(new()
            {
                [0x1000] = MakeHeader(0xDEAD, 0xBEEF),
                [0x5000] = MakeHeader(MatchingTimeDateStamp, MatchingSizeOfImage),
            });

            var found = ModuleLocator.Locate(regions, reader, exePath);

            Assert.NotNull(found);
            Assert.Equal(0x5000, found!.Value.Base);
            Assert.Equal(MatchingSizeOfImage, found.Value.SizeOfImage);
        }
        finally { File.Delete(exePath); }
    }

    [Fact]
    public void NeverReadsABaseWhoseHeaderPageWasNotReportedSafe()
    {
        // AllocationBase 0x9000 has a MEM_IMAGE region, but no region starts exactly at the base (as if the
        // header page were PAGE_EXECUTE_* and so excluded by ProcessReader.ImageRegions). ModuleLocator must
        // skip it without ever calling TryRead there — the fake reader throws if it does.
        string exePath = WriteExeFile(MatchingTimeDateStamp, MatchingSizeOfImage);
        try
        {
            var regions = new[]
            {
                new MemoryRegion(0x9010, 0x1000, MemImage, AllocationBase: 0x9000, Protection: PageReadWrite),
            };
            var reader = new FakeReader([]);

            var found = ModuleLocator.Locate(regions, reader, exePath);

            Assert.Null(found);
        }
        finally { File.Delete(exePath); }
    }

    [Fact]
    public void ReturnsNullWhenNoBaseMatches()
    {
        string exePath = WriteExeFile(MatchingTimeDateStamp, MatchingSizeOfImage);
        try
        {
            var regions = new[] { new MemoryRegion(0x1000, 0x1000, MemImage, AllocationBase: 0x1000, Protection: PageReadOnly) };
            var reader = new FakeReader(new() { [0x1000] = MakeHeader(1, 2) });

            Assert.Null(ModuleLocator.Locate(regions, reader, exePath));
        }
        finally { File.Delete(exePath); }
    }

    [Fact]
    public void WritableRegionsAtKeepsOnlyReadWriteAndWriteCopyPagesOfThatModule()
    {
        const long moduleBase = 0x5000;
        var regions = new[]
        {
            new MemoryRegion(moduleBase, 0x1000, MemImage, moduleBase, PageReadOnly),       // header: excluded
            new MemoryRegion(moduleBase + 0x1000, 0x1000, MemImage, moduleBase, PageReadWrite),   // .data: included
            new MemoryRegion(moduleBase + 0x2000, 0x1000, MemImage, moduleBase, PageWriteCopy),   // untouched .data: included
            new MemoryRegion(moduleBase + 0x3000, 0x1000, MemImage, moduleBase, PageExecuteRead), // .text: excluded
            new MemoryRegion(0x8000, 0x1000, MemImage, 0x8000, PageReadWrite),                    // a different module: excluded
        };

        var writable = ModuleLocator.WritableRegionsAt(regions, moduleBase);

        Assert.Equal(2, writable.Count);
        Assert.All(writable, r => Assert.Equal(moduleBase, r.AllocationBase));
        Assert.Contains(writable, r => r.Protection == PageReadWrite);
        Assert.Contains(writable, r => r.Protection == PageWriteCopy);
    }
}
