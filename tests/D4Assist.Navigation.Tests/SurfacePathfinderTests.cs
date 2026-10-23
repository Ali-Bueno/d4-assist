using System.Numerics;
using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class SurfacePathfinderTests
{
    private const float V = SurfacePathfinder.VoxelUnits;

    // Surface voxels covering [x0, x1) x [y0, y1) at the height the function gives.
    private static IEnumerable<Vector3> Floor(float x0, float x1, float y0, float y1, Func<float, float, float> height)
    {
        for (float x = x0; x < x1; x += V)
            for (float y = y0; y < y1; y += V)
                yield return new Vector3(x, y, height(x, y));
    }

    private static float Length(Vector2 from, IReadOnlyList<Vector2> points)
    {
        float length = 0;
        foreach (var p in points) (length, from) = (length + Vector2.Distance(from, p), p);
        return length;
    }

    [Fact]
    public void Leads_straight_across_open_ground_and_ends_at_the_target()
    {
        var ground = Floor(0, 40, 0, 20, (_, _) => 0).ToList();

        var path = SurfacePathfinder.Find(ground, new(5, 10), new(35, 10));

        Assert.NotNull(path);
        Assert.Equal(new Vector2(35, 10), path[^1]);
        Assert.InRange(Length(new(5, 10), path), 29f, 32f);
        Assert.All(path.Zip(path.Skip(1)), leg => Assert.InRange(Vector2.Distance(leg.First, leg.Second), 0f, SurfacePathfinder.PointSpacingUnits + 1));
    }

    [Fact]
    public void A_cliff_seen_from_above_as_one_floor_is_not_walked_down()
    {
        // Low ground west of x=20, a plateau 6 units up east of it: side by side from above, no step joins them.
        var cliff = Floor(0, 40, 0, 20, (x, _) => x < 20 ? 0 : 6).ToList();

        Assert.Null(SurfacePathfinder.Find(cliff, new(5, 10), new(35, 10), fromZ: 0));
    }

    [Fact]
    public void A_ramp_up_the_cliff_is_taken_as_the_detour()
    {
        // The same cliff, with a ramp along the south edge (y < 4) rising 6 units over x 14..26.
        var ground = Floor(0, 40, 0, 20, (x, y) => y >= 4 ? (x < 20 ? 0 : 6) : Math.Clamp((x - 14) / 2, 0, 6)).ToList();

        var path = SurfacePathfinder.Find(ground, new(5, 15), new(35, 15), fromZ: 0);

        Assert.NotNull(path);
        Assert.Contains(path, p => p.X > 14 && p.X < 26 && p.Y < 4);
    }

    [Fact]
    public void A_bridge_over_a_lower_floor_keeps_both_floors()
    {
        // A river bed at z=0 running north-south (x 18..22) and a bridge at z=3 over it on y 9..11; banks at z=3.
        var banks = Floor(0, 18, 0, 20, (_, _) => 3).Concat(Floor(22, 40, 0, 20, (_, _) => 3));
        var bed = Floor(18, 22, 0, 20, (_, _) => 0);
        var bridge = Floor(18, 22, 9, 11, (_, _) => 3);

        var path = SurfacePathfinder.Find([.. banks, .. bed, .. bridge], new(5, 10), new(35, 10), fromZ: 3);

        Assert.NotNull(path);
        Assert.InRange(Length(new(5, 10), path), 29f, 33f); // straight over the bridge, not down into the bed
    }

    [Fact]
    public void Keeps_to_the_middle_of_a_path_rather_than_its_edge()
    {
        // An L-shaped path 6 units wide: the shortest line hugs the inner corner, the route keeps off it.
        var path = Floor(0, 30, 0, 6, (_, _) => 0).Concat(Floor(24, 30, 6, 30, (_, _) => 0)).ToList();

        var route = SurfacePathfinder.Find(path, new(3, 3), new(27, 27));

        Assert.NotNull(route);
        Assert.DoesNotContain(route, p => p.X < 24.9f && p.Y > 5.1f);
        Assert.All(route.Where(p => p.X < 20), p => Assert.InRange(p.Y, 1.5f, 4.5f));
    }

    [Fact]
    public void No_surfaces_near_the_start_gives_no_route()
    {
        var ground = Floor(0, 10, 0, 10, (_, _) => 0).ToList();

        Assert.Null(SurfacePathfinder.Find(ground, new(50, 50), new(5, 5)));
    }

    // Two ledges 5 units apart across a gap (x 15..20), as a Sightless Eye jump.
    private static List<Vector3> Ledges() => [.. Floor(0, 15, 0, 20, (_, _) => -4), .. Floor(20, 40, 0, 20, (_, _) => -4)];

    [Fact]
    public void A_traversal_joins_ledges_the_walk_cannot()
    {
        var jump = new SurfaceLink(new(14, 10, -4), new(20.5f, 10, -4));

        Assert.Null(SurfacePathfinder.Find(Ledges(), new(5, 10), new(35, 10), fromZ: -4));
        var path = SurfacePathfinder.Find(Ledges(), new(5, 10), new(35, 10), fromZ: -4, links: [jump]);

        Assert.NotNull(path);
        Assert.Equal(new Vector2(35, 10), path[^1]);
        Assert.InRange(Length(new(5, 10), path), 29f, 34f);
    }

    [Fact]
    public void A_one_way_traversal_is_not_taken_back()
    {
        var slide = new SurfaceLink(new(14, 10, -4), new(20.5f, 10, -4), OneWay: true);

        Assert.NotNull(SurfacePathfinder.Find(Ledges(), new(5, 10), new(35, 10), fromZ: -4, links: [slide]));
        Assert.Null(SurfacePathfinder.Find(Ledges(), new(35, 10), new(5, 10), fromZ: -4, links: [slide]));
    }

    [Fact]
    public void A_traversal_with_an_end_off_the_floor_is_left_out()
    {
        var lost = new SurfaceLink(new(14, 10, -4), new(28, 60, -4));

        Assert.Null(SurfacePathfinder.Find(Ledges(), new(5, 10), new(35, 10), fromZ: -4, links: [lost]));
    }
}
