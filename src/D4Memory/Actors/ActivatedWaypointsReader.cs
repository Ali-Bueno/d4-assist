using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.Actors;

/// <summary>
/// Reads the waypoints the local player has activated: <c>PlayerSavedData.arWaypointsActivated</c> in the local player's slot
/// of the game's player block (research/client-memory.md §4, activated waypoints).
/// </summary>
public static class ActivatedWaypointsReader
{
    // [G + 0xA80] → player block; four slots from +0x1F8, 0xF520 apart, each with its u32 index at +0 and its PlayerSavedData
    // at +0xE0 (live 2026-10-18, build 3.2.3.73883).
    private const int PlayerBlockOffset = 0xA80, FirstSlotOffset = 0x1F8, SlotStride = 0xF520, SavedDataOffset = 0xE0;

    /// <summary>Player slots in a game.</summary>
    public const int SlotCount = 4;

    // PlayerSavedData: tHotbarBtnAssignments (0x60 bytes) at +0, arWaypointsActivated (64 × u32 LevelArea SNO) at +0x78.
    private const int HotbarBytes = 0x60, WaypointsOffset = 0x78;

    /// <summary>Slots in arWaypointsActivated.</summary>
    public const int WaypointSlots = 64;

    private const uint Unassigned = uint.MaxValue;

    /// <summary>
    /// The LevelArea SNOs of the local player's activated waypoints, in activation order; null when the player block is
    /// unreadable or no slot is the local player's. The list ends at the first word that is no <paramref name="waypointLevelAreas"/>
    /// entry: the local slot held other data right after its last waypoint (live), so nothing else is ever read as one.
    /// </summary>
    public static IReadOnlyList<int>? Read(IMemoryReader memory, long moduleBase, long gameObjectPointerRva, IReadOnlySet<int> waypointLevelAreas)
    {
        if (LocalSlot(memory, moduleBase, gameObjectPointerRva) is not { } slot) return null;
        var words = new byte[WaypointSlots * sizeof(uint)];
        if (!memory.TryRead(slot + SavedDataOffset + WaypointsOffset, words, words.Length)) return null;
        var activated = new List<int>();
        for (int w = 0; w < WaypointSlots; w++)
        {
            int levelArea = BitConverter.ToInt32(words, w * sizeof(uint));
            if (!waypointLevelAreas.Contains(levelArea)) break;
            activated.Add(levelArea);
        }
        return activated;
    }

    /// <summary>The address of the local player's slot in the player block; null when the block is unreadable or no slot is
    /// the local player's.</summary>
    public static long? LocalSlot(IMemoryReader memory, long moduleBase, long gameObjectPointerRva)
    {
        if (!PoolContainer.TryReadU64(memory, moduleBase + gameObjectPointerRva, out ulong g) || g == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)g + PlayerBlockOffset, out ulong block) || block == 0) return null;
        for (int i = 0; i < SlotCount; i++)
        {
            long slot = (long)block + FirstSlotOffset + (long)i * SlotStride;
            if (!PoolContainer.TryReadU32(memory, slot, out uint index) || index != i) return null; // not the player block
            if (IsLocal(memory, slot + SavedDataOffset)) return slot;
        }
        return null;
    }

    // Only the local player's slot carries its hotbar (power SNOs); the other players' slots read Unassigned there (live: two
    // other players in Temis, research/client-memory.md §4).
    private static bool IsLocal(IMemoryReader memory, long savedData)
    {
        var hotbar = new byte[HotbarBytes];
        if (!memory.TryRead(savedData, hotbar, hotbar.Length)) return false;
        for (int o = 0; o < HotbarBytes; o += sizeof(uint))
        {
            uint word = BitConverter.ToUInt32(hotbar, o);
            if (word != Unassigned && word != 0) return true;
        }
        return false;
    }
}
