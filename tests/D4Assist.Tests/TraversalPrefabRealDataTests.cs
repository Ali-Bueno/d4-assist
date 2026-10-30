using D4Assist.Game;
using D4Assist.Navigation;
using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;
using D4Data.Tests;

namespace D4Assist.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class TraversalPrefabRealDataTests
{
    // Tester 2026-10-30, the Temple of the Deathspeaker (quest 600410 phase 34): the guide pointed straight at the last two Echoes
    // of Elias. This tile, at (0,0), joins its west floor to its south one only over a chain of four jumps.
    private const string TempleTile = "Ancients_Flooded_SW_POI_CSD_Hawe_Swamp_TempleoftheDeathspeaker";
    private static readonly (float X, float Y) WestFloor = (18.5f, 3.5f), SouthFloor = (28.5f, 18.5f), WestJump = (13.5f, 5.3f);

    [Fact]
    public void The_temple_tiles_chain_of_jumps_leads_from_its_west_floor_to_its_south_one()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        int sno =store.Resolve(Scenes(store, schema, allLayers: false, includeMap: false).Group, TempleTile);
        var links = LinksOf(store, schema, sno);
        Assert.True(new CascTileWalkability(Scenes(store, schema, allLayers: false, includeMap: true)).TryGet(sno, out var grid));
        var south = grid.CellAt(SouthFloor.X, SouthFloor.Y);

        Assert.True(RoomPathfinder.TryFindPath(grid, WestFloor, [south], links, out _, out _, out var jump));
        Assert.True(MathF.Abs(jump!.Value.At.X - WestJump.X) + MathF.Abs(jump.Value.At.Y - WestJump.Y) < 0.1f, $"first jump at {jump.Value.At}");
        // The same ends paired with their mutual nearest join two ledges to themselves and leave no way across.
        var ends = links.SelectMany(l => new[] { l.A, l.B }).ToList();
        Assert.False(RoomPathfinder.TryFindPath(grid, WestFloor, [south], JumpLinkCollector.Pair(ends), out _, out _, out _));
    }

    [Theory]
    [InlineData(TempleTile, 4)]
    [InlineData("Ancients_Flooded_FLEX_POI_IslandHop_01", 7)] // six jumps and a climb, placed alike in two layers
    [InlineData("Cave_Flooded_FLEX_POI_GrottoEntrance_01", 6)] // the Snake Den's entrance: three ladders, two jumps, a duck-under
    public void Every_traversal_prefab_of_a_tile_gives_one_link(string tile, int traversals)
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);

        Assert.Equal(traversals, LinksOf(store, schema, store.Resolve(Scenes(store, schema, allLayers: false, includeMap: false).Group, tile)).Count);
    }

    // As the app reads them (Program.cs): traversal markers from every layer, the walk map with the gameplay layers.
    private static SceneReader Scenes(SnoStore store, TypeSchema schema, bool allLayers, bool includeMap) =>
        new(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers, includeMap);

    // The links the collector applies once the tile loads at (0,0), unrotated.
    private static IReadOnlyList<JumpLink> LinksOf(SnoStore store, TypeSchema schema, int sceneSno)
    {
        var traversals = new TileActorReader(Scenes(store, schema, allLayers: true, includeMap: false), sno => TraversalKinds.Of(store.Name(sno)) != TraversalKind.None);
        var feed = new FakeFeed();
        IReadOnlyList<JumpLink> links = [];
        _ = new JumpLinkCollector(feed, traversals.In, applied => links = applied, kindOf: sno => TraversalKinds.Of(store.Name(sno)));
        feed.RaiseTiles(new TileState(sceneSno, store.Name(sceneSno), 0, 0, 0, 0, 0, 0, 0));
        return links;
    }
}
