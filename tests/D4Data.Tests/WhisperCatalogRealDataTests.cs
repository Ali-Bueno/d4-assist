using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class WhisperCatalogRealDataTests
{
    private const int OpenWorld = 69068;

    private static void WithCatalog(Action<WhisperCatalog, SnoStore> test)
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        test(new WhisperCatalog(store, TypeSchema.Load(definitions)), store);
    }

    [Fact]
    public void Each_kind_pays_its_grim_favors_from_the_bounty_definitions() => WithCatalog((catalog, _) =>
    {
        Assert.Equal(1, Assert.Single(catalog.All, w => w.QuestName == "Bounty_Culling_Tier1_Scos_Hills_Woodwraith").Favors);
        Assert.Equal(3, Assert.Single(catalog.All, w => w.QuestName == "Bounty_Harvest_Tier2_Kehj_Caldeum").Favors);
        Assert.Equal(5, Assert.Single(catalog.All, w => w.QuestName == "Bounty_Dungeon_Tier3_Step_Eastern_PallidDelve").Favors);
        Assert.Equal(5, Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Hawe_Boss").Favors);
        Assert.Equal(3, Assert.Single(catalog.All, w => w.QuestName == "Bounty_ZE_Tier2_Kehj_Alcarnus").Favors);
        Assert.DoesNotContain(catalog.All, w => w.Favors == 0);
    });

    [Fact]
    public void World_bosses_and_legions_list_at_every_place_they_can_rise() => WithCatalog((catalog, _) =>
    {
        Assert.Equal(WhisperKind.WorldBoss, Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Hawe_Boss").Kind);
        Assert.Equal(WhisperKind.Legion, Assert.Single(catalog.All, w => w.QuestName == "Bounty_ZE_Tier2_Kehj_Alcarnus").Kind);
        Assert.True(catalog.All.Count(w => w.Kind == WhisperKind.WorldBoss) >= 5);
        Assert.True(catalog.All.Count(w => w.Kind == WhisperKind.Legion) >= 5);
    });

    [Fact]
    public void PvP_seasons_and_breadcrumbs_are_left_out_and_every_whisper_has_a_spot() => WithCatalog((catalog, _) =>
    {
        Assert.True(catalog.All.Count > 500);
        Assert.DoesNotContain(catalog.All, w => w.QuestName.Contains("pvp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(catalog.All, w => w.QuestName.Contains("Breadcrumb", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.All, w => w.QuestName.StartsWith("S07_", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.All, w => w.QuestName.StartsWith("Bounty_Helltide_", StringComparison.Ordinal));
        Assert.All(catalog.All, w => Assert.Equal(OpenWorld, w.WorldSno));
        Assert.Equal(catalog.All.Count, catalog.All.Select(w => w.QuestSno).Distinct().Count());
    });

    [Fact]
    public void A_whisper_is_up_while_its_world_state_holds_its_value() => WithCatalog((catalog, _) =>
    {
        var whisper = catalog.All.First(w => w.QuestName.StartsWith("Bounty_OWC_", StringComparison.Ordinal));
        Assert.True(whisper.IsUp(new Dictionary<int, int> { [whisper.WorldStateSno] = whisper.WorldStateValue }));
        Assert.False(whisper.IsUp(new Dictionary<int, int> { [whisper.WorldStateSno] = whisper.WorldStateValue + 1 }));
        Assert.False(whisper.IsUp(new Dictionary<int, int>()));
    });

    [Fact]
    public void Whispers_have_the_games_names() => WithCatalog((catalog, store) =>
    {
        if (store.Text("deDE", "Quest_Bounty_Harvest_Tier2_Kehj_Caldeum", "Name") is null) return; // no German text
        Assert.Equal("Ernte: Caldeum", catalog.Name(Assert.Single(catalog.All, w => w.QuestName == "Bounty_Harvest_Tier2_Kehj_Caldeum"), "deDE"));
        Assert.Equal("Weltboss", catalog.Name(Assert.Single(catalog.All, w => w.QuestName == "Bounty_WE_Tier3_Hawe_Boss"), "deDE"));
    });
}
