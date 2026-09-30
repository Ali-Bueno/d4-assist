using D4Memory.Scan;

namespace D4Memory.Tests;

public class RegionWindowsTests
{
    [Fact]
    public void ReadRegion_StopsAtTheWindowThatReachesTheEnd()
    {
        const int regionBytes = 6000, windowBytes = 4096, overlapBytes = 1024;
        var memory = new FakeMemoryReader();
        memory.AddRegion(0x1000, new byte[regionBytes]);
        var region = memory.Regions()[0];

        var windows = RegionWindows.ReadRegion(memory, region, new byte[windowBytes], overlapBytes)
            .Select(w => (w.Address, w.Data.Length)).ToList();

        Assert.Equal([(0x1000L, windowBytes), (0x1000L + windowBytes - overlapBytes, regionBytes - windowBytes + overlapBytes)], windows);
    }
}
