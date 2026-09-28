using D4MemScan.Scan;

namespace D4MemScan.Tests;

public class BearingFormatterTests
{
    // Engine axes (task spec): +X = south, +Y = east. North is therefore -X.
    [Theory]
    [InlineData(-1f, 0f, "N")]
    [InlineData(-1f, 1f, "NE")]
    [InlineData(0f, 1f, "E")]
    [InlineData(1f, 1f, "SE")]
    [InlineData(1f, 0f, "S")]
    [InlineData(1f, -1f, "SW")]
    [InlineData(0f, -1f, "W")]
    [InlineData(-1f, -1f, "NW")]
    public void FormatsEachOfTheEightCompassPoints(float deltaX, float deltaY, string expected)
    {
        Assert.Equal(expected, BearingFormatter.Format(deltaX, deltaY));
    }

    [Fact]
    public void ReturnsNullForNegligibleMovement()
    {
        Assert.Null(BearingFormatter.Format(0f, 0f));
        Assert.Null(BearingFormatter.Format(BearingFormatter.MinMovementUnits / 10f, 0f));
    }
}
