using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class WaypointCatalogRealDataTests
{
    // Build 3.2.3: Skov_Temis waypoint at (2581,-482) in Sanctuary_Eastern_Continent; Skov_Philios_OracleTown is "Visionsende" in deDE.
    private const int Temis = 1893662, PhiliosOracleTown = 1936327, OpenWorld = 69068;

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
}
