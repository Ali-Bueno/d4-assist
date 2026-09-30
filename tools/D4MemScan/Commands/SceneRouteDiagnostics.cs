using System.Buffers.Binary;
using D4Memory.Pools;
using D4Memory.Scan;
using D4Memory.SceneRoute;
using D4MemScan.Scan;

namespace D4MemScan.Commands;

/// <summary>
/// `scene-route --diagnose`: prints every intermediate value of the static route and locates the slots of
/// tiles the full heap scan found, so a route that comes back empty (seen live 2026-09-28 after a portal
/// inside S15_Triad_B_Worldstone) can be traced to the step that broke.
/// </summary>
internal static class SceneRouteDiagnostics
{
    // Same slot layout as SceneRouteReader (research/client-memory.md §1).
    private const int SnoSceneOffsetInSlot = 0x168;
    private const int VtableOffsetInSlot = 0x158;
    private const int SceneIdOffsetInSlot = 0x8;
    private const int ChunkTableEntryCount = 16;
    private const int ChunkTableEntrySize = 0x18;
    // Enough chunk pointers to cover any pool seen so far; only used for this diagnostic walk.
    private const int MaxChunksToInspect = 64;

    public static void Run(IMemoryReader process, IReadOnlyList<long> moduleBases, GameBuildOffsets offsets, CandidateIndex candidates, bool slotsOnly = false)
    {
        if (slotsOnly) { foreach (var b in moduleBases) DumpSlots(process, b, offsets, candidates); return; }
        var scan = new SceneScanner().Scan(process, candidates, ProgressPrinter.Create());
        Console.WriteLine($"Full scan: {scan.Tiles.Count} tile(s).");
        foreach (var t in scan.Tiles)
            Console.WriteLine($"  hit {t.Name} wp=({t.Wp.X:F0},{t.Wp.Y:F0}) snoScene@0x{t.Address:X} slot@0x{t.Address - SnoSceneOffsetInSlot:X}");

        foreach (var moduleBase in moduleBases)
        {
            Console.WriteLine($"Module view 0x{moduleBase:X}:");
            ulong expectedVtable = (ulong)moduleBase + (ulong)offsets.CSceneVtableRva;
            var g = U64(process, moduleBase + offsets.GameObjectPointerRva);
            var p = g is { } gv and not 0 ? U64(process, (long)gv + offsets.Scene.ContainerOffset) : null;
            uint? k = PoolContainer.TryOpen(process, moduleBase, offsets, offsets.Scene, out var pool) ? pool.ChunkTableIndex : null;
            Console.WriteLine($"  G=0x{g:X} P=0x{p:X} table in use: {(k is { } kv ? kv.ToString() : "none validates")}");
            if (p is not { } pv || pv == 0 || k is null) continue;

            var capacity = U32(process, (long)pv + offsets.Pool.CapacityOffset);
            var maxIndex = U32(process, (long)pv + offsets.Pool.MaxIndexOffset);
            var shift = U32(process, (long)pv + offsets.Pool.ShiftOffset);
            var mask = U32(process, (long)pv + offsets.Pool.MaskOffset);
            Console.WriteLine($"  capacity={capacity} maxIndex={maxIndex} shift={shift} mask=0x{mask:X} k={k} stride=0x{pool.Stride:X}");

            for (int e = 0; e < ChunkTableEntryCount; e++)
            {
                long entry = (long)pv + offsets.Pool.ChunkTableOffset + ChunkTableEntrySize * e;
                Console.WriteLine($"  chunkTable[{e,2}] = 0x{U64(process, entry):X} 0x{U64(process, entry + 8):X} 0x{U64(process, entry + 16):X}");
            }

            foreach (var t in scan.Tiles)
            {
                long slot = t.Address - SnoSceneOffsetInSlot;
                var vt = U64(process, slot + VtableOffsetInSlot);
                var id = U32(process, slot + SceneIdOffsetInSlot);
                Console.WriteLine($"  {t.Name}: vtable {(vt == expectedVtable ? "ok" : $"0x{vt:X} (expected 0x{expectedVtable:X})")}, id=0x{id:X}, located: {Locate(process, pv, offsets, slot)}");
            }
        }
    }

