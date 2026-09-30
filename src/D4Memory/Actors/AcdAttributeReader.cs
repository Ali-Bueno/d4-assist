using System.Buffers.Binary;
using System.Globalization;
using D4Memory.Scan;

namespace D4Memory.Actors;

/// <summary>One ACD attribute: its key (attribute id + parameter, -1 = none) and its value.</summary>
/// <param name="Kind">0 = float, 1 = integer (live 2026-09-30); anything else prints raw.</param>
/// <param name="Map">Index of the container map holding it (0 holds most; the others' roles are unknown).</param>
public readonly record struct AcdAttribute(uint AttributeId, int Parameter, uint Kind, uint RawValue, int Map)
{
    public const uint FloatKind = 0, IntegerKind = 1;
    public const int NoParameter = -1;

    public string FormattedValue => Kind switch
    {
        FloatKind => BitConverter.UInt32BitsToSingle(RawValue).ToString("G6", CultureInfo.InvariantCulture),
        IntegerKind => ((int)RawValue).ToString(CultureInfo.InvariantCulture),
        _ => $"0x{RawValue:X}",
    };
}

/// <summary>Walks an ACD attribute container: four chained hash maps of attribute nodes (research/client-memory.md §4).</summary>
public static class AcdAttributeReader
{
    // Map headers {u64 bucket count, u64 bucket array, u64 entry count} at these container offsets.
    private static readonly int[] MapOffsets = [0x08, 0x38, 0xD8, 0x108];
    private const int BucketArrayOffset = 0x8, EntryCountOffset = 0x10, MapHeaderBytes = 0x18;

    // A bucket points at a node's next-link word; the key sits 8 bytes before it, kind and value 8 and 12 bytes after.
    private const int NodeKeyBefore = 0x8, NodeBytes = 0x18;
    private const int NodeParamOffset = 0x4, NodeNextOffset = 0x8, NodeKindOffset = 0x10, NodeValueOffset = 0x14;

    // Bounds against a torn or freed container: gizmo maps had 8-64 buckets, the player's largest 512 buckets and 337 entries.
    private const long MaxBuckets = 1 << 16, MaxEntries = 1 << 14;

    /// <summary>Every readable attribute; a map whose header is implausible is skipped.</summary>
    public static IReadOnlyList<AcdAttribute> Read(IMemoryReader reader, long container)
    {
        var attributes = new List<AcdAttribute>();
        var header = new byte[MapHeaderBytes];
        var node = new byte[NodeBytes];
        for (int map = 0; map < MapOffsets.Length; map++)
        {
            if (!reader.TryRead(container + MapOffsets[map], header, header.Length)) continue;
            long bucketCount = BinaryPrimitives.ReadInt64LittleEndian(header);
            long buckets = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(BucketArrayOffset));
            long entries = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(EntryCountOffset));
            if (bucketCount is <= 0 or > MaxBuckets || entries is <= 0 or > MaxEntries) continue;

            var bucketArray = new byte[bucketCount * sizeof(long)];
            if (!reader.TryRead(buckets, bucketArray, bucketArray.Length)) continue;
            int walked = 0;
            for (int b = 0; b < bucketCount && walked < entries; b++)
            {
                long link = BinaryPrimitives.ReadInt64LittleEndian(bucketArray.AsSpan(b * sizeof(long)));
                while (link != 0 && walked < entries && reader.TryRead(link - NodeKeyBefore, node, node.Length))
                {
                    attributes.Add(new AcdAttribute(
                        AttributeId: BinaryPrimitives.ReadUInt32LittleEndian(node),
                        Parameter: BinaryPrimitives.ReadInt32LittleEndian(node.AsSpan(NodeParamOffset)),
                        Kind: BinaryPrimitives.ReadUInt32LittleEndian(node.AsSpan(NodeKindOffset)),
                        RawValue: BinaryPrimitives.ReadUInt32LittleEndian(node.AsSpan(NodeValueOffset)),
                        Map: map));
                    walked++;
                    link = BinaryPrimitives.ReadInt64LittleEndian(node.AsSpan(NodeNextOffset));
                }
            }
        }
        return attributes;
    }
}
