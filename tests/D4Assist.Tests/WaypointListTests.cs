using D4Assist.Game;
using D4Assist.Navigation;

namespace D4Assist.Tests;

public class WaypointListTests
{
    // Player in Gea Kul (681.6, -410.3); waypoints from the catalog (research/client-memory.md §4).
    private static readonly (string Name, string? Region, double X, double Y)[] Waypoints =
        [("Zarbinzet", "Hawezar", -236, 386), ("Gea Kul", "Kehjistan", 680, -418), ("Basar von Kurast", "Nahantu", 1038, 152)];

    [Fact]
    public void Entries_are_nearest_first_with_distance_and_direction()
    {
        var entries = WaypointList.Build(Waypoints, 681.6, -410.3);

        Assert.Equal(["Gea Kul", "Basar von Kurast", "Zarbinzet"], entries.Select(e => e.Name));
        Assert.InRange(entries[0].Distance, 7, 9);
        Assert.Equal(StickDirection.Right, entries[2].Direction); // -X+Y is screen right
    }

    [Fact]
    public void Up_and_down_stop_at_the_ends()
    {
        var list = new WaypointList();
        list.Open(WaypointList.Build(Waypoints, 681.6, -410.3));

        Assert.False(list.Previous());
        Assert.True(list.Next());
        Assert.True(list.Next());
        Assert.False(list.Next());
        Assert.Equal("Zarbinzet", list.Current?.Name);
        Assert.Equal(2, list.Index);
    }

    [Fact]
    public void An_empty_list_stays_closed()
    {
        var list = new WaypointList();

        list.Open([]);

        Assert.False(list.IsOpen);
        Assert.Null(list.Current);
    }
}
