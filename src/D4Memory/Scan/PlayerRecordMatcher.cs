using System.Buffers.Binary;
using System.Numerics;

namespace D4Memory.Scan;

/// <summary>A validated hit for the local player's static position record (see PlayerRecordMatcher).</summary>
public readonly record struct PlayerRecordHit(
    long PositionAddress, int SceneSno, int WorldSno, int ActorSno, Vector3 Position, uint LeadingWord, uint TrailingWord);

/// <summary>
/// Finds the local player's static position record by structural signature: the fields around the position
/// must all agree with independently-known-good values (the current World SNO, a player actor SNO, a real
/// Scene SNO, and a zero marker), so a match is a strong structural signal, not a value coincidence. Layout
/// from offline RE of two live captures; see research/client-memory.md §3. All offsets are relative to the
/// position field `p`. Pure function over a byte buffer, unit-tested without any process access.
/// </summary>
public static class PlayerRecordMatcher
{
    /// <summary>u32 at p+12 in the live record; a stale copy in the game's second image view holds 0 (live run 2026-09-28).</summary>
    public const uint LiveRecordFlag = 1;

    public const int Alignment = 4;

    // Field offsets relative to `p` (research/client-memory.md §3).
    private const int OffsetLeadingWord = -20; // observed 0xCDCD0001; reported only, task spec says don't require it
    private const int OffsetSceneSno = -16;
    private const int OffsetWorldSno = -12;
    private const int OffsetActorSno = -8;
    private const int OffsetZeroMarker = -4;
    private const int OffsetPosition = 0;
    private const int OffsetTrailingWord = 12; // observed == 1; reported only, task spec says don't require it

    /// <summary>Bytes the record needs before `p` (earliest field checked is p-20, reported not validated).</summary>
    public const int RecordBytesBefore = -OffsetLeadingWord;

    /// <summary>Bytes the record needs after `p` (the last field read is p+12, a u32).</summary>
    public const int RecordBytesAfter = OffsetTrailingWord + sizeof(uint);

    /// <summary>Sane world-coordinate bound (world units): far beyond any known Diablo IV map extent
    /// (research/dungeon-data.md tile/world AABB observations), so it only rejects garbage/uninitialized floats.</summary>
    public const float PositionBoundUnits = 1_000_000f;

    public static List<PlayerRecordHit> FindHits(
        ReadOnlySpan<byte> buffer, long baseAddress, IReadOnlySet<int> playerActorSnos, int worldSno, IReadOnlySet<int> sceneSnos)
    {
        var hits = new List<PlayerRecordHit>();
        for (int off = 0; off + sizeof(int) <= buffer.Length; off += Alignment)
        {
            if (off - RecordBytesBefore < 0 || off + RecordBytesAfter > buffer.Length) continue; // window edge

            // Cheapest, most selective field first: the current World SNO is a single known value.
            if (ReadI32(buffer, off + OffsetWorldSno) != worldSno) continue;
            if (ReadU32(buffer, off + OffsetZeroMarker) != 0) continue;

            int actorSno = ReadI32(buffer, off + OffsetActorSno);
            if (!playerActorSnos.Contains(actorSno)) continue;

            int sceneSno = ReadI32(buffer, off + OffsetSceneSno);
            if (!sceneSnos.Contains(sceneSno)) continue;

            var position = ReadVector3(buffer, off + OffsetPosition);
            if (!IsSanePosition(position)) continue;

            uint leadingWord = ReadU32(buffer, off + OffsetLeadingWord);
            uint trailingWord = ReadU32(buffer, off + OffsetTrailingWord);
            hits.Add(new PlayerRecordHit(baseAddress + off, sceneSno, worldSno, actorSno, position, leadingWord, trailingWord));
        }
        return hits;
    }

    private static bool IsSanePosition(Vector3 p) =>
        float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z)
        && MathF.Abs(p.X) <= PositionBoundUnits && MathF.Abs(p.Y) <= PositionBoundUnits && MathF.Abs(p.Z) <= PositionBoundUnits;

    private static int ReadI32(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(offset, sizeof(int)));

    private static uint ReadU32(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, sizeof(uint)));

    private static Vector3 ReadVector3(ReadOnlySpan<byte> buffer, int offset) => new(
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 4, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 8, 4)));
}
