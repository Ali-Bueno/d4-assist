using System.Buffers.Binary;
using D4Memory.Scan;

namespace D4Memory.Ui;

/// <summary>Where the conversation state lives in one build (research/client-memory.md §5, live 2026-09-29).</summary>
/// <param name="OptionsPointerRva">u64 pointer to the dialogue menu's option array; the u32 option count follows it (0 = menu closed).</param>
/// <param name="OptionStride">Bytes per option.</param>
/// <param name="OptionSnoOffset">Offset of an option's ConversationDefinition SNO.</param>
/// <param name="PlayingListRva">Head of the doubly linked list of playing conversations (next, prev); it points to itself when empty.</param>
/// <param name="NodeSnoOffset">Offset of a playing node's ConversationDefinition SNO.</param>
public sealed record ConversationOffsets(long OptionsPointerRva, int OptionStride, int OptionSnoOffset, long PlayingListRva, int NodeSnoOffset);

/// <summary>The dialogue menu's offered conversations (empty when the menu is closed) and the conversations playing now.</summary>
public sealed record ConversationSnapshot(IReadOnlyList<int> MenuOptions, IReadOnlyList<int> Playing);

/// <summary>Reads the dialogue menu and the playing-conversation list through static module addresses.</summary>
public static class ConversationReader
{
    // Guards against a torn or foreign read: the prologue menu offered 5 options; a list walk stops after this many nodes.
    private const int MaxOptions = 32, MaxPlaying = 16;

    public static ConversationSnapshot? TryRead(IMemoryReader memory, long moduleBase, ConversationOffsets offsets)
    {
        var header = new byte[sizeof(long) + sizeof(uint)];
        if (!memory.TryRead(moduleBase + offsets.OptionsPointerRva, header, header.Length)) return null;
        long options = BinaryPrimitives.ReadInt64LittleEndian(header);
        int count = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(sizeof(long))), MaxOptions);

        var menu = new List<int>();
        var word = new byte[sizeof(long)];
        for (int i = 0; i < count; i++)
            if (memory.TryRead(options + i * offsets.OptionStride + offsets.OptionSnoOffset, word, sizeof(int)))
                menu.Add(BinaryPrimitives.ReadInt32LittleEndian(word));

        var playing = new List<int>();
        long head = moduleBase + offsets.PlayingListRva, node = head;
        for (int i = 0; i < MaxPlaying; i++)
        {
            if (!memory.TryRead(node, word, sizeof(long))) return null;
            node = BinaryPrimitives.ReadInt64LittleEndian(word);
            if (node == head) break;
            if (!memory.TryRead(node + offsets.NodeSnoOffset, word, sizeof(int))) return null;
            playing.Add(BinaryPrimitives.ReadInt32LittleEndian(word));
        }
        return new ConversationSnapshot(menu, playing);
    }
}
