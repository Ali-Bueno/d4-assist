using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Numerics;
using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.Actors;

/// <summary>One entry of the client's map-marker list: an actor the map can show, streamed in or not.</summary>
/// <param name="Kind">1 the player, 2 a follower or pet, 8 an NPC, 10 a boss, 11 a gizmo or trigger (<see cref="MapMarkerReader.KindGizmo"/>).</param>
public readonly record struct MapMarker(int ActorSno, Vector3 Position, int LevelAreaSno, int Kind);

/// <summary>
/// Finds and reads the client's map-marker list: a doubly-linked list of 0xA0-byte pool nodes whose head sits in the heap
/// reservation that holds the game object. No pointer leads to it, so its head is found by structure (research/dungeon-objectives.md §19).
/// </summary>
public static class MapMarkerReader
{
    /// <summary>Gizmos and triggers: they never move, so a marker's spot is the actor's.</summary>
    public const int KindGizmo = 11;

    /// <summary>More nodes than this is no marker list (21 in a seasonal dungeon, 2026-10-04).</summary>
    public const int MaxCount = 20000;

    // Head H: [H-8] the node pool, whose node size is at +0x20; [H] first, [H+8] last, [H+0x10] count.
    private const int PoolBeforeHead = 8, PoolNodeSizeOffset = 0x20, LastOffset = 8, CountOffset = 0x10, NodeSize = 0xA0;

    // Node: next, prev, then the fields below; every node of the list carries the same u32 twice (+0x10, +0x18: 19, then 21
    // after the list was rebuilt).
    private const int PrevOffset = 8, GenerationOffset = 0x10, GenerationCopyOffset = 0x18, SnoOffset = 0x1C, PositionOffset = 0x20,
        LevelAreaOffset = 0x38, KindOffset = 0x50;

    private const long MinPointer = 0x10000, MaxPointer = 0x7FF000000000;
    private static readonly int ScanThreads = Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>The list head, scanning only the reservation of the game object (one 883 MB region, about 2 s); null when none matches.</summary>
    public static long? Locate(IMemoryReader memory, long moduleBase, long gameObjectPointerRva)
    {
        if (!PoolContainer.TryReadU64(memory, moduleBase + gameObjectPointerRva, out ulong g) || g == 0) return null;
        var all = memory.Regions();
        if (all.FirstOrDefault(r => (long)g >= r.Address && (long)g < r.Address + r.Size) is not { Size: > 0 } home) return null;
        var candidates = new ConcurrentBag<long>();
        Parallel.ForEach(all.Where(r => r.AllocationBase == home.AllocationBase), new ParallelOptions { MaxDegreeOfParallelism = ScanThreads },
            () => new byte[RegionWindows.DefaultWindowBytes], (region, _, buffer) =>
            {
                foreach (var (address, window) in RegionWindows.ReadRegion(memory, region, buffer))
                    CollectCandidates(address, window.Span, candidates);
                return buffer;
            }, _ => { });
        return candidates.Distinct().Order().Cast<long?>().FirstOrDefault(h => IsHead(memory, h!.Value));
    }

    // The cheap part of the head shape, on raw bytes: 8-aligned pointers to first, last and the pool, and a plausible count.
    private static void CollectCandidates(long address, ReadOnlySpan<byte> span, ConcurrentBag<long> candidates)
    {
        for (int i = (int)((8 - address % 8) % 8) + PoolBeforeHead; i + CountOffset + 8 <= span.Length; i += 8)
        {
            long count = BinaryPrimitives.ReadInt64LittleEndian(span[(i + CountOffset)..]);
            if (count is < 1 or > MaxCount) continue;
            long first = BinaryPrimitives.ReadInt64LittleEndian(span[i..]), last = BinaryPrimitives.ReadInt64LittleEndian(span[(i + LastOffset)..]);
            long pool = BinaryPrimitives.ReadInt64LittleEndian(span[(i - PoolBeforeHead)..]);
            if (!IsPointer(first) || !IsPointer(last) || !IsPointer(pool) || (count == 1 && first != last)) continue;
            candidates.Add(address + i);
        }
    }

    /// <summary>True when <paramref name="head"/> still heads a marker list: its pool holds 0xA0-byte nodes and the ring closes.</summary>
    public static bool IsHead(IMemoryReader memory, long head) => TryRead(memory, head) is not null;

    /// <summary>Every marker, walking from the head; null when the head no longer validates (freed, or another list).</summary>
    public static IReadOnlyList<MapMarker>? TryRead(IMemoryReader memory, long head)
    {
        if (!PoolContainer.TryReadU64(memory, head - PoolBeforeHead, out ulong pool) || !PoolContainer.TryReadU32(memory, (long)pool + PoolNodeSizeOffset, out uint size)
            || size != NodeSize || !PoolContainer.TryReadU64(memory, head + CountOffset, out ulong count) || count is < 1 or > MaxCount
            || !PoolContainer.TryReadU64(memory, head, out ulong first) || !PoolContainer.TryReadU64(memory, (long)first + PrevOffset, out ulong back)
            || (long)back != head)
            return null;
        var markers = new List<MapMarker>((int)count);
        var node = new byte[NodeSize];
        long at = (long)first;
        uint? generation = null;
        while (at != head)
        {
            if (markers.Count >= (int)count || !memory.TryRead(at, node, NodeSize)) return null;
            var span = node.AsSpan();
            uint mark = BinaryPrimitives.ReadUInt32LittleEndian(span[GenerationOffset..]);
            if (mark != BinaryPrimitives.ReadUInt32LittleEndian(span[GenerationCopyOffset..]) || mark != (generation ??= mark)) return null;
            markers.Add(new MapMarker(
                BinaryPrimitives.ReadInt32LittleEndian(span[SnoOffset..]),
                new Vector3(BinaryPrimitives.ReadSingleLittleEndian(span[PositionOffset..]), BinaryPrimitives.ReadSingleLittleEndian(span[(PositionOffset + 4)..]),
                    BinaryPrimitives.ReadSingleLittleEndian(span[(PositionOffset + 8)..])),
                BinaryPrimitives.ReadInt32LittleEndian(span[LevelAreaOffset..]),
                BinaryPrimitives.ReadInt32LittleEndian(span[KindOffset..])));
            at = BinaryPrimitives.ReadInt64LittleEndian(span);
        }
        return markers.Count == (int)count ? markers : null;
    }

    private static bool IsPointer(long value) => value is >= MinPointer and < MaxPointer && (value & 7) == 0;
}
