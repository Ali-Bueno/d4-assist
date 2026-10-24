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
        // Live 2026-10-22, phase 328: the open world's copy at (2959.2+16,-1088.5+13) is on the grid but 3000 units off this world's tiles.
        Assert.Empty(layers.Place([new SceneMarker(Scene + 1, TriggerMarker, 99, [])], Tiles(RoomA), Map((Trigger, 2975.2f, -1075.5f))));

        Assert.Equal((-16f, 205f), (b.Position.X, b.Position.Y));
        Assert.Equal(-48f, Assert.Single(layers.Place([In(LayerA)], Tiles(RoomA), [])).Position.X);
    }

    // Live 2026-10-24, Skartara's mindscape 4: the step's six Hatred Spires are markers of the holdout tunnel on its tile at (0,-32),
    // but the spires stood (19.9,-30.5) off, all six.
    private const int Tunnel = 2, Spire = 2495603;
    private static readonly (float X, float Y)[] SpireMarks = [(13, 14), (36, 13), (25, -6), (8, -8), (-9, 8), (37, -15)];
    private static readonly SceneMarker[] Spires = [.. SpireMarks.Select(m => new SceneMarker(Tunnel, new PlacedMarker(1, Spire, new(m.X, m.Y, 0))))];
    private static Dictionary<int, List<(Vector3 Wp, Quaternion Q)>> TunnelTile => new() { [Tunnel] = [(new(0, -32, 0), Quaternion.Identity)] };

    [Fact]
    public void A_scenes_markers_move_to_where_two_of_their_actors_stand_one_shift_off_and_stay_there()
    {
        var layers = new SceneLayerPlacement();
        var seen = Map((Spire, 32.7f, -48.1f), (Spire, 10.9f, -54.9f), (Spire, 999, 999));

        var moved = layers.Place(Spires, TunnelTile, [], seen);
        var later = layers.Place(Spires, TunnelTile, [], []);

        Assert.Equal(6, moved.Count);
        Assert.Contains(moved, p => Vector2.Distance(new(p.Position.X, p.Position.Y), new(27.5f, -70.8f)) < 1);
        Assert.Equal(moved.Select(p => p.Position), later.Select(p => p.Position));
    }

    [Fact]
    public void A_scenes_markers_stay_on_the_tile_while_their_actors_stand_there_or_only_one_stands_off()
    {
        var layers = new SceneLayerPlacement();
        var one = layers.Place(Spires, TunnelTile, [], Map((Spire, 32.7f, -48.1f)));
        var onTile = layers.Place(Spires, TunnelTile, [], Map((Spire, 13, -18), (Spire, 36, -19), (Spire, 32.7f, -48.1f), (Spire, 10.9f, -54.9f)));

        Assert.Contains(one, p => (p.Position.X, p.Position.Y) == (13f, -18f));
        Assert.Contains(onTile, p => (p.Position.X, p.Position.Y) == (13f, -18f));
    }
}
