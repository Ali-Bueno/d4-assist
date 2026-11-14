using D4Assist.Game;
using D4Assist.Navigation;
using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;
using D4Data.Tests;

namespace D4Assist.Tests;

/// <summary>Reads the local Diablo IV install; the test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class DarkenedHoltFillerRealDataTests
{
    // Tester log of 2026-11-14, the Darkened Holt (CSD_Frac_GaleValley_IllusionWoods, world 536086): with these three tiles
    // loaded the guide chose "UnvisitedTile tile:-1,-2 at (-4.5,-32.0)", the filler, and the player stood against the trees at
    // (-13.3,-23.5) three times in a minute and a half. Tiles, origins, live exit masks and room nodes as that log's TILES line.
    private const string Woods = "Frac_Pine_POI_FLEX_IllusionWoods_01", Filler = "Frac_Pine_FILLER_01", Entrance = "Frac_Pine_N_Entrance_01";
    private static readonly (float X, float Y) WherePlayerStood = (-15.1f, 3.2f);
    private static readonly (float X, float Y) IntoTheFiller = (-4.5f, -32f);

    [Fact]
    public void The_woods_gap_onto_the_filler_tile_is_never_the_guides_target()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var scenes = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);
        TileInput Tile(string name, float x, float y, float size, int liveMask, uint node)
        {
            int sno = store.Resolve(scenes.Group, name);
            return new TileInput(sno, name, x, y, size, size, liveMask, node, 0, 0f, scenes.ReadSummary(sno).ExitMask);
        }
        var filler = Tile(Filler, -32, -64, 32, 0, 0x66);
        Assert.Equal(0, filler.AuthoredExitMask);
        var walkability = new CascTileWalkability(scenes);
        Assert.False(walkability.TryGet(filler.SceneSno, out _));
        var map = new DungeonMap();
        map.AddOrUpdate([Tile(Woods, -96, -32, 96, 15, 0x66), filler, Tile(Entrance, 0, 0, 32, ExitMaskBits.North, 0x63)]);
        map.MarkVisited(16f, 12f);
        map.MarkVisited(WherePlayerStood.X, WherePlayerStood.Y);

        var step = new ExplorationGuide(map, walkability).Step(new PlayerInput(WherePlayerStood.X, WherePlayerStood.Y, 0));

        Assert.NotEqual(IntoTheFiller, step.Target?.Position);
        Assert.NotEqual(GuideTargetKind.UnvisitedTile, step.Target?.Kind);
    }
}
