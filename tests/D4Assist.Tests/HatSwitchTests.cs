using D4Assist.Input;

namespace D4Assist.Tests;

public class HatSwitchTests
{
    [Theory]
    // An Xbox pad's HID side: 1 = up, clockwise to 8 = up-left, 0 = centred.
    [InlineData(6, 1, 8, true)]   // down-left
    [InlineData(7, 1, 8, true)]   // left
    [InlineData(8, 1, 8, true)]   // up-left
    [InlineData(0, 1, 8, false)]  // centred
    [InlineData(1, 1, 8, false)]  // up
    [InlineData(3, 1, 8, false)]  // right
    [InlineData(5, 1, 8, false)]  // down
    // DirectInput pads: 0 = up, clockwise to 7, and 8 or 15 centred.
    [InlineData(6, 0, 7, true)]
    [InlineData(8, 0, 7, false)]
    [InlineData(15, 0, 7, false)]
    [InlineData(4, 0, 7, false)]
    // A four-way hat: 3 = left.
    [InlineData(3, 0, 3, true)]
    [InlineData(2, 0, 3, false)]
    public void Left_is_left_up_left_or_down_left(long value, int min, int max, bool left) =>
        Assert.Equal(left, HatSwitch.IsLeft(value, min, max));
}
