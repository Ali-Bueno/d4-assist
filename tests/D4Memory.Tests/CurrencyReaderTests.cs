using D4Memory.Actors;

namespace D4Memory.Tests;

public class CurrencyReaderTests
{
    // research/client-memory.md §4: [G + 0xA80] → player block, slots at +0x1F8 every 0xF520; the local one has a hotbar at
    // +0xE0; its currencies at slot + 0x7688, 98 entries 16 bytes apart, each an i64 amount first.
    private const long ModuleBase = 0x7FF600000000, GameObjectRva = 0x3651918, Heap = 0x10000000, Block = Heap + 0x1000;

    [Fact]
    public void The_local_players_slot_holds_the_amounts_by_currency_type()
    {
        // Live 2026-10-28: gold 93,051,377, cinders 36, one Baneful Heart; the other player's slot holds its own.
        var memory = Game(localSlot: 1, local: new() { [0] = 93_051_377, [CurrencyReader.Cinders] = 36, [CurrencyReader.BanefulHearts] = 1 },
            other: new() { [CurrencyReader.Cinders] = 500 });

        var amounts = CurrencyReader.Read(memory, ModuleBase, GameObjectRva);

        Assert.NotNull(amounts);
        Assert.Equal(CurrencyReader.Count, amounts.Count);
        Assert.Equal(93_051_377, amounts[0]);
        Assert.Equal(36, amounts[CurrencyReader.Cinders]);
        Assert.Equal(1, amounts[CurrencyReader.BanefulHearts]);
    }

    [Fact]
    public void A_word_that_is_no_amount_reads_as_unknown()
    {
        var memory = Game(localSlot: 0, local: new() { [CurrencyReader.Cinders] = -1 }, other: []);

        Assert.Null(CurrencyReader.Read(memory, ModuleBase, GameObjectRva));
    }

    [Fact]
    public void No_local_slot_reads_as_unknown_not_as_nothing()
    {
        var memory = Game(localSlot: null, local: [], other: new() { [CurrencyReader.Cinders] = 3 });

        Assert.Null(CurrencyReader.Read(memory, ModuleBase, GameObjectRva));
    }

    private static FakeMemoryReader Game(int? localSlot, Dictionary<int, long> local, Dictionary<int, long> other)
    {
        var heap = new byte[0x1000 + 0x1F8 + 4 * 0xF520];
        BitConverter.GetBytes(Block).CopyTo(heap, 0xA80);
        for (int i = 0; i < 4; i++)
        {
            int slot = 0x1000 + 0x1F8 + i * 0xF520, saved = slot + 0xE0;
            BitConverter.GetBytes(i).CopyTo(heap, slot);
            for (int o = 0; o < 0x60; o += 4) BitConverter.GetBytes(uint.MaxValue).CopyTo(heap, saved + o);
            if (i == localSlot) BitConverter.GetBytes(0x21C3C1).CopyTo(heap, saved); // Warlock_Summon_Lunatic
            foreach (var (type, amount) in i == localSlot ? local : other)
                BitConverter.GetBytes(amount).CopyTo(heap, slot + 0x7688 + type * 16);
        }
        var memory = new FakeMemoryReader();
        memory.AddRegion(ModuleBase + GameObjectRva, BitConverter.GetBytes(Heap));
        memory.AddRegion(Heap, heap);
        return memory;
    }
}
