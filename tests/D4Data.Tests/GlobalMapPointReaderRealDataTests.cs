using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class GlobalMapPointReaderRealDataTests
{
    [Fact]
    public void Kyovashad_IsAWaypointNamedInEnglishAndSpanish()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);

        var points = new GlobalMapPointReader(store, schema).Read();
        var kyovashad = Assert.Single(points, p => p.Kind == MapPointKind.Waypoint && store.Name(p.LevelAreaSno) == "Frac_Kyovashad");

        var names = new LevelAreaNameReader(store, schema);
        Assert.Equal("Kyovashad", names.Name(kyovashad.LevelAreaSno, "enUS"));
        Assert.Equal("Kyovashad", names.Name(kyovashad.LevelAreaSno, "esES"));
    }

    [Fact]
    public void DungeonEntrance_IsNamedAfterItsDestinationArea()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);

        var points = new GlobalMapPointReader(store, schema).Read();
        var tunnels = points.First(p => p.Portal is not null && store.Name(p.LevelAreaSno) == "Frac_Taiga_E_SETunnelAreaCaves1");

        var names = new LevelAreaNameReader(store, schema);
        Assert.Equal("Conniving Tunnels", names.Name(tunnels.Portal!.DestLevelArea, "enUS"));
        Assert.Equal("Túneles Confabuladores", names.Name(tunnels.Portal.DestLevelArea, "esES"));
    }
}
