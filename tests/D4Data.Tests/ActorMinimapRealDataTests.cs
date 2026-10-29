using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class ActorMinimapRealDataTests
{
    // Build 3.2.3: treasure goblins have their own map icon; ordinary monsters none.
    private const int Goblin = 384484, UndercityGoblin = 2048580;

    [Fact]
    public void Treasure_goblins_have_a_map_icon_and_their_family_ordinary_monsters_none()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var reader = new ActorDefinitionReader(store, schema);

        var goblin = reader.Read(Goblin)!.Value;
        Assert.Equal("Treasure_Goblin", store.Name(goblin.MonsterFamilySno));
        Assert.Equal(new MinimapInfo(2609695548, ActiveByDefault: false, RespectsRevealRadius: true, MaxVisRange: 0), goblin.Minimap);

        var undercity = reader.Read(UndercityGoblin)!.Value;
        Assert.Equal("Treasure_Goblin", store.Name(undercity.MonsterFamilySno));
        Assert.True(undercity.Minimap is { ActiveByDefault: true, RespectsRevealRadius: false, MaxVisRange: 2048 });

        int cultist = store.EntriesWithPrefix(schema.SnoGroup("ActorDefinition"), "cultist_melee_female_phys").First(e => e.Name == "cultist_melee_female_phys").Id;
        Assert.Equal(0u, reader.Read(cultist)!.Value.Minimap?.Icon ?? 0u);
    }
}
