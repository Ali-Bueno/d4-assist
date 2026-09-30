using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using D4Memory.Scan;
using D4Memory.SceneRoute;

namespace D4Memory.Pools;

/// <summary>One occupied pool slot: its address and the id stored at slot+8.</summary>
public readonly record struct PoolSlot(long Address, uint Id);

/// <summary>
/// A chunked object pool of the game object G (research/client-memory.md §1, §4): the header and the chunk table in use
/// give the record stride; slots are enumerated or looked up by id. Reads only through IMemoryReader.
/// </summary>
public sealed class PoolContainer
{
    /// <summary>Offset of the record id inside a slot; the same field marks a free slot.</summary>
    public const int IdOffsetInSlot = 0x8;

    /// <summary>u32[X+8] value a free (unoccupied) slot holds.</summary>
    private const uint FreeSlotSentinel = 0xFFFFFFFF;

    /// <summary>Id-to-index mask of the by-id lookup (client-memory.md §1 step 4).</summary>
    private const uint IdIndexMask = 0x7FFFF;

    // The chunk-table selector array holds 16 entries; k picks one (client-memory.md §1 step 2).
    private const int ChunkTableEntryCount = 16;
    private const int ChunkTableEntrySize = 0x18;

    // stride = (StrideBase + StrideKMultiplier * k) rounded down to StrideRoundingUnit (client-memory.md §1 step 2).
    private const int StrideKMultiplier = 16;
    private const int StrideRoundingUnit = 0x10;

    /// <summary>Sanity bound on the reported max index: only rejects a misread container field.</summary>
    private const int MaxIndexSanityBound = 1_000_000;

    // Chunk-table validation: the first slots of chunk 0 are checked; a table is taken when at least MinMatches occupied
    // slots map back to their index and none contradicts it. When no table fits the layout's stride (an update resized
    // the record), every 16-byte stride up to MaxStride is tried.
    private const int ValidationSlots = 64;
    private const int MinMatches = 2;
    private const int MinStride = 0x100, MaxStride = 0x2000;

    // The chunk table in use per container, per process: it only changes with the container (a new session).
    private static readonly ConditionalWeakTable<IMemoryReader, ConcurrentDictionary<(long Container, int StrideBase), (uint K, long Stride)>> Tables = new();

    private readonly IMemoryReader _reader;
    private readonly long _chunks;
    private readonly int _maxIndex;
    private readonly int _shift;
    private readonly uint _mask;

    private PoolContainer(IMemoryReader reader, long chunks, int maxIndex, int shift, uint mask, long stride)
    {
        _reader = reader;
        _chunks = chunks;
        _maxIndex = maxIndex;
        _shift = shift;
        _mask = mask;
        Stride = stride;
    }

    /// <summary>Byte distance between consecutive slots of a chunk.</summary>
    public long Stride { get; }

    /// <summary>False (route unavailable) on any unreadable or inconsistent header field; never throws.</summary>
    public static bool TryOpen(
        IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, PoolParameters parameters, out PoolContainer pool)
    {
        pool = null!;
        var layout = offsets.Pool;
        if (!TryReadU64(reader, moduleBase + offsets.GameObjectPointerRva, out ulong g) || g == 0) return false;
        if (!TryReadU64(reader, (long)g + parameters.ContainerOffset, out ulong p) || p == 0) return false;
        if (parameters.ContainerBehindPointer && (!TryReadU64(reader, (long)p, out p) || p == 0)) return false;

        if (!TryReadU32(reader, (long)p + layout.MaxIndexOffset, out uint maxIndexRaw)) return false;
        if (!TryReadU32(reader, (long)p + layout.ShiftOffset, out uint shiftRaw)) return false;
        if (!TryReadU32(reader, (long)p + layout.MaskOffset, out uint mask)) return false;
        int maxIndex = (int)maxIndexRaw;
        int shift = (int)shiftRaw;
        if (maxIndex < 0 || maxIndex > MaxIndexSanityBound || shift is < 0 or >= 64) return false;

        // The game picks the table with a per-session seed kept at a build-specific address (client-memory.md §1 step 2).
        // The table is found by its content instead, so an update doesn't need that address.
        var tables = Tables.GetOrCreateValue(reader);
        if (!tables.TryGetValue(((long)p, parameters.StrideBase), out var table))
        {
            if (!TryFindTable(reader, (long)p, layout, parameters.StrideBase, maxIndex, mask, out table)) return false;
            tables[((long)p, parameters.StrideBase)] = table;
        }

        long entry = (long)p + layout.ChunkTableOffset + ChunkTableEntrySize * table.K;
        if (!TryReadU64(reader, entry, out ulong chunks) || chunks == 0) return false;

        pool = new PoolContainer(reader, (long)chunks, maxIndex, shift, mask, table.Stride) { ChunkTableIndex = table.K };
        return true;
    }

