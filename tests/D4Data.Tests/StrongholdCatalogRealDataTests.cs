using System.Numerics;
using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class StrongholdCatalogRealDataTests
{
    private const int OpenWorld = 69068;

    [Fact]
    public void Every_region_s_strongholds_are_listed_at_their_map_icon_under_the_game_s_name()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new StrongholdCatalog(store, TypeSchema.Load(definitions));

        Assert.Equal(21, catalog.All.Count); // 3 per region, 7 regions, build 3.2.4
        Assert.All(catalog.All, s => Assert.Equal(OpenWorld, s.WorldSno));
        // Xanderos in Skovos, by the Inundated Cellar (user, 2026-11-06): its waypoint stands at (2793,-385); the icon drew 35 px
        // below its quest's start (2800,-377) at zoom 544, where its chain puts it.
        var xanderos = Assert.Single(catalog.All, s => s.QuestName == "CMP_Skov_Firstborn");
        Assert.Equal("Xanderos", catalog.Name(xanderos, "deDE"));
        Assert.True(Vector2.Distance(new(xanderos.Position.X, xanderos.Position.Y), new(2813f, -365f)) < 2);
        // The Onyx Watchtower's quest starts 56 units off its icon (live 2026-11-13: the cursor landed beside it).
        var onyx = Assert.Single(catalog.All, s => s.QuestName == "CMP_Step_OnyxWatchtower");
        Assert.True(Vector2.Distance(new(onyx.Position.X, onyx.Position.Y), new(-818f, -722f)) < 2);
        Assert.Contains(onyx.LevelAreas!, a => store.Name(a)!.StartsWith("Step_", StringComparison.Ordinal));
        Assert.Equal("Kreuzritterdenkmal", catalog.Name(Assert.Single(catalog.All, s => s.QuestName == "Camp_Hawe_CrusadersMont"), "deDE"));
    }
}
