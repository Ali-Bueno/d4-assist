using System.Buffers.Binary;
using D4Memory.Actors;
using D4Memory.Pools;
using D4Memory.SceneRoute;

namespace D4Memory.Tests;

public class AcdReaderTests
{
    private const long ModuleBase = 0x1000;
    private const long G = 0x2000, P = 0x9000, Chunks = 0xA000, Chunk0 = 0x10000, Chunk1 = 0x20000;
    private const int SlotsPerChunk = 16, Shift = 4;
    private const uint K = 5, IdTag = 0x1C80000;
    private const int RActorIdOffset = 0x14;

    private static readonly GameBuildOffsets Offsets = new(
        GameObjectPointerRva: 0x8,
        Pool: new PoolLayout(ContainerSize: 0x100, CapacityOffset: 0x18, MaxIndexOffset: 0x20,
            ShiftOffset: 0x24, MaskOffset: 0x28, ChunkTableOffset: 0x40),
        Scene: new PoolParameters(ContainerOffset: 0x8, StrideBase: 0x92F),
        Actor: new PoolParameters(ContainerOffset: 0x10, StrideBase: 0xC27),
        CSceneVtableRva: 0,
        Acd: new AcdOffsets(new PoolParameters(ContainerOffset: 0x18, StrideBase: 0x627), 0, 0, 0));

    private static readonly long Stride = PoolContainer.StrideFor(Offsets.Acd!.Pool.StrideBase, K);

    // Two chunks of 16 slots, all free; the header reports indices up to maxIndex.
    private static (FakeMemoryReader Reader, byte[] Buffer) BuildPool(uint maxIndex)
    {
        var buffer = new byte[0x30000];
        Write64(buffer, ModuleBase + Offsets.GameObjectPointerRva, G);
        Write64(buffer, G + Offsets.Acd!.Pool.ContainerOffset, P);
        Write32(buffer, P + Offsets.Pool.MaxIndexOffset, maxIndex);
        Write32(buffer, P + Offsets.Pool.ShiftOffset, Shift);
        Write32(buffer, P + Offsets.Pool.MaskOffset, SlotsPerChunk - 1);
        Write64(buffer, P + Offsets.Pool.ChunkTableOffset + 0x18 * K, Chunks);
        Write64(buffer, Chunks, Chunk0);
        Write64(buffer, Chunks + 8, Chunk1);
        for (int idx = 0; idx < 2 * SlotsPerChunk; idx++) Write32(buffer, Slot(idx) + PoolContainer.IdOffsetInSlot, 0xFFFFFFFF);
        var reader = new FakeMemoryReader();
        reader.AddRegion(0, buffer);
        return (reader, buffer);
    }

    private static long Slot(int idx) => (idx < SlotsPerChunk ? Chunk0 : Chunk1) + (idx % SlotsPerChunk) * Stride;

    private static uint Occupy(byte[] buffer, int idx, uint ractorId)
    {
        uint id = IdTag | (uint)idx;
        Write32(buffer, Slot(idx) + PoolContainer.IdOffsetInSlot, id);
        Write32(buffer, Slot(idx) + RActorIdOffset, ractorId);
        return id;
    }

    [Fact]
    public void Refresh_FindsARecordThatLoadedAfterThePoolGrew()
    {
        var (reader, buffer) = BuildPool(maxIndex: SlotsPerChunk - 1);
        uint first = Occupy(buffer, 0, 100);
        Occupy(buffer, 1, 101);
        Assert.True(AcdReader.TryOpen(reader, ModuleBase, Offsets, out var acds));

        uint door = Occupy(buffer, 20, 42991656); // a dungeon door, loaded later in the second chunk
        Write32(buffer, P + Offsets.Pool.MaxIndexOffset, 2 * SlotsPerChunk - 1);

        Assert.False(acds.TryGet(door, out _));
        acds.Refresh();
        Assert.True(acds.TryGet(door, out var record));
        Assert.Equal(42991656u, record.RActorId);
        Assert.True(acds.TryGet(first, out _));
    }

    private static void Write32(byte[] b, long at, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan((int)at), v);
    private static void Write64(byte[] b, long at, long v) => BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan((int)at), v);
}
