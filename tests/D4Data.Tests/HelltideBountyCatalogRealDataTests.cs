using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class HelltideBountyCatalogRealDataTests
{
    // Build 3.2.3, live 2026-10-27: the Deep Forest's Blood Maiden at (-1303.9,-1002.2), up while its world state 1905410 is 1.
    private const int DeepForestQuest = 1905379, DeepForestState = 1905410, OpenWorld = 69068;

    [Fact]
    public void The_Deep_Forests_Blood_Maiden_has_its_spot_and_world_state()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new HelltideBountyCatalog(store, TypeSchema.Load(definitions));

        var maiden = Assert.Single(catalog.All, b => b.QuestSno == DeepForestQuest);

        Assert.Equal("Scos_DeepForest", maiden.Territory);
        Assert.Equal(DeepForestState, maiden.WorldStateSno);
        Assert.Equal(OpenWorld, maiden.WorldSno);
        Assert.InRange(maiden.Position.X, -1305, -1302);
        Assert.InRange(maiden.Position.Y, -1004, -1001);
        Assert.True(catalog.All.Count >= 11); // two or three per base-game region, two in each expansion
        if (store.Text("deDE", "Quest_Bounty_Helltide_Tier3_Scos_DeepForest_Succuboss", "Name") is null) return; // no German text
        Assert.Equal("Tötet die Blutjungfrau", catalog.Name(maiden, "deDE"));
    }
}
