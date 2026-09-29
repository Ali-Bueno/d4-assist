using System.Buffers.Binary;
using D4Memory.Actors;
using D4Memory.Pools;
using D4Memory.SceneRoute;

namespace D4Memory.Tests;

public class RActorReaderTests
{
    private const long ModuleBase = 0x1000;
    private const long G = 0x2000, P = 0x9000, Chunks = 0xA000, SlotBase = 0xB000;

    private static GameBuildOffsets BuildOffsets() => new(
        GameObjectPointerRva: 0x8,
        Pool: new PoolLayout(ContainerSize: 0x100, CapacityOffset: 0x18, MaxIndexOffset: 0x20,
            ShiftOffset: 0x24, MaskOffset: 0x28, ChunkTableOffset: 0x40),
        Scene: new PoolParameters(ContainerOffset: 0x8, SeedRva: 0x10, StrideBase: 0x92F),
        Actor: new PoolParameters(ContainerOffset: 0x10, SeedRva: 0x14, StrideBase: 0xC27),
        CSceneVtableRva: 0x500);

    /// <summary>Builds a pool with k = ((P/0x100) + seed) &amp; 0xF = 5, so stride = (0xC27 + 80) &amp; ~0xF = 0xC70.</summary>
    private static (FakeMemoryReader Reader, byte[] Buffer, long Stride) BuildPool(uint maxIndex)
    {
        var offsets = BuildOffsets();
        var buffer = new byte[0x10000];
        const uint seed = 5; // P/0x100 = 0x90, so k = (0x90 + 5) & 0xF = 5
        const long k = 5;
        Write64(buffer, ModuleBase + offsets.GameObjectPointerRva, G);
        Write64(buffer, G + offsets.Actor.ContainerOffset, P);
        Write32(buffer, ModuleBase + offsets.Actor.SeedRva, seed);
        Write32(buffer, P + offsets.Pool.MaxIndexOffset, maxIndex);
        Write32(buffer, P + offsets.Pool.ShiftOffset, 4);
        Write32(buffer, P + offsets.Pool.MaskOffset, 0xF);
        Write64(buffer, P + offsets.Pool.ChunkTableOffset + 0x18 * k, Chunks);
        Write64(buffer, Chunks, SlotBase);
        var reader = new FakeMemoryReader();
        reader.AddRegion(0, buffer);
        return (reader, buffer, PoolContainer.StrideFor(offsets.Actor.StrideBase, (uint)k));
    }

    [Fact]
    public void ReadsOccupiedSlotsUsingTheSeededNonZeroKStrideAndSkipsFreeOnes()
    {
        var (reader, buffer, stride) = BuildPool(maxIndex: 2);
        Assert.Equal(0xC70, stride);

        long slot0 = SlotBase, slot1 = SlotBase + stride, slot2 = SlotBase + 2 * stride;
        WriteActor(buffer, slot0, id: 7, acd: 70, sno: 176832, pos: (10f, 20f, 3f), scene: 0x8001, placement: 0);
        Write32(buffer, slot1 + 8, 0xFFFFFFFF); // free
        WriteActor(buffer, slot2, id: 9, acd: 90, sno: 555, pos: (1f, 2f, 3f), scene: 0x8002, placement: -1);

        Assert.True(RActorReader.TryRead(reader, ModuleBase, BuildOffsets(), out var actors));

        Assert.Equal(2, actors.Count);
        var placed = actors[0];
        Assert.Equal(slot0, placed.SlotAddress);
        Assert.Equal(7u, placed.Id);
        Assert.Equal(70u, placed.AcdId);
        Assert.Equal(176832, placed.ActorSno);
        Assert.Equal(10f, placed.Position.X);
        Assert.Equal(1f, placed.Rotation.W);
        Assert.Equal(0x8001u, placed.SceneId);
        Assert.True(placed.IsPlaced);
    }

    [Fact]
    public void UnplacedRecordsAreReportedWithTheirFlag()
    {
        var (reader, buffer, stride) = BuildPool(maxIndex: 1);
        WriteActor(buffer, SlotBase + stride, id: 9, acd: 90, sno: 555, pos: (1f, 2f, 3f), scene: 0x8002, placement: -1);

        Assert.True(RActorReader.TryRead(reader, ModuleBase, BuildOffsets(), out var actors));

        var unplaced = Assert.Single(actors, a => a.Id == 9);
        Assert.Equal(-1, unplaced.Placement);
        Assert.False(unplaced.IsPlaced);
    }

    [Fact]
    public void LookupByIdRequiresTheStoredIdToMatch()
    {
        var (reader, buffer, stride) = BuildPool(maxIndex: 2);
        WriteActor(buffer, SlotBase + stride, id: 1, acd: 1, sno: 1, pos: (0f, 0f, 0f), scene: 0, placement: 0);
        Assert.True(PoolContainer.TryOpen(reader, ModuleBase, BuildOffsets(), BuildOffsets().Actor, out var pool));

        Assert.True(pool.TryGetSlotById(1, out long found));
        Assert.Equal(SlotBase + stride, found);
        Assert.False(pool.TryGetSlotById(0x80001, out _)); // same index, id differs from the stored one
    }

    [Fact]
    public void UnreadableRouteMakesTheReaderUnavailable()
    {
        Assert.False(RActorReader.TryRead(new FakeMemoryReader(), ModuleBase, BuildOffsets(), out var actors));
        Assert.Empty(actors);
    }

    private static void WriteActor(byte[] b, long slot, uint id, uint acd, int sno, (float X, float Y, float Z) pos, uint scene, int placement)
    {
        Write32(b, slot + 0x08, id);
        Write32(b, slot + 0x0C, acd);
        Write32(b, slot + 0x5B8, unchecked((uint)sno));
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan((int)slot + 0x750 + 12), 1f); // quaternion w
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan((int)slot + 0x760), pos.X);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan((int)slot + 0x764), pos.Y);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan((int)slot + 0x768), pos.Z);
        Write32(b, slot + 0x7A8, scene);
        Write32(b, slot + 0x7AC, unchecked((uint)placement));
    }

    private static void Write32(byte[] b, long off, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan((int)off), v);
    private static void Write64(byte[] b, long off, long v) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan((int)off), (ulong)v);
}