    /// <summary>The chunk table in use (the seeded k).</summary>
    public uint ChunkTableIndex { get; private init; }

    // In the table in use, an occupied slot's id maps back to its index (idx = id & 0x7FFFF); any other table or stride
    // reads other bytes there. The layout's stride formula is tried first for every table.
    private static bool TryFindTable(
        IMemoryReader reader, long p, PoolLayout layout, int strideBase, int maxIndex, uint mask, out (uint K, long Stride) table)
    {
        table = default;
        int slots = (int)Math.Min(Math.Min(maxIndex, mask) + 1L, ValidationSlots);
        var chunk0 = new ulong[ChunkTableEntryCount];
        for (uint k = 0; k < ChunkTableEntryCount; k++)
            if (TryReadU64(reader, p + layout.ChunkTableOffset + ChunkTableEntrySize * k, out ulong chunks) && chunks != 0)
                TryReadU64(reader, (long)chunks, out chunk0[k]);

        int best = 0;
        for (uint k = 0; k < ChunkTableEntryCount; k++)
        {
            long stride = StrideFor(strideBase, k);
            if (chunk0[k] == 0 || !TryReadSlots(reader, (long)chunk0[k], slots, stride, out var bytes)) continue;
            int score = Score(bytes, slots, stride);
            if (score > best) (best, table) = (score, (k, stride));
        }
        if (best >= MinMatches) return true;

        for (uint k = 0; k < ChunkTableEntryCount; k++)
        {
            if (chunk0[k] == 0 || !TryReadSlots(reader, (long)chunk0[k], slots, MaxStride, out var bytes)) continue;
            for (long stride = MinStride; stride <= MaxStride; stride += StrideRoundingUnit)
            {
                int score = Score(bytes, slots, stride);
                if (score > best) (best, table) = (score, (k, stride));
            }
        }
        return best >= MinMatches;
    }

    private static bool TryReadSlots(IMemoryReader reader, long chunkBase, int slots, long stride, out byte[] bytes)
    {
        bytes = new byte[(int)(slots * stride)];
        return reader.TryRead(chunkBase, bytes, bytes.Length);
    }

    // Occupied slots whose id maps back to their index; 0 when any occupied slot contradicts it.
    private static int Score(byte[] bytes, int slots, long stride)
    {
        int matches = 0;
        for (int idx = 0; idx < slots; idx++)
        {
            long at = idx * stride + IdOffsetInSlot;
            if (at + sizeof(uint) > bytes.Length) break;
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan((int)at));
            if (id == FreeSlotSentinel) continue;
            if ((id & IdIndexMask) != idx) return 0;
            matches++;
        }
        return matches;
    }

    /// <summary>Record stride for chunk-table index <paramref name="k"/>.</summary>
    public static long StrideFor(long strideBase, uint k) =>
        (strideBase + StrideKMultiplier * k) & ~(long)(StrideRoundingUnit - 1);

    /// <summary>Every readable, non-free slot in index order; unreadable chunks and slots are skipped.</summary>
    public IEnumerable<PoolSlot> OccupiedSlots()
    {
        for (int idx = 0; idx <= _maxIndex; idx++)
        {
            if (!TryGetSlotAddress(idx, out long slot)) continue;
            if (!TryReadU32(_reader, slot + IdOffsetInSlot, out uint id) || id == FreeSlotSentinel) continue;
            yield return new PoolSlot(slot, id);
        }
    }

    /// <summary>Looks a slot up by id; valid only when the slot still holds exactly that id.</summary>
    public bool TryGetSlotById(uint id, out long slotAddress)
    {
        slotAddress = 0;
        int idx = (int)(id & IdIndexMask);
        if (idx > _maxIndex || !TryGetSlotAddress(idx, out long slot)) return false;
        if (!TryReadU32(_reader, slot + IdOffsetInSlot, out uint stored) || stored != id) return false;
        slotAddress = slot;
        return true;
    }

    private bool TryGetSlotAddress(int idx, out long slot)
    {
        slot = 0;
        if (!TryReadU64(_reader, _chunks + 8L * (idx >> _shift), out ulong chunkBase) || chunkBase == 0) return false;
        slot = (long)chunkBase + (idx & _mask) * Stride;
        return true;
    }

    public static bool TryReadU32(IMemoryReader reader, long address, out uint value)
    {
        var buffer = new byte[sizeof(uint)];
        if (!reader.TryRead(address, buffer, buffer.Length)) { value = 0; return false; }
        value = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        return true;
    }

    public static bool TryReadU64(IMemoryReader reader, long address, out ulong value)
    {
        var buffer = new byte[sizeof(ulong)];
        if (!reader.TryRead(address, buffer, buffer.Length)) { value = 0; return false; }
        value = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        return true;
    }
}
