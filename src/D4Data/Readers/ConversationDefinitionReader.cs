using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>Reads (and caches per SNO) ConversationDefinition.eConvType and whether an NPC speaks in it.</summary>
public sealed class ConversationDefinitionReader(SnoStore store, TypeSchema schema)
{
    private const string ConversationDefinition = "ConversationDefinition", TreeNode = "ConversationTreeNode", Speaker = "SpeakerDefinition";
    private const string RootNodes = "arRootNodes", ChildNodes = "arChildNodes", NodeType = "dwType", SpeakerSno = "snoSpeaker";

    /// <summary>eConvType of the standard NPC conversations and in-engine story scenes (census of ConversationDefinition.eConvType, 2026-09-29; see research/client-memory.md §5).</summary>
    public const int StandardConversationType = 3;

    /// <summary>eConvType of hero lines, town gossip and the lines an NPC says when talked to without a dialogue menu (same census).</summary>
    public const int LineConversationType = 1;

    // The hero's Speaker SNO: the 502 hero-only type-1 conversations (HRO_*, *_Player_*) use only it (speaker census, 2026-09-30).
    private const string HeroSpeakerName = "Player";

    // A malformed tree stops here instead of recursing without end.
    private const int MaxTreeDepth = 32;

    private readonly Dictionary<int, Conversation?> _cache = [];
    private int? _heroSpeaker;

    /// <summary>Null when the SNO has no readable ConversationDefinition record.</summary>
    public int? ReadType(int conversationSno) => Read(conversationSno)?.Type;

    public bool IsStandard(int conversationSno) => ReadType(conversationSno) == StandardConversationType;

    /// <summary>An NPC talks to the player: a standard conversation, or a line conversation with a non-hero speaker.</summary>
    public bool IsNpcTalk(int conversationSno) => Read(conversationSno) is { } c
        && (c.Type == StandardConversationType || c.Type == LineConversationType && c.NpcSpeaks);

    private Conversation? Read(int sno)
    {
        if (_cache.TryGetValue(sno, out var cached)) return cached;
        var meta = store.Meta(schema.SnoGroup(ConversationDefinition), sno);
        Conversation? conversation = null;
        if (meta is not null && meta.Length >= schema.Size(ConversationDefinition))
        {
            int type = (int)meta.U32(schema.Offset(ConversationDefinition, "eConvType"));
            conversation = new Conversation(type, type == LineConversationType && NpcSpeaks(meta));
        }
        _cache[sno] = conversation;
        return conversation;
    }

    /// <summary>Walks the node tree for a line whose speaker is not the hero; an unreadable tree counts as no NPC.</summary>
    private bool NpcSpeaks(SnoBlob meta)
    {
        _heroSpeaker ??= store.Resolve(schema.SnoGroup(Speaker), HeroSpeakerName);
        var arrays = new Stack<(int Offset, int Depth)>();
        arrays.Push((schema.Offset(ConversationDefinition, RootNodes), 0));
        while (arrays.TryPop(out var array))
        {
            if (array.Depth > MaxTreeDepth || meta.PolymorphicElements(array.Offset, node => NodeTypeAt(meta, node) is { } t ? schema.Size(t) : null) is not { } nodes)
                return false;
            foreach (int node in nodes)
            {
                string type = NodeTypeAt(meta, node)!;
                if (schema.HasField(type, SpeakerSno) && meta.I32(node + schema.Offset(type, SpeakerSno)) is > 0 and var speaker && speaker != _heroSpeaker)
                    return true;
                if (schema.HasField(type, ChildNodes)) arrays.Push((node + schema.Offset(type, ChildNodes), array.Depth + 1));
            }
        }
        return false;
    }

    // A node's dwType is its type's hash in definitions.json.
    private string? NodeTypeAt(SnoBlob meta, int node)
    {
        int typeOffset = node + schema.Offset(TreeNode, NodeType);
        return typeOffset + sizeof(uint) <= meta.Length ? schema.TypeName(meta.U32(typeOffset)) : null;
    }

    private sealed record Conversation(int Type, bool NpcSpeaks);
}
