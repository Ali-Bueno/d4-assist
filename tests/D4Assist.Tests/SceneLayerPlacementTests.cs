using System.Numerics;
using D4Assist.Game;
using D4Data.Readers;

namespace D4Assist.Tests;

public class SceneLayerPlacementTests
{
    // Live 2026-10-22, "Die Leere" (QST_Skov_Celestia_03): lock rooms A (layer 2410344) and B (2411059) of Cave_Tomb_EW_01, each with
    // its trigger at (16,13) and its demonic barrier wall at (16.1,11); room A's tile at (-64,352), room B's barrier marked on the
    // map at (-15.9,203) before its tile loaded.
    private const int Scene = 7, LayerA = 2410344, LayerB = 2411059, Trigger = 700166, Barrier = 726309, Lorath = 2129009;
    private static readonly PlacedMarker TriggerMarker = new(1, Trigger, new(16, 13, 1.4f));
    private static readonly PlacedMarker[] Anchors = [new(1, Barrier, new(16.1f, 11, 1.8f)), new(1, Lorath, new(16, 14.5f, 1.4f))];
    private static readonly Vector3 RoomA = new(-64, 352, 0);

    private static SceneMarker In(int layer) => new(Scene, TriggerMarker, layer, Anchors);

    private static Dictionary<int, List<(Vector3 Wp, Quaternion Q)>> Tiles(params Vector3[] origins) =>
        new() { [Scene] = [.. origins.Select(o => (o, Quaternion.Identity))] };

    private static List<(int, Vector3)> Map(params (int Sno, float X, float Y)[] marks) => [.. marks.Select(m => (m.Sno, new Vector3(m.X, m.Y, 1.8f)))];

    [Fact]
    public void A_second_layer_of_a_scene_lands_where_the_map_marks_its_actor_not_on_the_first_layers_tile()
    {
        var layers = new SceneLayerPlacement();
        // The banishment room's barrier wall, the same actor at another spot in its own tile, implies an origin off the grid.
        var map = Map((Barrier, -47.9f, 363), (Barrier, -15.9f, 203), (Barrier, 140.2f, -37.5f));

        var a = Assert.Single(layers.Place([In(LayerA)], Tiles(RoomA), map));
        var b = Assert.Single(layers.Place([In(LayerB)], Tiles(RoomA), map));

        Assert.Equal((-48f, 365f), (a.Position.X, a.Position.Y));
        Assert.Equal(-16f, b.Position.X, 0.01f);
        Assert.Equal(205f, b.Position.Y, 0.01f);
    }

    [Fact]
    public void A_second_layer_takes_the_scenes_other_tile_once_it_loads()
    {
        var layers = new SceneLayerPlacement();
        layers.Place([In(LayerA)], Tiles(RoomA), []);

        Assert.Empty(layers.Place([In(LayerB)], Tiles(RoomA), []));
        var b = Assert.Single(layers.Place([In(LayerB)], Tiles(RoomA, new(-32, 192, 0)), []));

        Assert.Equal((-16f, 205f), (b.Position.X, b.Position.Y));
    }

    [Fact]
    public void A_layer_keeps_its_tile_and_a_scene_placed_twice_says_nothing_until_one_tile_is_taken()
    {
        var layers = new SceneLayerPlacement();
        Assert.Empty(layers.Place([In(LayerA)], Tiles(RoomA, new(-32, 192, 0)), []));
        Assert.Empty(layers.Place([new SceneMarker(Scene, TriggerMarker)], Tiles(RoomA, new(-32, 192, 0)), []));

        layers.Place([In(LayerA)], Tiles(RoomA), []);
        var again = Assert.Single(layers.Place([In(LayerA)], Tiles(RoomA, new(-32, 192, 0)), []));

        Assert.Equal((-48f, 365f), (again.Position.X, again.Position.Y));
    }

    [Fact]
    public void A_restart_of_the_app_still_knows_the_first_layers_tile()
    {
        var before = new SceneLayerPlacement();
        before.Place([In(LayerA)], Tiles(RoomA), []);

        var after = new SceneLayerPlacement();
        after.Restore(before.Export());
        var b = Assert.Single(after.Place([In(LayerB)], Tiles(RoomA), Map((Barrier, -15.9f, 203))));

        Assert.Equal(-16f, b.Position.X, 0.01f);
        after.Reset();
        Assert.Equal(RoomA.Y + 13, Assert.Single(after.Place([In(LayerB)], Tiles(RoomA), [])).Position.Y);
    }

    [Fact]
    public void The_save_wins_over_a_layer_placed_before_it_came_in()
    {
        // Live 2026-10-22: the restarted app placed room B on room A's tile 0.13 s before the save named that tile room A's.
        var before = new SceneLayerPlacement();
        before.Place([In(LayerA)], Tiles(RoomA), []);
        var after = new SceneLayerPlacement();
        after.Place([In(LayerB)], Tiles(RoomA), []);

        after.Restore(before.Export());

        Assert.Empty(after.Place([In(LayerB)], Tiles(RoomA), []));
        Assert.Equal(205f, Assert.Single(after.Place([In(LayerB)], Tiles(RoomA), Map((Barrier, -15.9f, 203)))).Position.Y, 0.01f);
    }

    [Fact]
    public void The_maps_marker_of_the_steps_own_trigger_places_its_layer_even_beside_a_single_tile()
    {
        // Live 2026-10-22, phase 280 after a restart: only room A's tile loaded, the map marking room B's trigger at (-16,205) and the
        // overworld's copy of the same trigger at (3027.7,-1019.6), off the grid.
        var layers = new SceneLayerPlacement();

        var b = Assert.Single(layers.Place([In(LayerB)], Tiles(RoomA), Map((Trigger, -16, 205), (Trigger, 3027.7f, -1019.6f))));

        Assert.Equal((-16f, 205f), (b.Position.X, b.Position.Y));
        Assert.Equal(-48f, Assert.Single(layers.Place([In(LayerA)], Tiles(RoomA), [])).Position.X);
    }
}
