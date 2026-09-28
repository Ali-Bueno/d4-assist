using System.Buffers.Binary;
using D4Memory.Scan;

namespace D4Memory.SceneRoute;

/// <summary>
/// Static route from the main module to the live CScene pool (research/client-memory.md §1), as a direct
/// alternative to scanning all of readable memory (SceneScanner). Every read is validated against the
/// container's own bookkeeping and the CScene vtable; any inconsistency makes the route "unavailable"
/// rather than throwing, so a caller always has the full scan as a fallback.
/// </summary>
public static class SceneRouteReader
{
    // Field offsets inside one pool slot, relative to the slot's own start (not `a`/snoScene) — see
    // research/client-memory.md §1, step 3-4.
    private const int SceneIdOffsetInSlot = 0x8;
    private const int VtableOffsetInSlot = 0x158;
    private const int SnoSceneOffsetInSlot = 0x168;

    /// <summary>u32[X+8] value a free (unoccupied) slot holds.</summary>
    private const uint FreeSlotSentinel = 0xFFFFFFFF;

    // The chunk-table selector array holds 16 entries of this size; k (below) picks one of them.
    private const int ChunkTableEntryCount = 16;
    private const int ChunkTableEntrySize = 0x18;
    private const uint ChunkTableSelectorMask = 0xF;

    // Per-record stride formula: stride = (StrideFormulaBase + StrideFormulaKMultiplier * k) rounded down
    // to a multiple of StrideRoundingUnit. Both constants are the seeded formula observed in two sessions
    // with different strides (0xA00 and 0x950) — client-memory.md §1 step 2.
    private const int StrideFormulaBase = 0x92F;
    private const int StrideFormulaKMultiplier = 16;
    private const int StrideRoundingUnit = 0x10;

    /// <summary>
    /// Sanity bound on the container's reported max index: far beyond any observed CScene pool size, so it
    /// only rejects a misread/corrupted container field rather than a real dungeon's tile count.
    /// </summary>
    private const int MaxIndexSanityBound = 1_000_000;

    /// <summary>
    /// Reads every occupied, validated slot of the live CScene pool. Returns false (route unavailable) on
    /// any unreadable or inconsistent field; never throws.
    /// </summary>
    public static bool TryRead(
        IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, CandidateIndex candidates,
        out IReadOnlyList<CSceneHit> tiles)
    {
        tiles = [];
        if (!TryReadU64(reader, moduleBase + offsets.GameObjectPointerRva, out ulong g) || g == 0) return false;
        if (!TryReadU64(reader, (long)g + offsets.SceneContainerOffset, out ulong p) || p == 0) return false;
        if (!TryReadU32(reader, moduleBase + offsets.SeedRva, out uint seed)) return false;

        if (!TryReadU32(reader, (long)p + offsets.MaxIndexOffset, out uint maxIndexRaw)) return false;
        if (!TryReadU32(reader, (long)p + offsets.ShiftOffset, out uint shiftRaw)) return false;
        if (!TryReadU32(reader, (long)p + offsets.MaskOffset, out uint mask)) return false;
        int maxIndex = (int)maxIndexRaw;
        int shift = (int)shiftRaw;
        if (maxIndex < 0 || maxIndex > MaxIndexSanityBound || shift is < 0 or >= 64) return false;

        uint k = (uint)((long)(p / (ulong)offsets.SceneContainerSize) + seed) & ChunkTableSelectorMask;
        long stride = (StrideFormulaBase + StrideFormulaKMultiplier * k) & ~(long)(StrideRoundingUnit - 1);

        long chunkTableEntry = (long)p + offsets.ChunkTableOffset + ChunkTableEntrySize * k;
        if (k >= ChunkTableEntryCount) return false; // defensive; ChunkTableSelectorMask already guarantees this
        if (!TryReadU64(reader, chunkTableEntry, out ulong chunks) || chunks == 0) return false;

        ulong expectedVtable = (ulong)moduleBase + (ulong)offsets.CSceneVtableRva;
        var hits = new List<CSceneHit>();
        var window = new byte[CSceneRecordMatcher.RecordBytesBefore + CSceneRecordMatcher.RecordBytesAfter];

        for (int idx = 0; idx <= maxIndex; idx++)
        {
            if (!TryReadU64(reader, (long)chunks + 8L * (idx >> shift), out ulong chunkBase) || chunkBase == 0) continue;
            long slot = (long)chunkBase + (idx & mask) * stride;

            if (!TryReadU32(reader, slot + SceneIdOffsetInSlot, out uint sceneId)) continue;
            if (sceneId == FreeSlotSentinel) continue; // free slot, not an error

            if (!TryReadU64(reader, slot + VtableOffsetInSlot, out ulong vtable)) continue;
            if (vtable != expectedVtable) continue; // inconsistent slot; skip it, not the whole route

            long snoSceneAddress = slot + SnoSceneOffsetInSlot;
            if (!reader.TryRead(snoSceneAddress - CSceneRecordMatcher.RecordBytesBefore, window, window.Length)) continue;
            if (CSceneRecordMatcher.TryMatchAt(window, CSceneRecordMatcher.RecordBytesBefore, candidates, out var hit))
                hits.Add(hit with { Address = snoSceneAddress });
        }

        tiles = hits;
        return true;
    }

    private static bool TryReadU32(IMemoryReader reader, long address, out uint value)
    {
        var buffer = new byte[sizeof(uint)];
        if (!reader.TryRead(address, buffer, buffer.Length)) { value = 0; return false; }
        value = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        return true;
    }

    private static bool TryReadU64(IMemoryReader reader, long address, out ulong value)
    {
        var array = new byte[sizeof(ulong)];
        if (!reader.TryRead(address, array, array.Length)) { value = 0; return false; }
        value = BinaryPrimitives.ReadUInt64LittleEndian(array);
        return true;
    }
}
