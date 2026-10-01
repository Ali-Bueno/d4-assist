using System.Buffers.Binary;
using System.Numerics;

namespace D4Memory.Scan;

/// <summary>A validated live CScene record hit (see CSceneRecordMatcher).</summary>
/// <param name="NodeId">DRLG node id at a-40; a multi-tile room shares NodeId/GroupId across its tiles.</param>
/// <param name="GroupId">DRLG group id at a-36.</param>
public readonly record struct CSceneHit(
    long Address, int Sno, string? Name, Vector3 Wp, Quaternion Q, int ExitMask,
    uint TileHash, bool HashMatchesFormula, ulong Vtable, Vector3 WorldAabbCenter, Vector3 WorldAabbHalf,
    uint NodeId, uint GroupId);

/// <summary>
/// Finds live "CScene" instance records by structural signature: every field below must agree with the
/// candidate's offline scene data, so a match is a strong structural signal rather than a value coincidence.
/// Layout verified by offline RE of two live captures; see research/client-memory.md §1. All offsets are
/// relative to the address of the u32 `snoScene` field, `a`. Pure function over a byte buffer so it can be
/// unit-tested without any process access; see D4Memory.Safety.ProcessReader for the live memory source.
/// </summary>
public static class CSceneRecordMatcher
{
    public const int Alignment = 4;

    // Field offsets relative to `a` (the address of snoScene).
    private const int OffsetTileHash = -48;
    private const int OffsetExitMask = -44;      // eTileType
    private const int OffsetNodeId = -40;         // DRLG node id; reported only, not validated
    private const int OffsetGroupId = -36;        // DRLG group id; reported only, not validated
    private const int OffsetHasDrlgInfoFlag = -20; // observed == 1 on every valid record
    private const int OffsetVtable = -16;         // u64, constant across every record; reported, not validated
    private const int OffsetQuaternion = 80;
    private const int OffsetWorldPosition = 96;
    private const int OffsetLocalAabb = 136;      // 6x float32: center xyz, half-extent xyz
    private const int OffsetWorldAabb = 160;      // 6x float32: center xyz, half-extent xyz
    // A copy of the tile hash and exit mask on ordinary tiles; another cell's on a large flexible POI piece
    // (Ancients_Blood_FLEX_POI_Underworld_01, 96x128: a-48 0xFFFCFFFE / a-44 6, here 0xFFFAFFFC / 14 — live pool dump
    // 2026-10-01), so they are bounds for the record size only, never checked.
    private const int OffsetTileHashCopy = 416;
    private const int OffsetExitMaskCopy = 420;

    /// <summary>Bytes the record needs before `a` (a−52: earliest field checked here is a−48).</summary>
    public const int RecordBytesBefore = 52;

    /// <summary>Bytes the record needs after `a` (the last field checked is a+420, a u32).</summary>
    public const int RecordBytesAfter = OffsetExitMaskCopy + sizeof(uint);

    /// <summary>Slack on |q| == 1 for a unit rotation quaternion (float round-trip noise).</summary>
    public const float QuaternionNormTolerance = 0.02f;

    /// <summary>Slack for the local/world AABB field comparisons against offline data (task spec: 1e-3).</summary>
    public const float AabbTolerance = 1e-3f;

    /// <summary>Tile-hash formula's confirmed unit (32x32 tiles); units for larger tiles are unconfirmed, so
    /// the formula is only ever reported, never required for a match.</summary>
    private const float HashFormulaTileUnit = 32f;

    public static List<CSceneHit> FindHits(ReadOnlySpan<byte> buffer, long baseAddress, CandidateIndex candidates)
    {
        var hits = new List<CSceneHit>();
        for (int off = 0; off + sizeof(int) <= buffer.Length; off += Alignment)
            if (TryMatchAt(buffer, off, candidates, out var hit))
                hits.Add(hit with { Address = baseAddress + off });
        return hits;
    }

