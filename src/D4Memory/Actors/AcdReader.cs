using System.Buffers.Binary;
using System.Numerics;
using D4Memory.Pools;
using D4Memory.Scan;
using D4Memory.SceneRoute;

namespace D4Memory.Actors;

/// <summary>One live ACD slot, the actor's game record (research/client-memory.md §4).</summary>
/// <param name="AttributesAddress">The actor's attribute container (read with <see cref="AcdAttributeReader"/>).</param>
public readonly record struct AcdRecord(long SlotAddress, uint Id, uint RActorId, int ActorSno, Vector3 Position, long AttributesAddress);

/// <summary>Reads ACD slots through the pool route `[[G+0xAB0]]` (research/client-memory.md §4, live 2026-09-30).</summary>
public sealed class AcdReader
{
    private readonly IMemoryReader _reader;
    private readonly long _moduleBase;
    private readonly GameBuildOffsets _offsets;
    private volatile PoolContainer _pool;

    private AcdReader(IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, PoolContainer pool) =>
        (_reader, _moduleBase, _offsets, _pool) = (reader, moduleBase, offsets, pool);

    /// <summary>Byte distance between slots; a slot holds this many bytes.</summary>
    public long Stride => _pool.Stride;

    /// <summary>False when the build has no ACD offsets or the pool header is unreadable; never throws.</summary>
    public static bool TryOpen(IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, out AcdReader acds)
    {
        acds = null!;
        if (offsets.Acd is not { } acd || !PoolContainer.TryOpen(reader, moduleBase, offsets, acd.Pool, out var pool)) return false;
        acds = new AcdReader(reader, moduleBase, offsets, pool);
        return true;
    }

    /// <summary>Re-reads the pool header, keeping the last one when unreadable: the pool grows as actors load, and a slot
    /// past the size read before is never found (live 2026-09-30: a dungeon door's flags never arrived).</summary>
    public void Refresh()
    {
        if (PoolContainer.TryOpen(_reader, _moduleBase, _offsets, _offsets.Acd!.Pool, out var pool)) _pool = pool;
    }

    /// <summary>The slot holding <paramref name="acdId"/> now (an RActor's +0x0C), or false when it is freed.</summary>
    public bool TryGet(uint acdId, out AcdRecord record)
    {
        record = default;
        return _pool.TryGetSlotById(acdId, out long slot) && TryRead(slot, acdId, out record);
    }

    /// <summary>Every occupied, readable slot.</summary>
    public IEnumerable<AcdRecord> All()
    {
        foreach (var (slot, id) in _pool.OccupiedSlots())
            if (TryRead(slot, id, out var record)) yield return record;
    }

    private bool TryRead(long slot, uint id, out AcdRecord record)
    {
        record = default;
        var f = _offsets.Acd!.Fields; // from the layout (research/client-memory.md §4), checked live by LayoutDiscovery
        var buffer = new byte[f.RecordBytes];
        if (!_reader.TryRead(slot, buffer, buffer.Length)) return false;
        record = new AcdRecord(slot, id,
            RActorId: BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(f.RActorId)),
            ActorSno: BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(f.ActorSno)),
            Position: new Vector3(F32(buffer, f.Position), F32(buffer, f.Position + 4), F32(buffer, f.Position + 8)),
            AttributesAddress: BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(f.Attributes)));
        return true;
    }

    private static float F32(byte[] b, int o) => BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(o));
}
