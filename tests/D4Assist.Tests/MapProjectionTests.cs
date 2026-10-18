using D4Assist.Game;
using D4Memory.Ui;

namespace D4Assist.Tests;

public class MapProjectionTests
{
    // Live 2026-10-18, 1920×1080, map centered on the player in Gea Kul (681.64, -410.26): the waypoint icons' pixels on three
    // screenshots (research/client-memory.md §4, world map view).
    private static readonly MapView FarOut = new(681.64f, -410.26f, 1877.56f), Near = new(681.64f, -410.26f, 417.62f);

    [Theory]
    [InlineData(680, -418, 918, 538)]   // Gea Kul
    [InlineData(-236, 386, 1590, 494)]  // Zarbinzet
    [InlineData(1038, 152, 999, 900)]   // Kurast Bazaar
    public void Waypoints_land_on_their_icons_far_out(double x, double y, int iconX, int iconY)
    {
        var pixel = MapProjection.ToClient(FarOut, x, y, 1920, 1080);

        Assert.NotNull(pixel);
        Assert.InRange(pixel.Value.X, iconX - 8, iconX + 8);
        Assert.InRange(pixel.Value.Y, iconY - 8, iconY + 8);
    }

    [Fact]
    public void Gea_Kul_lands_on_its_icon_zoomed_in()
    {
        var pixel = MapProjection.ToClient(Near, 680, -418, 1920, 1080);

        Assert.NotNull(pixel);
        Assert.InRange(pixel.Value.X, 909 - 5, 909 + 5);
        Assert.InRange(pixel.Value.Y, 524 - 5, 524 + 5);
    }

    [Fact]
    public void A_point_off_the_map_area_has_no_pixel()
    {
        Assert.Null(MapProjection.ToClient(Near, -236, 386, 1920, 1080)); // Zarbinzet, far right of a near view
    }

    [Fact]
    public void The_projection_scales_with_the_client_height()
    {
        var pixel = MapProjection.ToClient(FarOut, -236, 386, 3840, 2160);

        Assert.NotNull(pixel);
        Assert.InRange(pixel.Value.X, 2 * 1590 - 16, 2 * 1590 + 16);
        Assert.InRange(pixel.Value.Y, 2 * 494 - 16, 2 * 494 + 16);
    }
}
