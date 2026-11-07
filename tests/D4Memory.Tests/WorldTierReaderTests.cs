using D4Memory.Actors;

namespace D4Memory.Tests;

public class WorldTierReaderTests
{
    // research/client-memory.md §4, world tier: [base + game object RVA] → G; u32 eWorldTier at G + 0x990.
    private const long ModuleBase = 0x7FF600000000, GameObjectRva = 0x3649A38, Heap = 0x10000000;

    [Theory]
    [InlineData(0u, 0)] // Normal
    [InlineData(3u, 3)] // Penitent
    [InlineData(5u, 5)] // Torment 2 (live 2026-11-07)
    [InlineData(15u, 15)] // Torment 12, the highest in the game data
    public void The_game_object_holds_the_world_tier(uint word, int tier) =>
        Assert.Equal(tier, WorldTierReader.Read(Game(word), ModuleBase, GameObjectRva));

    [Fact]
    public void A_word_that_is_no_tier_reads_as_unknown() =>
        Assert.Null(WorldTierReader.Read(Game(0xFFFFFFFF), ModuleBase, GameObjectRva));

    [Fact]
    public void No_game_object_reads_as_unknown()
    {
        var memory = new FakeMemoryReader();
        memory.AddRegion(ModuleBase + GameObjectRva, new byte[8]);

        Assert.Null(WorldTierReader.Read(memory, ModuleBase, GameObjectRva));
    }

    private static FakeMemoryReader Game(uint tier)
    {
        var memory = new FakeMemoryReader();
        memory.AddRegion(ModuleBase + GameObjectRva, BitConverter.GetBytes(Heap));
        var g = new byte[0x1000];
        BitConverter.GetBytes(tier).CopyTo(g, 0x990);
        BitConverter.GetBytes(7u).CopyTo(g, 0x994); // the highest tier unlocked, Torment 4
        memory.AddRegion(Heap, g);
        return memory;
    }
}
