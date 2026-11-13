using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.Actors;

/// <summary>
/// Reads the quests the local character has completed: a hash set of quest SNOs behind the game's player block
/// (research/client-memory.md §4, completed quests). The game hides a freed stronghold's map icon by it.
/// </summary>
public static class CompletedQuestsReader
{
    // [[[G + 0xA80] + 0xA8] + 0] → the set's buffer: capacity (u32) at +0x10, entry size (u32, 16) at +0x14, entries from +0x50,
    // each a chain pointer (or 0) and the quest SNO at +8 (live 2026-11-13, build 3.2.3: 335 entries of 512 on a character that
    // had freed six strongholds, all six among them and no other of the 21; a second character's set, at another buffer, held
    // 296 quests and no stronghold; the stronghold the user then freed was absent before and present after).
    private const int PlayerBlockOffset = 0xA80, ContainerOffset = 0xA8, CapacityOffset = 0x10, EntrySizeOffset = 0x14,
        EntriesOffset = 0x50, EntrySize = 16, SnoInEntry = 8;

    /// <summary>A larger capacity is no quest set.</summary>
    public const int MaxCapacity = 1 << 16;

    /// <summary>The quest SNOs of the local character's completed quests; null when the set is unreadable or has no set's shape.</summary>
    public static IReadOnlySet<int>? Read(IMemoryReader memory, long moduleBase, long gameObjectPointerRva)
    {
        if (!PoolContainer.TryReadU64(memory, moduleBase + gameObjectPointerRva, out ulong g) || g == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)g + PlayerBlockOffset, out ulong block) || block == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)block + ContainerOffset, out ulong container) || container == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)container, out ulong buffer) || buffer == 0) return null;
        if (!PoolContainer.TryReadU32(memory, (long)buffer + CapacityOffset, out uint capacity) || capacity is 0 or > MaxCapacity) return null;
        if (!PoolContainer.TryReadU32(memory, (long)buffer + EntrySizeOffset, out uint entrySize) || entrySize != EntrySize) return null;
        var entries = new byte[capacity * EntrySize];
        if (!memory.TryRead((long)buffer + EntriesOffset, entries, entries.Length)) return null;
        var quests = new HashSet<int>();
        for (int i = 0; i < capacity; i++)
        {
            int sno = BitConverter.ToInt32(entries, i * EntrySize + SnoInEntry);
            if (sno is not (0 or -1)) quests.Add(sno);
        }
        return quests;
    }
}
