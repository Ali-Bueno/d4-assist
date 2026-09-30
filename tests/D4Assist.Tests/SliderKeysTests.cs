using System.Windows.Forms;
using D4Assist.Ui;

namespace D4Assist.Tests;

public class SliderKeysTests
{
    [Theory]
    [InlineData(Keys.Up, 50, 51)]
    [InlineData(Keys.Down, 50, 49)]
    [InlineData(Keys.Right, 50, 55)]
    [InlineData(Keys.Left, 50, 45)]
    [InlineData(Keys.PageUp, 50, 60)]
    [InlineData(Keys.PageDown, 50, 40)]
    [InlineData(Keys.Home, 50, 100)]
    [InlineData(Keys.End, 50, 0)]
    public void Each_key_moves_the_value(Keys key, int value, int expected) =>
        Assert.Equal(expected, SliderKeys.Next(key, value, 0, 100));

    [Theory]
    [InlineData(Keys.Up, 100, 100)]
    [InlineData(Keys.Right, 98, 100)]
    [InlineData(Keys.PageUp, 95, 100)]
    [InlineData(Keys.Down, 0, 0)]
    [InlineData(Keys.Left, 3, 0)]
    [InlineData(Keys.PageDown, 4, 0)]
    public void The_value_stops_at_the_ends(Keys key, int value, int expected) =>
        Assert.Equal(expected, SliderKeys.Next(key, value, 0, 100));

    [Fact]
    public void Home_and_End_follow_the_range()
    {
        Assert.Equal(300, SliderKeys.Next(Keys.Home, 120, 25, 300));
        Assert.Equal(25, SliderKeys.Next(Keys.End, 120, 25, 300));
    }

    [Theory]
    [InlineData(Keys.A)]
    [InlineData(Keys.Enter)]
    [InlineData(Keys.Tab)]
    [InlineData(Keys.None)]
    [InlineData(Keys.Up | Keys.Control)] // a modifier is not a key of its own here: the control checks modifiers
    public void Other_keys_are_not_slider_keys(Keys key) => Assert.Null(SliderKeys.Next(key, 50, 0, 100));
}
