using System.Numerics;
using D4Assist.Game;
using D4Data.Readers;

namespace D4Assist.Tests;

public class OverworldPathsTests
{
    private const int Scene = 1;
    private const float V = 0.5f;

    // One 96-unit chunk at the origin: a ground floor (Z 0) whose straight way north-south crosses a door spot at x 40-44, y 46-50,
    // and a long way round the east side; an upper ledge (Z 6) over the door spot.
    private static OverworldPaths Paths()
    {
        var voxels = new List<Vector3>();
        for (float x = 0; x < 96; x += V)
            for (float y = 40; y < 56; y += V)
                if (x is < 30 or >= 60 || y is >= 46 and < 50) voxels.Add(new(x, y, 0)); // a wall band 30..60 but for a gap at y 46..50
        for (float x = 0; x < 96; x += V)
            for (float y = 80; y < 90; y += V)
                voxels.Add(new(x, y, 0)); // the way round, east
        for (float y = 56; y < 80; y += V)
            for (float x = 0; x < 6; x += V) voxels.Add(new(x, y, 0));
        for (float y = 56; y < 80; y += V)
            for (float x = 90; x < 96; x += V) voxels.Add(new(x, y, 0));
        for (float x = 40; x < 44; x += V)
            for (float y = 46; y < 50; y += V)
                voxels.Add(new(x, y, 6)); // a ledge over the gap, out of reach from the ground
        return new OverworldPaths(_ => [new SceneChunkInfo(Scene, Vector3.Zero, Quaternion.Identity, 0)], _ => voxels);
    }

    private static float Length(Vector2 from, IReadOnlyList<Vector2> points)
    {
        float length = 0;
        foreach (var p in points) (length, from) = (length + Vector2.Distance(from, p), p);
        return length;
    }

    private static DoorWall Door(float z) =>
        new(Enumerable.Range(40, 4).SelectMany(x => Enumerable.Range(46, 4).Select(y => (x, y))).ToHashSet(), z);

    [Fact]
    public void A_new_search_in_the_same_stretch_reuses_the_map_and_a_door_change_builds_it_anew()
    {
        var paths = Paths();
        var to = new Vector2(80, 48);

        var first = paths.Find("W", new Vector3(10, 48, 0), to, null);
        var again = paths.Find("W", new Vector3(12, 48, 0), to, null);
        Assert.Equal((1, 1), paths.MapUse);
        Assert.NotNull(first);
        Assert.Equal(first.Count, again!.Count);

        var shut = paths.Find("W", new Vector3(12, 48, 0), to, [Door(0)]);
        Assert.Equal((2, 1), paths.MapUse);
        Assert.True(Length(new(12, 48), shut!) > 120f); // round the door, as on a fresh map
        paths.Find("W", new Vector3(12, 48, 0), to, null);
        Assert.Equal((3, 1), paths.MapUse);
    }

    [Fact]
    public void A_forgotten_map_is_built_anew()
    {
        var paths = Paths();
        var to = new Vector2(80, 48);
        paths.Find("W", new Vector3(10, 48, 0), to, null);

        paths.Forget();
        Assert.NotNull(paths.Find("W", new Vector3(12, 48, 0), to, null));
        Assert.Equal((2, 0), paths.MapUse);
    }

    [Fact]
    public void A_locked_door_on_the_way_sends_the_path_round()
    {
        var from = new Vector3(10, 48, 0);
        var to = new Vector2(80, 48);

        var open = Paths().Find("W", from, to, null);
        var shut = Paths().Find("W", from, to, [Door(0)]);

        Assert.InRange(Length(new(from.X, from.Y), open!), 68f, 75f);
        Assert.NotNull(shut);
        Assert.True(Length(new(from.X, from.Y), shut) > 120f);
        Assert.All(shut, p => Assert.False(p.X is >= 40 and < 44 && p.Y is >= 46 and < 50));
    }

    // The Darkened Way's invisible doors, read a hair off the half units, lay a cell apart across the ramp (tester 2026-11-01).
    [Fact]
    public void Two_locked_doors_a_cell_apart_leave_no_way_between_them()
    {
        var from = new Vector3(10, 48, 0);
        DoorWall Cells(int y0, int y1) => new(Enumerable.Range(40, 4).SelectMany(x => Enumerable.Range(y0, y1 - y0 + 1).Select(y => (x, y))).ToHashSet(), 0);

        var shut = Paths().Find("W", from, new Vector2(80, 48), [Cells(46, 47), Cells(49, 49)]);

        Assert.NotNull(shut);
        Assert.True(Length(new(from.X, from.Y), shut) > 120f);
    }

    [Fact]
    public void A_door_on_another_floor_bars_only_that_floor()
    {
        var from = new Vector3(10, 48, 0);

        var path = Paths().Find("W", from, new Vector2(80, 48), [Door(OverworldPaths.DoorFloorUnits + 4)]);

        Assert.InRange(Length(new(from.X, from.Y), path!), 68f, 75f);
    }
}
