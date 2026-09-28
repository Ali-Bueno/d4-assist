using System.Buffers.Binary;
using System.Numerics;
using D4Data.Model;
using D4Memory.Scan;
using D4Memory.SceneRoute;

namespace D4Memory.Tests;

public class SceneRouteTests
{
    private const long ModuleBase = 0x1000;

    // Offline facts for SNO 1234, same shape as CSceneRecordMatcherTests (see it for the record layout).
    private static readonly CandidateScene Candidate =
        new(1234, "Test_Scene", [32f, 32f], ExitMask: 15, Bounds: new Box([0f, 0f, -1f], [32f, 32f, 1f]));
    private static readonly (float X, float Y, float Z) Wp = (64f, 96f, 3.5f);
    private const uint TileHash = 0x20003; // (64/32)<<16 | (96/32)

    private static GameBuildOffsets BuildOffsets() => new(
        GameObjectPointerRva: 0x8,
        SceneContainerOffset: 0x8,
        SceneContainerSize: 0x100,
        SeedRva: 0x10,
        CapacityOffset: 0x18,
        MaxIndexOffset: 0x20,
        ShiftOffset: 0x24,
        MaskOffset: 0x28,
        ChunkTableOffset: 0x40,
        CSceneVtableRva: 0x500);

    [Fact]
    public void ReadsTheOccupiedValidatedSlotAndSkipsFreeAndBadVtableSlots()
    {
        var offsets = BuildOffsets();
        var buffer = new byte[0xD000];

        const long g = 0x2000, p = 0x9000, chunks = 0xA000, chunkArrayBase = 0xB000;
        const uint seed = 3;
        // k = ((p / SceneContainerSize) + seed) & 0xF = ((0x9000/0x100) + 3) & 0xF = (0x90 + 3) & 0xF = 3.
        const long k = 3;
        // stride = (0x92F + 16*k) & ~0xF, the same formula SceneRoute computes (client-memory.md §1 step 2).
        const long stride = 0x950;

        WriteU64(buffer, ModuleBase + offsets.GameObjectPointerRva, g);
        WriteU64(buffer, g + offsets.SceneContainerOffset, p);
        WriteU32(buffer, ModuleBase + offsets.SeedRva, seed);
        WriteU32(buffer, p + offsets.MaxIndexOffset, 2); // idx 0..2 inclusive
        WriteU32(buffer, p + offsets.ShiftOffset, 4);
        WriteU32(buffer, p + offsets.MaskOffset, 0xF);
        WriteU64(buffer, p + offsets.ChunkTableOffset + 0x18 * k, chunks);
        WriteU64(buffer, chunks, chunkArrayBase); // idx>>shift == 0 for every idx used here

        long slot0 = chunkArrayBase, slot1 = chunkArrayBase + stride, slot2 = chunkArrayBase + 2 * stride;
        long expectedVtable = ModuleBase + offsets.CSceneVtableRva;

        WriteU32(buffer, slot0 + 8, 5); // occupied
        WriteU64(buffer, slot0 + 0x158, expectedVtable);
        WriteGoodRecord(buffer, a: slot0 + 0x168, expectedVtable);

        WriteU32(buffer, slot1 + 8, 0xFFFFFFFF); // free: must be skipped without reading anything else

        WriteU32(buffer, slot2 + 8, 9); // occupied but...
        WriteU64(buffer, slot2 + 0x158, unchecked((long)0xBADBADBADBADBADBUL)); // ...wrong vtable: must be skipped

        var reader = new FakeMemoryReader();
        reader.AddRegion(0, buffer);
        var candidates = new CandidateIndex(new Dictionary<int, CandidateScene> { [1234] = Candidate });

        bool available = SceneRouteReader.TryRead(reader, ModuleBase, offsets, candidates, out var tiles);

        Assert.True(available);
        var hit = Assert.Single(tiles);
        Assert.Equal(1234, hit.Sno);
        Assert.Equal(slot0 + 0x168, hit.Address);
        Assert.Equal(64f, hit.Wp.X, 3);
    }

    [Fact]
    public void UnreadableGameObjectPointerMakesTheRouteUnavailable()
    {
        var reader = new FakeMemoryReader(); // no regions: every read fails
        var candidates = new CandidateIndex(new Dictionary<int, CandidateScene>());

        bool available = SceneRouteReader.TryRead(reader, ModuleBase, BuildOffsets(), candidates, out var tiles);

        Assert.False(available);
        Assert.Empty(tiles);
    }

    [Fact]
    public void ImplausibleMaxIndexMakesTheRouteUnavailable()
    {
        var offsets = BuildOffsets();
        var buffer = new byte[0x9100];
        const long g = 0x2000, p = 0x9000;

        WriteU64(buffer, ModuleBase + offsets.GameObjectPointerRva, g);
        WriteU64(buffer, g + offsets.SceneContainerOffset, p);
        WriteU32(buffer, ModuleBase + offsets.SeedRva, 0);
        WriteU32(buffer, p + offsets.MaxIndexOffset, uint.MaxValue); // corrupted/garbage field

        var reader = new FakeMemoryReader();
        reader.AddRegion(0, buffer);
        var candidates = new CandidateIndex(new Dictionary<int, CandidateScene>());

        bool available = SceneRouteReader.TryRead(reader, ModuleBase, offsets, candidates, out var tiles);

        Assert.False(available);
        Assert.Empty(tiles);
    }

    /// <summary>Writes one valid CScene record with `snoScene` at <paramref name="a"/> (see CSceneRecordMatcherTests).</summary>
    private static void WriteGoodRecord(byte[] buffer, long a, long vtable)
    {
        WriteI32(buffer, a, Candidate.Sno);
        WriteU32(buffer, a - 48, TileHash);
        WriteI32(buffer, a - 44, Candidate.ExitMask);
        WriteU64(buffer, a - 16, vtable);
        WriteU32(buffer, a - 20, 1); // has-DRLG-info flag
        WriteQuat(buffer, a + 80, (0f, 0f, 0f, 1f));
        WriteVec3(buffer, a + 96, Wp);
        WriteVec3(buffer, a + 136, (16f, 16f, 0f)); // local AABB center
        WriteVec3(buffer, a + 148, (16f, 16f, 1f)); // local AABB half-extent
        WriteVec3(buffer, a + 160, (16f + Wp.X, 16f + Wp.Y, Wp.Z)); // world AABB center
        WriteVec3(buffer, a + 172, (16f, 16f, 1f)); // world AABB half-extent
        WriteU32(buffer, a + 416, TileHash);
        WriteI32(buffer, a + 420, Candidate.ExitMask);
    }

    private static void WriteI32(byte[] b, long off, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan((int)off), v);
    private static void WriteU32(byte[] b, long off, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan((int)off), v);
    private static void WriteU64(byte[] b, long off, long v) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan((int)off), (ulong)v);

    private static void WriteVec3(byte[] b, long off, (float X, float Y, float Z) v)
    {
        int o = (int)off;
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o), v.X);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o + 4), v.Y);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o + 8), v.Z);
    }

    private static void WriteQuat(byte[] b, long off, (float X, float Y, float Z, float W) q)
    {
        int o = (int)off;
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o), q.X);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o + 4), q.Y);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o + 8), q.Z);
        BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(o + 12), q.W);
    }
}
