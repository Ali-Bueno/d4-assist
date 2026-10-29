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
        Assert.Equal(1, maiden.WorldStateValue);
        // Live 2026-10-27: at Helltide_BountySubzone 1 the map marked the Deep Forest's Blood Maiden, not the coast's.
        var coast = Assert.Single(catalog.All, b => b.Territory == "Scos_Coast" && b.QuestName.EndsWith("_Succuboss", StringComparison.Ordinal));
        Assert.True(maiden.HasTurn(1));
        Assert.False(coast.HasTurn(1));
        Assert.True(coast.HasTurn(2));
        Assert.Contains(catalog.All, b => b.Territory == "Hawe_Wetland");
        Assert.True(catalog.BountySubzoneSno > 0);
        if (store.Text("deDE", "Quest_Bounty_Helltide_Tier3_Scos_DeepForest_Succuboss", "Name") is null) return; // no German text
        Assert.Equal("Tötet die Blutjungfrau", catalog.Name(maiden, "deDE"));
    }

    [Fact]
    public void Each_territory_lists_its_bounties_with_the_world_state_that_puts_each_up()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new HelltideBountyCatalog(store, TypeSchema.Load(definitions));

        // The game's bounties table, the Scosglen coast: ten bounties, two of them up by a "Kill" state named apart from the quest.
        var coast = catalog.All.Where(b => b.Territory == "Scos_Coast").ToDictionary(b => b.QuestName[(b.QuestName.LastIndexOf('_') + 1)..]);
        Assert.Equal(
            ["ChestsAny", "Cinders", "CullCultist", "CullDemons", "CultistRituals", "Doomsayers", "Harbingers", "Ironwolves", "Structures", "Succuboss"],
            coast.Keys.Order());
        Assert.Equal("Bounty_Helltide_Tier1_Scos_Coast_Kill", store.Name(coast["CullCultist"].WorldStateSno));
        Assert.Equal("Bounty_Helltide_Tier2_Scos_Coast_Kill", store.Name(coast["CullDemons"].WorldStateSno));
        Assert.Equal("Bounty_Helltide_Tier2_Scos_Coast_Chests", store.Name(coast["ChestsAny"].WorldStateSno));
        Assert.DoesNotContain(catalog.All, b => b.QuestName.Contains("_S1", StringComparison.Ordinal)); // seasons' bounties
        Assert.DoesNotContain(catalog.All, b => b.Territory.Contains("PVP", StringComparison.Ordinal));

        // Live 2026-10-27 16:15 (world-state dump): the coast had cull cultists, cinders and the Blood Maiden up.
        var states = new Dictionary<int, int>
        {
            [coast["CullCultist"].WorldStateSno] = 1, [coast["Cinders"].WorldStateSno] = 1, [coast["Succuboss"].WorldStateSno] = 1,
        };
        Assert.Equal(["Cinders", "CullCultist", "Succuboss"], coast.Where(c => c.Value.IsUp(states)).Select(c => c.Key).Order());
    }
}
