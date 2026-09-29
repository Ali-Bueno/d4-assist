using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

// Worked examples pinned at the adapter boundary (research/client-memory.md §3
// calibration; see the CLAUDE.md task brief's own two examples).
public class ScreenDirectionsTests
{
    [Fact]
    public void Offset_matching_the_stick_up_vector_bins_to_up()
    {
        // "an opening at player + (-10,-10) is 'up'"
        Assert.Equal(StickDirection.Up, ScreenDirections.Bin(-10, -10));
    }

    [Fact]
    public void A_tile_north_side_normal_bins_to_up_right()
    {
        // "tile N side = 'up-right'" — N's outward normal is (-1, 0) in engine axes.
        Assert.Equal(StickDirection.UpRight, ScreenDirections.Bin(-1, 0));
    }

    [Fact]
    public void A_tile_south_side_normal_bins_to_down_left()
    {
        Assert.Equal(StickDirection.DownLeft, ScreenDirections.Bin(1, 0));
    }

    [Fact]
    public void A_tile_east_side_normal_bins_to_down_right()
    {
        Assert.Equal(StickDirection.DownRight, ScreenDirections.Bin(0, 1));
    }

    [Fact]
    public void A_tile_west_side_normal_bins_to_up_left()
    {
        Assert.Equal(StickDirection.UpLeft, ScreenDirections.Bin(0, -1));
    }

    [Theory]
    [InlineData(-1, -1, StickDirection.Up)]
    [InlineData(-1, 1, StickDirection.Right)]
    [InlineData(1, 1, StickDirection.Down)]
    [InlineData(1, -1, StickDirection.Left)]
    public void Stick_cardinal_calibration_vectors_bin_to_their_own_direction(double dx, double dy, StickDirection expected)
    {
        Assert.Equal(expected, ScreenDirections.Bin(dx, dy));
    }

    [Fact]
    public void Zero_offset_falls_back_to_up_without_throwing()
    {
        Assert.Equal(StickDirection.Up, ScreenDirections.Bin(0, 0));
    }

    [Theory]
    [InlineData(-1, -1, 0, 1)]   // stick up
    [InlineData(-1, 1, 1, 0)]    // stick right
    [InlineData(1, 1, 0, -1)]    // stick down
    [InlineData(1, -1, -1, 0)]   // stick left
    public void ToScreen_maps_the_calibration_vectors_to_unit_screen_axes(double dx, double dy, double right, double up)
    {
        var (r, u) = ScreenDirections.ToScreen(dx, dy);

        Assert.Equal(right * Math.Sqrt(2), r, 9);
        Assert.Equal(up * Math.Sqrt(2), u, 9);
    }

    [Fact]
    public void ToScreen_preserves_distance()
    {
        var (r, u) = ScreenDirections.ToScreen(3, 4);

        Assert.Equal(5, Math.Sqrt(r * r + u * u), 9);
    }
}
