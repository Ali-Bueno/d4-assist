using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class WaypointCatalogRealDataTests
{
    // Build 3.2.3: Skov_Temis waypoint at (2581,-482) in Sanctuary_Eastern_Continent; Skov_Philios_OracleTown is "Visionsende" in deDE.
    private const int Temis = 1893662, PhiliosOracleTown = 1936327, OpenWorld = 69068, Nevesk = 448837;

    [Fact]
    public void Temis_IsAnOpenWorldWaypointWithItsTownSpot()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);

        var temis = Assert.Single(new WaypointCatalog(store, TypeSchema.Load(definitions)).All, w => w.LevelAreaSno == Temis);

        Assert.Equal(OpenWorld, temis.WorldSno);
        Assert.InRange(temis.Position.X, 2575, 2587);
        Assert.InRange(temis.Position.Y, -488, -476);
    }

    [Fact]
    public void A_waypoints_name_is_its_level_areas_localized_name()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new WaypointCatalog(store, TypeSchema.Load(definitions));
        var philios = catalog.All.Single(w => w.LevelAreaSno == PhiliosOracleTown);

        string name = catalog.Name(philios, "deDE");

        if (store.Text("deDE", "LevelArea_Skov_Temis", "Name") is null) return; // the install has no German text
        Assert.Equal("Visionsende", name);
    }

    // Build 3.2.3: every one of the 53 waypoints has a region; Nevesk (Frac_Taiga_S_Prologue_Village) is in "Zersplitterte Gipfel".
    [Fact]
    public void A_waypoints_region_is_its_subzones_zone_name()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var catalog = new WaypointCatalog(store, TypeSchema.Load(definitions));
        if (store.Text("deDE", "LevelArea_Skov_Temis", "Name") is null) return; // the install has no German text

        Assert.Equal("Zersplitterte Gipfel", catalog.Zone(catalog.All.First(w => w.LevelAreaSno == Nevesk), "deDE"));
        Assert.All(catalog.All, w => Assert.NotNull(catalog.Zone(w, "deDE")));
    }

    // Live 2026-10-25: the helltide's gifts in level area Hawe_WetlandNorth lie in territory "Auen von Fethis", Hawezar.
    [Fact]
    public void A_level_areas_territory_is_its_subzones_territory_name()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        if (store.Text("deDE", "LevelArea_Skov_Temis", "Name") is null) return; // the install has no German text
        var schema = TypeSchema.Load(definitions);
        var zones = new ZoneCatalog(store, schema);
        int wetlandNorth = store.Resolve(schema.SnoGroup("LevelAreaDefinition"), "Hawe_WetlandNorth");

        Assert.Equal("Auen von Fethis", zones.TerritoryName(wetlandNorth, "deDE"));
        Assert.Equal("Hawezar", zones.Name(wetlandNorth, "deDE"));
    }
}
