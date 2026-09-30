using System.Buffers.Binary;
using System.Text;
using D4Memory.Scan;
using D4Memory.SceneRoute;

namespace D4Memory.Actors;

/// <summary>Attribute id → name through the game's live descriptor table (research/client-memory.md §4); caches each lookup.</summary>
public sealed class AttributeNameTable(IMemoryReader reader, long moduleBase, AcdOffsets offsets)
{
    // The longest name in the table has 62 characters (live 2026-09-30); a read without a NUL is treated as garbage.
    private const int MaxNameBytes = 96;

    private readonly Dictionary<uint, string?> _cache = [];

    /// <summary>The attribute's name, or null when its descriptor doesn't carry that id (unknown or unreadable).</summary>
    public string? NameOf(uint attributeId)
    {
        if (_cache.TryGetValue(attributeId, out var cached)) return cached;
        return _cache[attributeId] = Read(attributeId);
    }

    private string? Read(uint attributeId)
    {
        long entry = moduleBase + offsets.AttributeTableRva + (long)attributeId * offsets.AttributeStride;
        var word = new byte[sizeof(long)];
        if (!reader.TryRead(entry, word, sizeof(uint)) || BinaryPrimitives.ReadUInt32LittleEndian(word) != attributeId) return null;
        if (!reader.TryRead(entry + offsets.AttributeNameOffset, word, sizeof(long))) return null;

        var name = new byte[MaxNameBytes];
        if (!reader.TryRead(BinaryPrimitives.ReadInt64LittleEndian(word), name, name.Length)) return null;
        int length = Array.IndexOf(name, (byte)0);
        return length > 0 ? Encoding.ASCII.GetString(name, 0, length) : null;
    }
}
