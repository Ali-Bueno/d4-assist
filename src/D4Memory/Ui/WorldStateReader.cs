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
    // [[[G + 0xA58] + 0x28]] → the table: first node at +0x00, node count (u32) at +0x10, node size (u32) at +0x14, end at +0x30;
    // 16-byte nodes {next, u32 SNO, i32 value} back to back (live 2026-10-27, build 3.2.3.73883: 540 nodes, ~500 states).
    private const int OwnerOffset = 0xA58, TableOffset = 0x28;
    private const int FirstOffset = 0x00, CountOffset = 0x10, NodeSizeOffset = 0x14, EndOffset = 0x30;
    private const int NodeSize = 16, SnoInNode = 8, ValueInNode = 12, MaxNodes = 16_384;

    /// <summary>Null when the table is unreadable or its header is no world-state table.</summary>
    public static IReadOnlyDictionary<int, int>? Read(IMemoryReader memory, long moduleBase, long gameObjectPointerRva)
    {
        if (!PoolContainer.TryReadU64(memory, moduleBase + gameObjectPointerRva, out ulong g) || g == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)g + OwnerOffset, out ulong owner) || owner == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)owner + TableOffset, out ulong holder) || holder == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)holder, out ulong table) || table == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)table + FirstOffset, out ulong first) ||
            !PoolContainer.TryReadU64(memory, (long)table + EndOffset, out ulong end) ||
            !PoolContainer.TryReadU32(memory, (long)table + CountOffset, out uint count) ||
            !PoolContainer.TryReadU32(memory, (long)table + NodeSizeOffset, out uint nodeSize)) return null;
        if (nodeSize != NodeSize || first == 0 || end <= first || count is 0 or > MaxNodes || (end - first) != count * NodeSize) return null;

        var bytes = new byte[(int)count * NodeSize];
        if (!memory.TryRead((long)first, bytes, bytes.Length)) return null;
        var states = new Dictionary<int, int>();
        for (int node = 0; node < bytes.Length; node += NodeSize)
        {
            int sno = BitConverter.ToInt32(bytes, node + SnoInNode);
            if (sno > 0) states[sno] = BitConverter.ToInt32(bytes, node + ValueInNode);
        }
        return states;
    }
}