    /// <summary>
    /// Validates one candidate `snoScene` at <paramref name="off"/> against the same field checks as
    /// <see cref="FindHits"/>, for a caller that already knows the record's address (e.g. the static scene
    /// route — SceneRoute reads the slot at a known offset instead of scanning a whole region for it).
    /// The returned hit's <see cref="CSceneHit.Address"/> is relative to the buffer, not absolute.
    /// </summary>
    public static bool TryMatchAt(ReadOnlySpan<byte> buffer, int off, CandidateIndex candidates, out CSceneHit hit)
    {
        hit = default;
        if (off - RecordBytesBefore < 0 || off + RecordBytesAfter > buffer.Length) return false; // window edge
        int sno = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(off, sizeof(int)));
        if (!candidates.TryGet(sno, out var candidate)) return false;
        if (!TryMatch(buffer, off, candidate, out hit)) return false;
        hit = hit with { Address = off };
        return true;
    }

    private static bool TryMatch(ReadOnlySpan<byte> buffer, int off, CandidateScene candidate, out CSceneHit hit)
    {
        hit = default;

        var q = ReadQuaternion(buffer, off + OffsetQuaternion);
        if (!IsUnitQuaternion(q)) return false;

        var (localCenter, localHalf) = ReadAabb(buffer, off + OffsetLocalAabb);
        if (!AabbMatchesOffline(localCenter, localHalf, candidate.Bounds)) return false;

        var wp = ReadVector3(buffer, off + OffsetWorldPosition);
        if (!float.IsFinite(wp.X) || !float.IsFinite(wp.Y) || !float.IsFinite(wp.Z)) return false;

        var (worldCenter, worldHalf) = ReadAabb(buffer, off + OffsetWorldAabb);
        if (!NearlyEqual(worldCenter, localCenter + wp, AabbTolerance)) return false;

        if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(off + OffsetHasDrlgInfoFlag, sizeof(uint))) != 1) return false;

        int exitMask = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(off + OffsetExitMask, sizeof(int)));
        // The live mask equals the offline one on ordinary DRLG tiles, is a strict subset on pieces of a large
        // preset room (Triad_B_ThroneRoom_N02_E03_DGN: offline 15, live 7 — live pool dump 2026-09-28) and a
        // superset on a flexible POI room (Mage_Halls_FLEX_POI_HoradricVault_01: offline 9 = N+W, live 15, with
        // filler beyond E — live pool dump 2026-10-01). The tile reports the exits both agree on.
        exitMask &= candidate.ExitMask;

        uint tileHash = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(off + OffsetTileHash, sizeof(uint)));

        ulong vtable = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(off + OffsetVtable, sizeof(ulong)));
        bool formulaMatches = tileHash == FormulaHash(wp);

        uint nodeId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(off + OffsetNodeId, sizeof(uint)));
        uint groupId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(off + OffsetGroupId, sizeof(uint)));

        hit = new CSceneHit(0, candidate.Sno, candidate.Name, wp, q, exitMask, tileHash, formulaMatches, vtable,
            worldCenter, worldHalf, nodeId, groupId);
        return true;
    }

    /// <summary>The observed `(wp.X/32) << 16 | (wp.Y/32)` formula; only confirmed for 32x32 tiles (see class doc).</summary>
    private static uint FormulaHash(Vector3 wp) =>
        (uint)(((int)(wp.X / HashFormulaTileUnit) << 16) | (int)(wp.Y / HashFormulaTileUnit));

    private static bool IsUnitQuaternion(Quaternion q) =>
        float.IsFinite(q.X) && float.IsFinite(q.Y) && float.IsFinite(q.Z) && float.IsFinite(q.W)
        && MathF.Abs(q.Length() - 1f) <= QuaternionNormTolerance;

    private static bool AabbMatchesOffline(Vector3 center, Vector3 half, D4Data.Model.Box offline)
    {
        var offlineMin = new Vector3(offline.Min[0], offline.Min[1], offline.Min[2]);
        var offlineMax = new Vector3(offline.Max[0], offline.Max[1], offline.Max[2]);
        return NearlyEqual(center, (offlineMin + offlineMax) / 2f, AabbTolerance)
            && NearlyEqual(half, (offlineMax - offlineMin) / 2f, AabbTolerance);
    }

    private static bool NearlyEqual(Vector3 a, Vector3 b, float tolerance) =>
        MathF.Abs(a.X - b.X) <= tolerance && MathF.Abs(a.Y - b.Y) <= tolerance && MathF.Abs(a.Z - b.Z) <= tolerance;

    private static (Vector3 Center, Vector3 Half) ReadAabb(ReadOnlySpan<byte> buffer, int offset) =>
        (ReadVector3(buffer, offset), ReadVector3(buffer, offset + 12));

    private static Quaternion ReadQuaternion(ReadOnlySpan<byte> buffer, int offset) => new(
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 4, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 8, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 12, 4)));

    private static Vector3 ReadVector3(ReadOnlySpan<byte> buffer, int offset) => new(
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 4, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(offset + 8, 4)));
}