    // Searches every chunk-table entry's chunk pointer array for a chunk containing the slot at a whole
    // multiple of any candidate stride, to tell "wrong k/stride" from "slot not in this container at all".
    private static string Locate(IMemoryReader process, ulong p, GameBuildOffsets offsets, long slot)
    {
        var found = new List<string>();
        for (int e = 0; e < ChunkTableEntryCount; e++)
        {
            var chunks = U64(process, (long)p + offsets.Pool.ChunkTableOffset + ChunkTableEntrySize * e);
            if (chunks is not { } cv || cv == 0) continue;
            for (int c = 0; c < MaxChunksToInspect; c++)
            {
                var chunkBase = U64(process, (long)cv + 8L * c);
                if (chunkBase is not { } bv || bv == 0) continue;
                long offset = slot - (long)bv;
                if (offset < 0) continue;
                for (uint kk = 0; kk < ChunkTableEntryCount; kk++)
                    if (offset % Stride(kk, offsets) == 0 && offset / Stride(kk, offsets) < 4096)
                        found.Add($"entry {e} chunk {c} index {offset / Stride(kk, offsets)} stride 0x{Stride(kk, offsets):X}");
            }
        }
        return found.Count == 0 ? "not in any chunk of this container" : string.Join("; ", found.Take(6));
    }

    // Every occupied slot of the active chunk entry, with the raw fields CSceneRecordMatcher checks, to see
    // why a live scene (e.g. a preset, non-DRLG room) is rejected.
    private static void DumpSlots(IMemoryReader process, long moduleBase, GameBuildOffsets offsets, CandidateIndex candidates)
    {
        var g = U64(process, moduleBase + offsets.GameObjectPointerRva);
        if (g is not { } gv || gv == 0) return;
        var p = U64(process, (long)gv + offsets.Scene.ContainerOffset);
        if (p is not { } pv || pv == 0 || !PoolContainer.TryOpen(process, moduleBase, offsets, offsets.Scene, out var pool)) return;
        uint k = pool.ChunkTableIndex;
        int maxIndex = (int)(U32(process, (long)pv + offsets.Pool.MaxIndexOffset) ?? 0);
        int shift = (int)(U32(process, (long)pv + offsets.Pool.ShiftOffset) ?? 0);
        uint mask = U32(process, (long)pv + offsets.Pool.MaskOffset) ?? 0;
        var chunks = U64(process, (long)pv + offsets.Pool.ChunkTableOffset + ChunkTableEntrySize * k) ?? 0;
        Console.WriteLine($"Module 0x{moduleBase:X}: maxIndex={maxIndex} k={k}");
        for (int idx = 0; idx <= maxIndex; idx++)
        {
            var chunkBase = U64(process, (long)chunks + 8L * (idx >> shift)) ?? 0;
            if (chunkBase == 0) continue;
            long slot = (long)chunkBase + (idx & mask) * pool.Stride;
            var id = U32(process, slot + SceneIdOffsetInSlot);
            long a = slot + SnoSceneOffsetInSlot;
            int sno = (int)(U32(process, a) ?? 0);
            string name = candidates.TryGet(sno, out var c) ? $"{c.Name} offlineExit={c.ExitMask} size={string.Join('x', c.TileSize)}" : "(not a candidate)";
            Console.WriteLine($"  [{idx,3}] id=0x{id:X8} sno={sno} {name} hash=0x{U32(process, a - 48):X} exit={U32(process, a - 44)} node=0x{U32(process, a - 40):X} group=0x{U32(process, a - 36):X} flag20={U32(process, a - 20)} " +
                $"wp=({F(process, a + 96)},{F(process, a + 100)},{F(process, a + 104)}) localAabb c=({F(process, a + 136)},{F(process, a + 140)}) h=({F(process, a + 148)},{F(process, a + 152)}) exitCopy={U32(process, a + 420)} hashCopy=0x{U32(process, a + 416):X}");
        }
    }

    private static string F(IMemoryReader reader, long address)
    {
        var buffer = new byte[4];
        return reader.TryRead(address, buffer, 4) ? BinaryPrimitives.ReadSingleLittleEndian(buffer).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) : "?";
    }

    private static long Stride(uint k, GameBuildOffsets offsets) => PoolContainer.StrideFor(offsets.Scene.StrideBase, k);

    private static ulong? U64(IMemoryReader reader, long address)
    {
        var buffer = new byte[8];
        return reader.TryRead(address, buffer, 8) ? BinaryPrimitives.ReadUInt64LittleEndian(buffer) : null;
    }

    private static uint? U32(IMemoryReader reader, long address)
    {
        var buffer = new byte[4];
        return reader.TryRead(address, buffer, 4) ? BinaryPrimitives.ReadUInt32LittleEndian(buffer) : null;
    }
}
