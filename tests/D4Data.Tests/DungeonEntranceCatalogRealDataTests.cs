using System.Numerics;
using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class DungeonEntranceCatalogRealDataTests
{
    private const int OpenWorld = 69068;
    // research/map-points.md §2: the Portal_Dungeon_Generic at (-1240.7, 424.4) leads to the Conniving Tunnels.
    private static readonly Vector2 ConnivingTunnels = new(-1240.7f, 424.4f);

    [Fact]
    public void Open_world_dungeons_have_their_entrance_named_after_the_dungeon()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new DungeonEntranceCatalog(store, TypeSchema.Load(definitions));

        var tunnels = Assert.Single(catalog.All, e => e.WorldSno == OpenWorld && Vector2.Distance(new(e.Position.X, e.Position.Y), ConnivingTunnels) < 2);
        Assert.Equal("QST_Frac_CutthroatsEscape", tunnels.DestLevelAreaName); // the map's "Conniving Tunnels"
        // The tester's dungeon (2026-10-31): the Sunken Ruins in Scosglen, at (-2055.0,-1033.5) in build 3.2.4.
        var ruins = Assert.Single(catalog.All, e => e.DestLevelAreaName == "DGN_Scos_SunkenRuins");
        Assert.Equal(OpenWorld, ruins.WorldSno);
        Assert.True(Vector2.Distance(new(ruins.Position.X, ruins.Position.Y), new(-2055f, -1033.5f)) < 2);
        Assert.InRange(catalog.All.Count(e => e.WorldSno == OpenWorld), 400, 700); // 534 in build 3.2.4: dungeons and cellars, not every portal
    }

    [Fact]
    public void An_entrance_is_named_as_the_map_names_it_after_the_world_it_leads_into()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new DungeonEntranceCatalog(store, TypeSchema.Load(definitions));

        // The Aegoye by Temis: its first level area is only its first floor, "Aufnahme" (user, 2026-11-06).
        var aegoye = catalog.All.Single(e => e.DestWorldName == "DGN_Skov_Aegoye");
        Assert.Equal("Die Aegoye", catalog.Name(aegoye, "deDE"));
        Assert.Equal("Dunkle Zuflucht", catalog.Name(catalog.All.Single(e => e.DestWorldName == "DGN_Skov_DarkRefuge"), "deDE"));
        Assert.Equal("Verlassene Gruft", catalog.Name(catalog.All.Single(e => e.DestWorldName == "OWC_Skov_Philios_01"), "deDE"));
        Assert.NotEqual(catalog.Name(aegoye, "deDE"), catalog.Name(aegoye, "enUS"));
    }
    [Fact]
    public void Entrances_tell_dungeons_cellars_and_boss_lairs_apart_and_a_season_s_ones_show_in_it_only()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new DungeonEntranceCatalog(store, TypeSchema.Load(definitions));

        var lairs = catalog.All.Where(e => e.Kind == EntranceKind.BossLair).ToList();
        Assert.Equal(10, lairs.Count); // Varshan, Grigoire, Zir, the Beast in the Ice, Duriel, Andariel, Belial, Urivar, the Harbinger, Mephisto
        Assert.Contains(lairs, e => e.DestLevelAreaName == "Boss_WT4_Duriel");
        // Cellars are the OWC_ worlds (175 in build 3.2.4), Skovos' too behind their dungeon-type portal; quest cellars are not.
        Assert.InRange(catalog.All.Count(e => e.Kind == EntranceKind.Cellar), 160, 200);
        Assert.All(catalog.All.Where(e => e.Kind == EntranceKind.Cellar), e => Assert.StartsWith("OWC_", e.DestWorldName));
        Assert.Equal(EntranceKind.Cellar, catalog.All.Single(e => e.DestWorldName == "OWC_Skov_Philios_01").Kind);
        Assert.Equal(EntranceKind.QuestCellar, catalog.All.Single(e => e.DestWorldName == "QST_Skov_Chronicles_03_Cellar").Kind);
        // Only there while its quest runs: no portal at its spot with the player 3 units off (live 2026-11-06).
        Assert.Equal(EntranceKind.QuestCellar, catalog.All.Single(e => e.DestWorldName == "QST_Skov_Philios_Larder_Cellar").Kind);
        Assert.Equal(EntranceKind.Dungeon, catalog.All.Single(e => e.DestLevelAreaName == "DGN_Scos_SunkenRuins").Kind);
        Assert.Equal(EntranceKind.Dungeon, catalog.All.Single(e => e.DestWorldName == "DGN_Hawe_BelfryZakara").Kind); // behind a cellar door

        Assert.True(catalog.Season >= 15);
        var oldSeason = catalog.All.First(e => e.Condition == "Season_Active_Season12");
        Assert.False(catalog.Shown(oldSeason)); // the season 12 town sieges
        var throne = catalog.All.Single(e => e.Condition == DungeonEntranceCatalog.TormentCondition);
        Assert.False(catalog.Shown(throne));
        Assert.False(catalog.Shown(throne, worldTier: 3)); // Penitent
        Assert.True(catalog.Shown(throne, worldTier: WorldTierCondition.FirstTorment));
        Assert.True(catalog.Shown(catalog.All.First(e => e.Condition == "Campaign_Public" && e.MinWorldTier == 0)));
    }

    [Fact]
    public void Boss_lairs_are_there_from_their_world_tier_on()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new DungeonEntranceCatalog(store, TypeSchema.Load(definitions));

        // Each lair portal's snoPhasingVisibilityCondition: PlayerWorldTier_GameWorldTier_IsWorldTier_Torment_1orGreater (and
        // campaign variants), Mephisto's …_Torment_10orGreater (build 3.2.4).
        var lairs = catalog.All.Where(e => e.Kind == EntranceKind.BossLair).ToList();
        var mephisto = lairs.Single(e => e.DestWorldName == "Boss_Skov_UberMephisto");
        Assert.Equal(13, mephisto.MinWorldTier);
        Assert.All(lairs.Where(e => e != mephisto), e => Assert.Equal(WorldTierCondition.FirstTorment, e.MinWorldTier));

        var duriel = lairs.Single(e => e.DestWorldName == "Boss_WT4_Duriel");
        Assert.False(catalog.Shown(duriel));
        Assert.False(catalog.Shown(duriel, worldTier: 3));
        Assert.True(catalog.Shown(duriel, worldTier: 7)); // Torment 4
        Assert.False(catalog.Shown(mephisto, worldTier: 7));
        Assert.True(catalog.Shown(mephisto, worldTier: 13));
        // No dungeon or cellar needs a tier: the lists stay as they were below Torment.
        Assert.All(catalog.All.Where(e => e.Kind is EntranceKind.Dungeon or EntranceKind.Cellar && e.Condition != DungeonEntranceCatalog.TormentCondition),
            e => Assert.Equal(0, e.MinWorldTier));
    }
}
