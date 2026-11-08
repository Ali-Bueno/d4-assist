using D4Memory.Ui;

namespace D4Memory.Tests;

public class WorldTierMenuReaderTests
{
    // research/client-memory.md §4, world tier: [[G + 0xC78] + 0x3F0] is set while the statue's menu is open.
    private const long ModuleBase = 0x7FF600000000, GameObjectRva = 0x3649A38, G = 0x10000000, Ui = 0x20000000;

    [Theory]
    [InlineData(0x13951A432F0L, true)] // live 2026-11-07, menu open
    [InlineData(0L, false)]
    public void The_menu_pointer_tells_whether_the_statue_menu_is_open(long pointer, bool open)
    {
        var memory = new FakeMemoryReader();
        memory.AddRegion(ModuleBase + GameObjectRva, BitConverter.GetBytes(G));
        var g = new byte[0x1000];
        BitConverter.GetBytes(Ui).CopyTo(g, 0xC78);
        memory.AddRegion(G, g);
        var ui = new byte[0x400];
        BitConverter.GetBytes(pointer).CopyTo(ui, 0x3F0);
        memory.AddRegion(Ui, ui);

        Assert.Equal(open, WorldTierMenuReader.IsOpen(memory, ModuleBase, GameObjectRva));
    }

    [Fact]
    public void No_game_object_reads_as_unknown()
    {
        var memory = new FakeMemoryReader();
        memory.AddRegion(ModuleBase + GameObjectRva, new byte[8]);

        Assert.Null(WorldTierMenuReader.IsOpen(memory, ModuleBase, GameObjectRva));
    }
}
