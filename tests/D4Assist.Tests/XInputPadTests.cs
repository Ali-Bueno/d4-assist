using D4Assist.Input;

namespace D4Assist.Tests;

public class XInputPadTests
{
    [Fact]
    public void Fires_on_the_press_edge_only_not_while_held()
    {
        ushort?[] slots = [0, null, null, null];
        int presses = 0;
        using var pad = new XInputPad(i => slots[i]);
        pad.DpadLeftPressed += () => presses++;

        pad.PollOnce();
        slots[0] = XInputPad.DpadLeftMask;
        pad.PollOnce();
        pad.PollOnce();
        Assert.Equal(1, presses);

        slots[0] = 0;
        pad.PollOnce();
        slots[0] = XInputPad.DpadLeftMask;
        pad.PollOnce();
        Assert.Equal(2, presses);
    }

    [Fact]
    public void Other_buttons_and_disconnects_do_not_fire_and_a_reconnect_starts_clean()
    {
        ushort?[] slots = [0x1000, null, XInputPad.DpadLeftMask, null];
        int presses = 0;
        using var pad = new XInputPad(i => slots[i]);
        pad.DpadLeftPressed += () => presses++;

        pad.PollOnce(); // slot 2 pressed
        slots[2] = null;
        pad.PollOnce();
        slots[2] = XInputPad.DpadLeftMask; // reconnected while still held: a fresh press
        pad.PollOnce();

        Assert.Equal(2, presses);
        Assert.Equal(0x0004, XInputPad.DpadLeftMask);
    }
}
