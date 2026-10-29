using System.Buffers.Binary;
using D4Memory.Ui;

namespace D4Memory.Tests;

public class WorldStateReaderTests
{
    // [G + 0xA58] → node pool; [+0x28] → block holders 0x18 apart, count at +0x30; each block {first, count, 16, …, end at +0x30}.
    private const long ModuleBase = 0x7FF600000000, GameObjectRva = 0x3651918, Heap = 0x10000000;
    private const long GameObject = Heap, Pool = Heap + 0x100, Holders = Heap + 0x200, Blocks = Heap + 0x400, Nodes = Heap + 0x1000;
    private const int UberSubzone = 1055388, Expansions = 2077464, Other = 1234;

    [Fact]
    public void Every_block_of_the_pool_is_read()
    {
        // Live 2026-10-28: the first of five blocks held the expansions' state, the last the base game's.
        var memory = Game([[(Expansions, 1), (Other, 7)], [(UberSubzone, 1)]]);

        var states = WorldStateReader.Read(memory, ModuleBase, GameObjectRva);

        Assert.Equal(new Dictionary<int, int> { [Expansions] = 1, [Other] = 7, [UberSubzone] = 1 }, states);
    }

    [Fact]
    public void A_state_two_blocks_hold_differently_takes_the_later_and_is_reported()
    {
        var memory = Game([[(UberSubzone, 3)], [(UberSubzone, 1), (Other, 2)], [(Other, 2)]]);
        var conflicting = new List<int>();

        var states = WorldStateReader.Read(memory, ModuleBase, GameObjectRva, conflicting);

        Assert.Equal(1, states![UberSubzone]);
        Assert.Equal([UberSubzone], conflicting);
    }

    [Fact]
    public void No_world_state_block_reads_as_unknown()
    {
        var memory = Game([]);

        Assert.Null(WorldStateReader.Read(memory, ModuleBase, GameObjectRva));
    }

    private static FakeMemoryReader Game((int Sno, int Value)[][] blocks)
    {
        var memory = new FakeMemoryReader();
        var module = new byte[0x10];
        BinaryPrimitives.WriteInt64LittleEndian(module, GameObject);
        memory.AddRegion(ModuleBase + GameObjectRva, module);

        var heap = new byte[0x4000];
        BinaryPrimitives.WriteInt64LittleEndian(heap.AsSpan((int)(GameObject - Heap) + 0xA58), Pool);
        BinaryPrimitives.WriteInt64LittleEndian(heap.AsSpan((int)(Pool - Heap) + 0x28), Holders);
        BinaryPrimitives.WriteUInt32LittleEndian(heap.AsSpan((int)(Pool - Heap) + 0x30), (uint)blocks.Length);
        long node = Nodes;
        for (int b = 0; b < blocks.Length; b++)
        {
            long block = Blocks + b * 0x40, first = node;
            BinaryPrimitives.WriteInt64LittleEndian(heap.AsSpan((int)(Holders - Heap) + b * 0x18), block);
            foreach (var (sno, value) in blocks[b])
            {
                BinaryPrimitives.WriteInt32LittleEndian(heap.AsSpan((int)(node - Heap) + 8), sno);
                BinaryPrimitives.WriteInt32LittleEndian(heap.AsSpan((int)(node - Heap) + 12), value);
                node += 16;
            }
            BinaryPrimitives.WriteInt64LittleEndian(heap.AsSpan((int)(block - Heap)), first);
            BinaryPrimitives.WriteUInt32LittleEndian(heap.AsSpan((int)(block - Heap) + 0x10), (uint)blocks[b].Length);
            BinaryPrimitives.WriteUInt32LittleEndian(heap.AsSpan((int)(block - Heap) + 0x14), 16);
            BinaryPrimitives.WriteInt64LittleEndian(heap.AsSpan((int)(block - Heap) + 0x30), node);
        }
        memory.AddRegion(Heap, heap);
        return memory;
    }
}
