using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class TileRotationTests
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(90f, 1)]
    [InlineData(180f, 2)]
    [InlineData(270f, 3)]
    [InlineData(-90f, 3)]
    [InlineData(360f, 0)]
    public void Axis_aligned_yaw_maps_to_its_quarter_turn_count(float yaw, int expectedTurns) =>
        Assert.Equal(expectedTurns, TileRotation.QuarterTurns(yaw));

    [Theory]
    [InlineData(45f)]
    [InlineData(30f)]
    public void Non_axis_aligned_yaw_signals_fallback(float yaw) =>
        Assert.Equal(-1, TileRotation.QuarterTurns(yaw));

    [Fact]
    public void Four_quarter_turns_are_the_identity()
    {
        var (x, y) = TileRotation.Rotate(3f, 5f, 4);
        Assert.Equal((3f, 5f), (x, y));
    }

    [Fact]
    public void RotateSide_follows_the_cycle_derived_from_Rotate()
    {
        Assert.Equal(ExitSide.West, TileRotation.RotateSide(ExitSide.North, 1));
        Assert.Equal(ExitSide.South, TileRotation.RotateSide(ExitSide.North, 2));
        Assert.Equal(ExitSide.East, TileRotation.RotateSide(ExitSide.North, 3));
        Assert.Equal(ExitSide.North, TileRotation.RotateSide(ExitSide.North, 4));
    }

    [Fact]
    public void RotateSide_is_the_inverse_of_itself_over_four_minus_turns()
    {
        foreach (var side in new[] { ExitSide.North, ExitSide.East, ExitSide.South, ExitSide.West })
            foreach (var turns in new[] { 0, 1, 2, 3 })
                Assert.Equal(side, TileRotation.RotateSide(TileRotation.RotateSide(side, turns), (4 - turns) % 4));
    }
}
