using D4Memory.Actors;
using D4Memory.Ui;

namespace D4Memory.Tests;

public class ActivatedWaypointsReaderTests
{
    // research/client-memory.md §4: [G + 0xA80] → player block, slots at +0x1F8 every 0xF520, PlayerSavedData at slot + 0xE0
    // (hotbar at +0, waypoints at +0x78); the map object at [G + 0xC78], zoom at +0x2F4, center at +0x2FC.
    private const long ModuleBase = 0x7FF600000000, GameObjectRva = 0x3651918, Heap = 0x10000000;
    private const long GameObject = Heap, Block = Heap + 0x1000, MapObject = Heap + 0x800;
    private const int Temis = 1893662, Jirandai = 287458, GeaKul = 747179, Visionsende = 1936327;
    private static readonly IReadOnlySet<int> Catalog = new HashSet<int> { Temis, Jirandai, GeaKul, Visionsende };

    [Fact]
    public void The_local_players_slot_is_the_one_with_a_hotbar()
    {
        var memory = Game(other: [Jirandai, Temis, Visionsende], local: [Jirandai, GeaKul, Temis], localSlot: 2);

        var activated = ActivatedWaypointsReader.Read(memory, ModuleBase, GameObjectRva, Catalog);

        Assert.Equal([Jirandai, GeaKul, Temis], activated);
    }

    // Live: the local slot held other data (0x34CF077B, ...) right after its 36th waypoint.
    [Fact]
    public void The_list_ends_at_the_first_word_that_is_no_waypoint()
    {
        var memory = Game(other: [], local: [Jirandai, 0x34CF077B, GeaKul], localSlot: 0);

        Assert.Equal([Jirandai], ActivatedWaypointsReader.Read(memory, ModuleBase, GameObjectRva, Catalog));
    }

    [Fact]
    public void No_local_slot_reads_as_unknown_not_as_no_waypoints()
    {
        var memory = Game(other: [Jirandai], local: [Temis], localSlot: null);

        Assert.Null(ActivatedWaypointsReader.Read(memory, ModuleBase, GameObjectRva, Catalog));
    }

    [Fact]
    public void A_block_without_the_slot_indexes_is_not_read()
    {
        var memory = Game(other: [], local: [Temis], localSlot: 1, slotIndexOffset: 7);

        Assert.Null(ActivatedWaypointsReader.Read(memory, ModuleBase, GameObjectRva, Catalog));
    }

    [Fact]
    public void The_map_view_is_read_from_the_map_object()
    {
        var memory = Game(other: [], local: [], localSlot: null);

        Assert.Equal(new MapView(574.93f, -267.57f, 417.6f), MapViewReader.Read(memory, ModuleBase, GameObjectRva));
    }

    [Fact]
    public void A_zoom_out_of_range_is_no_map_view()
    {
        var memory = Game(other: [], local: [], localSlot: null, zoom: 0);

        Assert.Null(MapViewReader.Read(memory, ModuleBase, GameObjectRva));
    }

    // Slot localSlot holds the local player's data (a hotbar), every other slot the other player's waypoints.
    private static FakeMemoryReader Game(int[] other, int[] local, int? localSlot, float zoom = 417.6f, int slotIndexOffset = 0)
    {
        var heap = new byte[0x1000 + 0x1F8 + 4 * 0xF520];
        BitConverter.GetBytes(Block).CopyTo(heap, 0xA80);
        BitConverter.GetBytes(MapObject).CopyTo(heap, 0xC78);
        BitConverter.GetBytes(zoom).CopyTo(heap, 0x800 + 0x2F4);
        BitConverter.GetBytes(574.93f).CopyTo(heap, 0x800 + 0x2FC);
        BitConverter.GetBytes(-267.57f).CopyTo(heap, 0x800 + 0x300);
        for (int i = 0; i < 4; i++)
        {
            int slot = 0x1000 + 0x1F8 + i * 0xF520, saved = slot + 0xE0;
            BitConverter.GetBytes(i + slotIndexOffset).CopyTo(heap, slot);
            bool isLocal = i == localSlot;
            for (int o = 0; o < 0x60; o += 4) BitConverter.GetBytes(uint.MaxValue).CopyTo(heap, saved + o);
            if (isLocal) BitConverter.GetBytes(0x21C3C1).CopyTo(heap, saved); // Warlock_Summon_Lunatic
            var waypoints = isLocal ? local : other;
            for (int w = 0; w < 64; w++)
                BitConverter.GetBytes(w < waypoints.Length ? waypoints[w] : -1).CopyTo(heap, saved + 0x78 + w * 4);
        }
        var memory = new FakeMemoryReader();
        memory.AddRegion(ModuleBase + GameObjectRva, BitConverter.GetBytes(GameObject));
        memory.AddRegion(Heap, heap);
        return memory;
    }
}
