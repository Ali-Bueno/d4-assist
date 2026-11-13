using D4Memory.Actors;

namespace D4Memory.Tests;

public class CompletedQuestsReaderTests
{
    // research/client-memory.md §4: [[[G + 0xA80] + 0xA8] + 0] → buffer; capacity at +0x10, entry size 16 at +0x14, entries from
    // +0x50 with the quest SNO at +8.
    private const long ModuleBase = 0x7FF600000000, GameObjectRva = 0x3649A38, Heap = 0x10000000;
    private const long Block = Heap + 0x1000, Container = Heap + 0x2000, Buffer = Heap + 0x3000;
    private const int Malnok = 707148, QaraYisu = 496526, Prologue = 403556;

    [Fact]
    public void The_set_holds_every_quest_of_its_entries()
    {
        var memory = Game(capacity: 8, entries: [(1, Malnok), (4, QaraYisu), (6, Prologue)]);

        Assert.Equal(new HashSet<int> { Malnok, QaraYisu, Prologue }, CompletedQuestsReader.Read(memory, ModuleBase, GameObjectRva));
    }

    [Fact]
    public void Empty_entries_are_no_quests()
    {
        var memory = Game(capacity: 4, entries: [(2, -1)]);

        Assert.Empty(CompletedQuestsReader.Read(memory, ModuleBase, GameObjectRva)!);
    }

    [Fact]
    public void A_buffer_without_the_sets_entry_size_is_not_read()
    {
        var memory = Game(capacity: 4, entries: [(0, Malnok)], entrySize: 24);

        Assert.Null(CompletedQuestsReader.Read(memory, ModuleBase, GameObjectRva));
    }

    [Fact]
    public void A_capacity_out_of_range_is_not_read()
    {
        var memory = Game(capacity: 0, entries: []);

        Assert.Null(CompletedQuestsReader.Read(memory, ModuleBase, GameObjectRva));
    }

    private static FakeMemoryReader Game(int capacity, (int Index, int Sno)[] entries, int entrySize = 16)
    {
        var heap = new byte[0x3000 + 0x50 + Math.Max(capacity, 1) * 16];
        BitConverter.GetBytes(Block).CopyTo(heap, 0xA80);
        BitConverter.GetBytes(Container).CopyTo(heap, 0x1000 + 0xA8);
        BitConverter.GetBytes(Buffer).CopyTo(heap, 0x2000);
        BitConverter.GetBytes(capacity).CopyTo(heap, 0x3000 + 0x10);
        BitConverter.GetBytes(entrySize).CopyTo(heap, 0x3000 + 0x14);
        foreach (var (index, sno) in entries)
        {
            BitConverter.GetBytes(Buffer + 0x50 + index * 16L - 16).CopyTo(heap, 0x3000 + 0x50 + index * 16); // chain pointer
            BitConverter.GetBytes(sno).CopyTo(heap, 0x3000 + 0x50 + index * 16 + 8);
        }
        var memory = new FakeMemoryReader();
        memory.AddRegion(ModuleBase + GameObjectRva, BitConverter.GetBytes(Heap));
        memory.AddRegion(Heap, heap);
        return memory;
    }
}
