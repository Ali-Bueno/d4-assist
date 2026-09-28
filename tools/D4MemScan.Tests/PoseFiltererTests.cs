using System.Buffers.Binary;
using D4Memory.Tests;
using D4MemScan.Scan;

namespace D4MemScan.Tests;

public class PoseFiltererTests
{
    private static byte[] Triple(float x, float y, float z)
    {
        var buffer = new byte[12];
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(0), x);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(4), y);
        BinaryPrimitives.WriteSingleLittleEndian(buffer.AsSpan(8), z);
        return buffer;
    }

    [Fact]
    public void ChangedModeKeepsAddressesThatMovedPastTheThreshold()
    {
        var reader = new FakeMemoryReader();
        reader.AddRegion(0x1000, Triple(15f, 0f, 0f)); // moved 15 units from (0,0,0)
        var previous = new List<PoseSample> { new(0x1000, 0f, 0f, 0f) };

        var survivors = PoseFilterer.Filter(previous, reader, PoseFilterMode.Changed);

        var survivor = Assert.Single(survivors);
        Assert.Equal(15f, survivor.X, 3);
    }

    [Fact]
    public void UnchangedModeKeepsAddressesBelowTheMinimumDistance()
    {
        // Jitter smaller than MinChangeDistanceUnits must still count as "unchanged".
        float jitter = PoseFilterer.MinChangeDistanceUnits / 2;
        var reader = new FakeMemoryReader();
        reader.AddRegion(0x2000, Triple(jitter, 0f, 0f));
        var previous = new List<PoseSample> { new(0x2000, 0f, 0f, 0f) };

        var unchanged = PoseFilterer.Filter(previous, reader, PoseFilterMode.Unchanged);
        var changed = PoseFilterer.Filter(previous, reader, PoseFilterMode.Changed);

        Assert.Single(unchanged);
        Assert.Empty(changed);
    }

    [Fact]
    public void UnreadableAddressIsDroppedFromBothModes()
    {
        var reader = new FakeMemoryReader(); // no regions at all: every address fails to read
        var previous = new List<PoseSample> { new(0x3000, 0f, 0f, 0f) };

        Assert.Empty(PoseFilterer.Filter(previous, reader, PoseFilterMode.Changed));
        Assert.Empty(PoseFilterer.Filter(previous, reader, PoseFilterMode.Unchanged));
    }
}
