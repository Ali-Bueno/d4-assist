using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.Ui;

/// <summary>
/// Reads the game session's world states, {WorldState SNO → value} (research/map-points.md §4): the server's world-wide flags,
/// such as the helltide's region (UberSubzone). The table belongs to the session: logging in again builds a new one elsewhere,
/// so it is found anew on each read through the game object.
/// </summary>
public static class WorldStateReader
{
    // [G + 0xA58] → the node pool: its blocks' holders at [+0x28], 0x18 bytes apart, their count (u32) at +0x30, the live states'
    // count (u32) at +0x18. Each holder's first word → a block: first node at +0x00, node count (u32) at +0x10, node size (u32)
    // at +0x14, end at +0x30; 16-byte nodes {next, u32 SNO, i32 value} back to back. Live 2026-10-27 (build 3.2.3.73883) the
    // first block alone held all ~500 states; 2026-10-28 five blocks of 60, 20, 10, 540 and 180 nodes held 775, UberSubzone in
    // the last only, and the first block alone read the expansions' helltide without the base game's.
    private const int OwnerOffset = 0xA58, HoldersOffset = 0x28, HolderCountOffset = 0x30, HolderStride = 0x18, MaxHolders = 64;
    private const int FirstOffset = 0x00, CountOffset = 0x10, NodeSizeOffset = 0x14, EndOffset = 0x30;
    private const int NodeSize = 16, SnoInNode = 8, ValueInNode = 12, MaxNodes = 16_384;

    /// <summary>Null when the pool is unreadable or none of its blocks is a world-state block.</summary>
    /// <param name="conflicting">Gets each state that two blocks hold with different values (2026-10-28: 35 states were held
    /// twice, each time with the same value); the later block's value is taken.</param>
    public static IReadOnlyDictionary<int, int>? Read(IMemoryReader memory, long moduleBase, long gameObjectPointerRva,
        ICollection<int>? conflicting = null)
    {
        if (!PoolContainer.TryReadU64(memory, moduleBase + gameObjectPointerRva, out ulong g) || g == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)g + OwnerOffset, out ulong owner) || owner == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)owner + HoldersOffset, out ulong holders) || holders == 0) return null;
        // An unreadable or implausible count reads the first block alone.
        int holderCount = PoolContainer.TryReadU32(memory, (long)owner + HolderCountOffset, out uint n) && n is > 0 and <= MaxHolders ? (int)n : 1;

        Dictionary<int, int>? states = null;
        for (int h = 0; h < holderCount; h++)
        {
            if (!PoolContainer.TryReadU64(memory, (long)holders + h * HolderStride, out ulong block) || block == 0) continue;
            if (ReadBlock(memory, (long)block) is not { } bytes) continue;
            states ??= [];
            for (int node = 0; node < bytes.Length; node += NodeSize)
            {
                int sno = BitConverter.ToInt32(bytes, node + SnoInNode);
                if (sno <= 0) continue;
                int value = BitConverter.ToInt32(bytes, node + ValueInNode);
                if (states.TryGetValue(sno, out int held) && held != value) conflicting?.Add(sno);
                states[sno] = value;
            }
        }
        return states;
    }

    private static byte[]? ReadBlock(IMemoryReader memory, long block)
    {
        if (!PoolContainer.TryReadU64(memory, block + FirstOffset, out ulong first) ||
            !PoolContainer.TryReadU64(memory, block + EndOffset, out ulong end) ||
            !PoolContainer.TryReadU32(memory, block + CountOffset, out uint count) ||
            !PoolContainer.TryReadU32(memory, block + NodeSizeOffset, out uint nodeSize)) return null;
        if (nodeSize != NodeSize || first == 0 || end <= first || count is 0 or > MaxNodes || (end - first) != count * NodeSize) return null;
        var bytes = new byte[(int)count * NodeSize];
        return memory.TryRead((long)first, bytes, bytes.Length) ? bytes : null;
    }
}
