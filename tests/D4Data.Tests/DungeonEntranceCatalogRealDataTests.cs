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
    public void Entrances_tell_dungeons_cellars_and_boss_lairs_apart_and_a_season_s_ones_show_in_it_only()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new DungeonEntranceCatalog(store, TypeSchema.Load(definitions));

        var lairs = catalog.All.Where(e => e.Kind == EntranceKind.BossLair).ToList();
        Assert.Equal(10, lairs.Count); // Varshan, Grigoire, Zir, the Beast in the Ice, Duriel, Andariel, Belial, Urivar, the Harbinger, Mephisto
        Assert.Contains(lairs, e => e.DestLevelAreaName == "Boss_WT4_Duriel");
        Assert.InRange(catalog.All.Count(e => e.Kind == EntranceKind.Cellar), 150, 250);
        Assert.Equal(EntranceKind.Dungeon, catalog.All.Single(e => e.DestLevelAreaName == "DGN_Scos_SunkenRuins").Kind);

        Assert.True(catalog.Season >= 15);
        var oldSeason = catalog.All.First(e => e.Condition == "Season_Active_Season12");
        Assert.False(catalog.Shown(oldSeason)); // the season 12 town sieges
        var throne = catalog.All.Single(e => e.Condition == DungeonEntranceCatalog.TormentCondition);
        Assert.False(catalog.Shown(throne));
        Assert.True(catalog.Shown(throne, torment: true));
        Assert.True(catalog.Shown(catalog.All.First(e => e.Condition == "Campaign_Public")));
    }
}
