using System.Buffers.Binary;
using D4Memory.Pools;
using D4Memory.SceneRoute;

namespace D4Memory.Tests;

public class PoolContainerTests
{
    private const long ModuleBase = 0x1000;
    private const long G = 0x2000, P = 0x9000, Chunks = 0xA000, SlotBase = 0x10000, DecoyChunks = 0xA800, DecoySlots = 0x60000;

    private static GameBuildOffsets Offsets(int strideBase) => new(
        GameObjectPointerRva: 0x8,
        Pool: new PoolLayout(ContainerSize: 0x100, CapacityOffset: 0x18, MaxIndexOffset: 0x20,
            ShiftOffset: 0x24, MaskOffset: 0x28, ChunkTableOffset: 0x40),
        Scene: new PoolParameters(ContainerOffset: 0x8, StrideBase: 0x92F),
        Actor: new PoolParameters(ContainerOffset: 0x10, StrideBase: strideBase),
        CSceneVtableRva: 0);

    // A pool whose table k holds slots with `stride` and ids idx | 0x80000 at the given indices; table 2 is a decoy whose
    // chunk holds unrelated words.
    private static FakeMemoryReader BuildPool(uint k, long stride, params int[] occupied)
    {
        var offsets = Offsets(0xC27);
        var buffer = new byte[0xA0000];
        Write64(buffer, ModuleBase + offsets.GameObjectPointerRva, G);
        Write64(buffer, G + offsets.Actor.ContainerOffset, P);
        Write32(buffer, P + offsets.Pool.MaxIndexOffset, 63);
        Write32(buffer, P + offsets.Pool.ShiftOffset, 6);
        Write32(buffer, P + offsets.Pool.MaskOffset, 0x3F);
        for (long idx = 0; idx < 64; idx++) Write32(buffer, SlotBase + idx * stride + 8, 0xFFFFFFFF);
        foreach (int idx in occupied) Write32(buffer, SlotBase + idx * stride + 8, 0x80000u | (uint)idx);
        Write64(buffer, P + offsets.Pool.ChunkTableOffset + 0x18 * k, Chunks);
        Write64(buffer, Chunks, SlotBase);

        Write64(buffer, P + offsets.Pool.ChunkTableOffset + 0x18 * 2, DecoyChunks);
        Write64(buffer, DecoyChunks, DecoySlots);
        var random = new Random(4);
        for (long at = DecoySlots; at < buffer.Length - 4; at += 4) Write32(buffer, at, (uint)random.Next());

        var reader = new FakeMemoryReader();
        reader.AddRegion(0, buffer);
        return reader;
    }

    [Fact]
    public void FindsTheChunkTableInUseWithoutTheSeed()
    {
        var offsets = Offsets(0xC27);
        long stride = PoolContainer.StrideFor(0xC27, 5);
        var reader = BuildPool(5, stride, 0, 3, 4);

        Assert.True(PoolContainer.TryOpen(reader, ModuleBase, offsets, offsets.Actor, out var pool));

        Assert.Equal(5u, pool.ChunkTableIndex);
        Assert.Equal(stride, pool.Stride);
        Assert.Equal([0x80000u, 0x80003u, 0x80004u], pool.OccupiedSlots().Select(s => s.Id));
    }

    [Fact]
    public void FindsTheStrideWhenAnUpdateResizedTheRecord()
    {
        var offsets = Offsets(0xC27);
        var reader = BuildPool(7, 0xD40, 1, 2, 9);

        Assert.True(PoolContainer.TryOpen(reader, ModuleBase, offsets, offsets.Actor, out var pool));

        Assert.Equal(7u, pool.ChunkTableIndex);
        Assert.Equal(0xD40, pool.Stride);
    }

    [Fact]
    public void AnEmptyPoolIsUnavailable()
    {
        var offsets = Offsets(0xC27);
        var reader = BuildPool(5, PoolContainer.StrideFor(0xC27, 5));

        Assert.False(PoolContainer.TryOpen(reader, ModuleBase, offsets, offsets.Actor, out _));
    }

    private static void Write32(byte[] b, long at, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan((int)at), v);
    private static void Write64(byte[] b, long at, long v) => BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan((int)at), v);
}
