using D4Memory.Pools;
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
    // Where the CScene record starts inside a pool slot (its snoScene, `a`), relative to the slot's own start — see
    // research/client-memory.md §1, step 3-4. Build 3.2.x; the layout carries the live-checked value (SceneRecordOffset).
    public const int SnoSceneOffsetInSlot = 0x168;
    // The record's vtable sits just before it.
    private const int VtableBeforeRecord = 0x10;

    /// <summary>
    /// Reads every occupied, validated slot of the live CScene pool. Returns false (route unavailable) on
    /// any unreadable or inconsistent field; never throws.
    /// </summary>
    public static bool TryRead(
        IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, CandidateIndex candidates,
        out IReadOnlyList<CSceneHit> tiles)
    {
        tiles = [];
        if (!PoolContainer.TryOpen(reader, moduleBase, offsets, offsets.Scene, out var pool)) return false;

        ulong expectedVtable = (ulong)moduleBase + (ulong)offsets.CSceneVtableRva;
        var hits = new List<CSceneHit>();
        var window = new byte[CSceneRecordMatcher.RecordBytesBefore + CSceneRecordMatcher.RecordBytesAfter];

        foreach (var (slot, _) in pool.OccupiedSlots())
        {
            if (!PoolContainer.TryReadU64(reader, slot + offsets.SceneRecordOffset - VtableBeforeRecord, out ulong vtable)) continue;
            if (offsets.CSceneVtableRva != 0 && vtable != expectedVtable) continue; // inconsistent slot; skip it, not the whole route

            long snoSceneAddress = slot + offsets.SceneRecordOffset;
            if (!reader.TryRead(snoSceneAddress - CSceneRecordMatcher.RecordBytesBefore, window, window.Length)) continue;
            if (CSceneRecordMatcher.TryMatchAt(window, CSceneRecordMatcher.RecordBytesBefore, candidates, out var hit))
                hits.Add(hit with { Address = snoSceneAddress });
        }

        tiles = hits;
        return true;
    }
}
