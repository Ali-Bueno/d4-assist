using D4Assist.Game;
using D4Assist.Navigation;
using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;
using D4Data.Tests;

namespace D4Assist.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class SwampFrontierRealDataTests
{
    // Tester 2026-10-29, the Corrosive Swamp (quest 589546): from Tysa's spot the guide led east to "unexplored" ground with no way
    // there. The clearing (64x64, exits N, S, E) has no walls: its floor touches the north edge's east half and the east edge's north
    // half only on islands, and meets the way in and the two ways on (north-west, south-west, east-south) from Tysa's floor.
    private const string Clearing = "Hawe_Swamp_FLEX_POI_SeaHagFight_01";
    private static readonly (float X, float Y) Tysa = (26.5f, 24.5f);

    // The three ways a walk from Tysa reaches: north-west to the way in, east-south and south-west to the ways on.
    private static readonly (float X, float Y)[] WaysOn = [(0f, 15.5f), (48.5f, 64f), (64f, 14.5f)];

    [Fact]
    public void The_clearings_island_exits_are_no_way_on_from_tysa()
    {
        if (Load() is not { } loaded) return;
        var (map, walkability, store) = loaded;
        using var _ = store;

        var step = new ExplorationGuide(map, walkability).Step(new PlayerInput(Tysa.X, Tysa.Y, 0));

        Assert.Equal(GuideStatus.AllExplored, step.Status);
    }

    // Same tester, of the radar ("the cue that there was an opening I could walk through") and the room description.
    [Fact]
    public void The_room_description_and_the_radar_name_only_the_ways_a_walk_from_tysa_reaches()
    {
        if (Load() is not { } loaded) return;
        var (map, walkability, store) = loaded;
        using var _ = store;
        Assert.True(RoomModel.TryGetRoom(map, Tysa.X, Tysa.Y, out var room, walkability));
        Assert.Equal(6, room.Openings.Count);

        var text = new AnnouncementPolicy().Describe(room, new PlayerInput(Tysa.X, Tysa.Y, 0)).Text;

        Assert.Equal("Cruce. Salidas: arriba a la derecha, abajo a la derecha, arriba a la izquierda.", text);

        var world = new DungeonRadarWorld(walkability);
        world.UpdateTiles([.. map.Tiles.Select(t => t.Input).Select(t =>
            new TileState(t.SceneSno, t.Name, t.OriginX, t.OriginY, t.SizeX, t.SizeY, t.ExitMask, t.NodeId, t.GroupId, t.YawDegrees, t.AuthoredExitMask))]);
        int x = (int)Tysa.X, y = (int)Tysa.Y;
        var onTysasFloor = world.Doorways.Where(d => d.Room == world.RoomAt(x, y) && d.Floor == world.FloorAt(x, y)).Select(d => ((float)d.Centre.X, (float)d.Centre.Y));
        Assert.Equal(WaysOn.Order(), onTysasFloor.Order());
    }

    private static (DungeonMap Map, CascTileWalkability Walkability, SnoStore Store)? Load()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return null;
        var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var scenes = new SceneReader(store, schema, new MarkerReader(store, schema), new NavGridReader(schema), allLayers: false, includeMap: true);
        int Sno(string name) => store.Resolve(scenes.Group, name);
        const int north = ExitMaskBits.North, south = ExitMaskBits.South, east = ExitMaskBits.East, west = ExitMaskBits.West;
        var map = new DungeonMap();
        map.AddOrUpdate([
            new TileInput(Sno(Clearing), Clearing, 0, 0, 64, 64, north | south | east, 1, 0, 0f, north | south | east),
            new TileInput(Sno("Hawe_Swamp_S_Entrance_01_CSD_Hawe_Wetland_ZakFortCrypts"), "in", -32, 0, 32, 32, south, 2, 0),
            new TileInput(Sno("Hawe_Swamp_N_01"), "on", 64, 0, 32, 32, north, 3, 0),
            new TileInput(Sno("Hawe_Swamp_W_01"), "on", 32, 64, 32, 32, west, 4, 0),
        ]);
        foreach (var (x, y) in new[] { Tysa, (-16f, 16f), (80f, 16f), (48f, 80f) }) map.MarkVisited(x, y);
        return (map, new CascTileWalkability(scenes), store);
    }
}
