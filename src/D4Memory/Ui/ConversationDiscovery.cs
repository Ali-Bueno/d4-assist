using System.Buffers.Binary;
using D4Memory.Scan;

namespace D4Memory.Ui;

/// <summary>
/// Finds the conversation addresses (research/client-memory.md §5) in the module's writable data by what they hold, so a
/// game update needs no new constants. Each search only succeeds while its state is live: the playing list while a
/// conversation plays, the dialogue menu while it is open. A search reads every pointer in the data once (about a second).
/// </summary>
public static class ConversationDiscovery
{
    private const ulong MinPointer = 0x10000, MaxPointer = 0x7FFF_FFFF_0000;
    private const int PrevOffset = 8; // a node: next, prev
    private const int MaxOptions = 16;

    /// <summary>The list head H: a data word whose first node links back to H and holds a conversation SNO.</summary>
    public static long? FindPlayingList(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> data, ModuleRange module, ConversationOffsets layout, Func<int, bool> isConversation)
    {
        var node = new byte[layout.NodeSnoOffset + sizeof(int)];
        foreach (var (head, first, _) in Words(memory, data, module))
        {
            if (first == (ulong)head || !memory.TryRead((long)first, node, node.Length)) continue;
            if (BinaryPrimitives.ReadInt64LittleEndian(node.AsSpan(PrevOffset)) != head) continue;
            if (isConversation(BinaryPrimitives.ReadInt32LittleEndian(node.AsSpan(layout.NodeSnoOffset)))) return head;
        }
        return null;
    }

    /// <summary>The dialogue menu: a pointer to the option array followed by a u32 count, every option a conversation (or 0).</summary>
    public static long? FindMenuOptions(
        IMemoryReader memory, IReadOnlyList<MemoryRegion> data, ModuleRange module, ConversationOffsets layout, Func<int, bool> isConversation)
    {
        foreach (var (at, array, count) in Words(memory, data, module))
        {
            if (count is 0 or > MaxOptions) continue;
            var options = new byte[(int)count * layout.OptionStride];
            if (!memory.TryRead((long)array, options, options.Length)) continue;
            int conversations = 0;
            bool others = false;
            for (int i = 0; i < count && !others; i++)
            {
                int sno = BinaryPrimitives.ReadInt32LittleEndian(options.AsSpan(i * layout.OptionStride + layout.OptionSnoOffset));
                if (sno == 0) continue;
                if (isConversation(sno)) conversations++; else others = true;
            }
            if (!others && conversations > 0) return at;
        }
        return null;
    }

    // Every 8-aligned heap-looking u64 in the data, with the u32 after it.
    private static IEnumerable<(long At, ulong Value, uint Next)> Words(IMemoryReader memory, IReadOnlyList<MemoryRegion> data, ModuleRange module)
    {
        var buffer = new byte[RegionWindows.DefaultWindowBytes];
        foreach (var region in data)
            foreach (var (address, window) in RegionWindows.ReadRegion(memory, region, buffer))
            {
                // The window's bytes are copied per hit: the buffer is reused once this window is done.
                var bytes = window.ToArray();
                for (int i = (int)((8 - address % 8) % 8); i + 12 <= bytes.Length; i += 8)
                {
                    ulong value = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(i));
                    if (value is < MinPointer or > MaxPointer || module.Contains((long)value)) continue;
                    yield return (address + i, value, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i + 8)));
                }
            }
    }
}
