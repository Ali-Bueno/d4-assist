using System.Buffers.Binary;

namespace D4Memory.Scan;

/// <summary>One revealed-scene list entry (the "A" record; see RevealedListMatcher).</summary>
public readonly record struct RevealedHit(long Address, uint IdSScene, int Sno, string? Name, uint K);

/// <summary>
/// Finds revealed-scene list entries: 96-byte records naming every revealed scene of the dungeon instance
/// (no position). Layout verified by offline RE of two live captures; see research/client-memory.md §2.
/// Offsets are relative to the address of the u32 `sno` field, `a`.
/// </summary>
public static class RevealedListMatcher
{
    public const int Alignment = 4;

    // idSScene is a scene-instance id, distinct from the SNO namespace; observed range across both captures.
    public const uint IdSSceneMin = 0x80000;
    public const uint IdSSceneMax = 0x8FFFF;
    public const uint MarkerConstant = 0xA;
    public const uint HasInfoFlag = 1;

    private const int OffsetIdSScene = -8;
    private const int OffsetMarker = -4;
    private const int OffsetFlag = 4;
    private const int OffsetK = 8;

    public static List<RevealedHit> FindHits(ReadOnlySpan<byte> buffer, long baseAddress, CandidateIndex candidates)
    {
        var hits = new List<RevealedHit>();
        for (int off = 0; off + sizeof(int) <= buffer.Length; off += Alignment)
        {
            if (off + OffsetIdSScene < 0 || off + OffsetK + sizeof(uint) > buffer.Length) continue; // window edge
            int sno = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(off, sizeof(int)));
            if (!candidates.TryGet(sno, out var candidate)) continue;

            uint idSScene = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(off + OffsetIdSScene, sizeof(uint)));
            if (idSScene < IdSSceneMin || idSScene > IdSSceneMax) continue;
            if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(off + OffsetMarker, sizeof(uint))) != MarkerConstant) continue;
            if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(off + OffsetFlag, sizeof(uint))) != HasInfoFlag) continue;

            uint k = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(off + OffsetK, sizeof(uint)));
            hits.Add(new RevealedHit(baseAddress + off, idSScene, candidate.Sno, candidate.Name, k));
        }
        return hits;
    }
}
