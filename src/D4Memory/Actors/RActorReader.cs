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
    public bool IsPlaced => Placement == RActorReader.PlacedValue;
}

/// <summary>Walks the RActor pool through the static route; layout in research/client-memory.md §4 (offline RE).</summary>
public static class RActorReader
{
    /// <summary>Placement flag of an actor placed in the world; UI/paper-doll copies hold -1.</summary>
    public const int PlacedValue = 0;

    // Field offsets from the slot start (research/client-memory.md §4, RActor slot table).
    private const int AcdIdOffset = 0x0C;
    private const int PreviousStateOffset = 0x148;
    private const int ActorSnoOffset = 0x5B8;
    private const int QuaternionOffset = 0x750;
    private const int PositionOffset = 0x760;
    private const int WorldIdOffset = 0x7A4;
    private const int SceneIdOffset = 0x7A8;
    private const int PlacementOffset = 0x7AC;

    /// <summary>Bytes read per slot: everything up to and including the placement field.</summary>
    private const int RecordBytes = PlacementOffset + sizeof(int);

    /// <summary>Reads every occupied, readable RActor slot. False when the pool route is unavailable; never throws.</summary>
    public static bool TryRead(
        IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, out IReadOnlyList<RActorRecord> actors)
    {
        actors = [];
        if (!PoolContainer.TryOpen(reader, moduleBase, offsets, offsets.Actor, out var pool)) return false;

        var records = new List<RActorRecord>();
        var buffer = new byte[RecordBytes];
        foreach (var (slot, id) in pool.OccupiedSlots())
        {
            if (!reader.TryRead(slot, buffer, buffer.Length)) continue;
            records.Add(Parse(buffer, slot, id));
        }
        actors = records;
        return true;
    }

    private static RActorRecord Parse(ReadOnlySpan<byte> slot, long address, uint id) => new(
        address, id,
        AcdId: U32(slot, AcdIdOffset),
        ActorSno: I32(slot, ActorSnoOffset),
        Position: new Vector3(F32(slot, PositionOffset), F32(slot, PositionOffset + 4), F32(slot, PositionOffset + 8)),
        Rotation: new Quaternion(
            F32(slot, QuaternionOffset), F32(slot, QuaternionOffset + 4), F32(slot, QuaternionOffset + 8), F32(slot, QuaternionOffset + 12)),
        WorldId: U32(slot, WorldIdOffset),
        SceneId: U32(slot, SceneIdOffset),
        Placement: I32(slot, PlacementOffset),
        PreviousState: U32(slot, PreviousStateOffset));

    private static uint U32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(o, 4));
    private static int I32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadInt32LittleEndian(b.Slice(o, 4));
    private static float F32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadSingleLittleEndian(b.Slice(o, 4));
}
