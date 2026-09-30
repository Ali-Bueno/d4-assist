using System.Buffers.Binary;
using D4Memory.Actors;

namespace D4Memory.Tests;

/// <summary>The attribute container layout read live on 2026-09-30 (research/client-memory.md §4), built by hand.</summary>
public class AcdAttributeReaderTests
{
    private const long Container = 0x1000, Buckets = 0x2000, Nodes = 0x3000;
    private const uint OperatedId = 0x229, StateId = 0x21E;

    // Map 0: two buckets, the first chaining two nodes, the second one node. A node's bucket link points 8 bytes into it.
    private static FakeMemoryReader Memory()
    {
        var container = new byte[0x200];
        WriteMap(container, 0x08, bucketCount: 2, buckets: Buckets, entries: 3);
        var buckets = new byte[16];
        BinaryPrimitives.WriteInt64LittleEndian(buckets, Nodes + 8);
        BinaryPrimitives.WriteInt64LittleEndian(buckets.AsSpan(8), Nodes + 0x40 + 8);
        var nodes = new byte[0x80];
        WriteNode(nodes, 0x00, OperatedId, parameter: -1, next: Nodes + 0x20 + 8, kind: 1, value: 1);
        WriteNode(nodes, 0x20, StateId, parameter: -1, next: 0, kind: 1, value: 1);
        WriteNode(nodes, 0x40, 0x100, parameter: 3, next: 0, kind: 0, value: BitConverter.SingleToUInt32Bits(2.5f));

        var memory = new FakeMemoryReader();
        memory.AddRegion(Container, container);
        memory.AddRegion(Buckets, buckets);
        memory.AddRegion(Nodes, nodes);
        return memory;
    }

    private static void WriteMap(byte[] container, int offset, long bucketCount, long buckets, long entries)
    {
        BinaryPrimitives.WriteInt64LittleEndian(container.AsSpan(offset), bucketCount);
        BinaryPrimitives.WriteInt64LittleEndian(container.AsSpan(offset + 8), buckets);
        BinaryPrimitives.WriteInt64LittleEndian(container.AsSpan(offset + 16), entries);
    }

    private static void WriteNode(byte[] nodes, int at, uint id, int parameter, long next, uint kind, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(nodes.AsSpan(at), id);
        BinaryPrimitives.WriteInt32LittleEndian(nodes.AsSpan(at + 4), parameter);
        BinaryPrimitives.WriteInt64LittleEndian(nodes.AsSpan(at + 8), next);
        BinaryPrimitives.WriteUInt32LittleEndian(nodes.AsSpan(at + 0x10), kind);
        BinaryPrimitives.WriteUInt32LittleEndian(nodes.AsSpan(at + 0x14), value);
    }

    [Fact]
    public void Reads_every_node_of_every_bucket_chain()
    {
        var attributes = AcdAttributeReader.Read(Memory(), Container);

        Assert.Equal(3, attributes.Count);
        Assert.Contains(attributes, a => a.AttributeId == OperatedId && a.RawValue == 1 && a.Kind == AcdAttribute.IntegerKind);
        var scaled = Assert.Single(attributes, a => a.AttributeId == 0x100);
        Assert.Equal(3, scaled.Parameter);
        Assert.Equal("2.5", scaled.FormattedValue);
    }

    [Fact]
    public void An_implausible_or_unreadable_container_reads_nothing()
    {
        Assert.Empty(AcdAttributeReader.Read(Memory(), 0x9000)); // unmapped
        var memory = new FakeMemoryReader();
        var container = new byte[0x200];
        WriteMap(container, 0x08, bucketCount: -1, buckets: Buckets, entries: 3);
        memory.AddRegion(Container, container);
        Assert.Empty(AcdAttributeReader.Read(memory, Container));
    }
}
