using System.Buffers.Binary;
using System.Numerics;
using D4Memory.Pools;
using D4Memory.Scan;
using D4Memory.SceneRoute;

namespace D4Memory.Actors;

/// <summary>One live RActor slot (research/client-memory.md §4); <see cref="Placement"/> 0 means placed in the world.</summary>
/// <param name="PreviousState">The gizmo state id before the current one; 0 until a gizmo is used once.</param>
public readonly record struct RActorRecord(
    long SlotAddress, uint Id, uint AcdId, int ActorSno, Vector3 Position, Quaternion Rotation,
    uint WorldId, uint SceneId, int Placement, uint PreviousState = 0)
{
    /// <summary>AcdId of an actor with no game record (client-only props).</summary>
    public const uint NoAcd = uint.MaxValue;

    public bool IsPlaced => Placement == RActorReader.PlacedValue;
}

/// <summary>Walks the RActor pool through the static route; layout in research/client-memory.md §4 (offline RE).</summary>
public static class RActorReader
{
    /// <summary>Placement flag of an actor placed in the world; UI/paper-doll copies hold -1.</summary>
    public const int PlacedValue = 0;

    /// <summary>Reads every occupied, readable RActor slot. False when the pool route is unavailable; never throws.</summary>
    public static bool TryRead(
        IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, out IReadOnlyList<RActorRecord> actors)
    {
        actors = [];
        if (!PoolContainer.TryOpen(reader, moduleBase, offsets, offsets.Actor, out var pool)) return false;

        // The fields come from the layout (research/client-memory.md §4), checked live by LayoutDiscovery.
        var fields = offsets.ActorFields;
        var records = new List<RActorRecord>();
        var buffer = new byte[fields.RecordBytes];
        foreach (var (slot, id) in pool.OccupiedSlots())
        {
            if (!reader.TryRead(slot, buffer, buffer.Length)) continue;
            records.Add(Parse(buffer, slot, id, fields));
        }
        actors = records;
        return true;
    }

    private static RActorRecord Parse(ReadOnlySpan<byte> slot, long address, uint id, RActorFields f) => new(
        address, id,
        AcdId: U32(slot, f.AcdId),
        ActorSno: I32(slot, f.ActorSno),
        Position: new Vector3(F32(slot, f.Position), F32(slot, f.Position + 4), F32(slot, f.Position + 8)),
        Rotation: new Quaternion(F32(slot, f.Rotation), F32(slot, f.Rotation + 4), F32(slot, f.Rotation + 8), F32(slot, f.Rotation + 12)),
        WorldId: U32(slot, f.WorldId),
        SceneId: U32(slot, f.SceneId),
        Placement: I32(slot, f.Placement),
        PreviousState: U32(slot, f.PreviousState));

    private static uint U32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(o, 4));
    private static int I32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o, 4));
    private static float F32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadSingleLittleEndian(b.Slice(o, 4));
}
