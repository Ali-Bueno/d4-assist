using D4Assist.Input;

namespace D4Assist.Tests;

public class XInputPadTests
{
    [Fact]
    public async Task Dispose_waits_for_a_poll_still_running()
    {
        using var inPoll = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var pad = new XInputPad(_ => { inPoll.Set(); release.Wait(); return null; });
        pad.Start();
        Assert.True(inPoll.Wait(TimeSpan.FromSeconds(5)));

        var disposing = Task.Run(pad.Dispose);
        Assert.NotSame(disposing, await Task.WhenAny(disposing, Task.Delay(200)));

        release.Set();
        Assert.Same(disposing, await Task.WhenAny(disposing, Task.Delay(TimeSpan.FromSeconds(5))));
    }

    [Fact]
    public void Dispose_without_start_or_twice_is_harmless()
    {
        var pad = new XInputPad(_ => null);
        pad.Dispose();
        pad.Dispose();
    }

    [Fact]
    public void A_short_press_fires_once_as_it_comes_back_up()
    {
        ushort?[] slots = [0, null, null, null];
        int presses = 0;
        using var pad = new XInputPad(i => slots[i], nowMs: () => 0);
        pad.DpadLeftPressed += () => presses++;

        pad.PollOnce();
        slots[0] = XInputPad.DpadLeftMask;
        pad.PollOnce();
        pad.PollOnce();
        Assert.Equal(0, presses); // down: a press or a hold, not known yet

        slots[0] = 0;
        pad.PollOnce();
        Assert.Equal(1, presses);
        slots[0] = XInputPad.DpadLeftMask;
        pad.PollOnce();
        slots[0] = 0;
        pad.PollOnce();
        Assert.Equal(2, presses);
    }

    [Fact]
    public void Held_for_a_second_it_fires_the_hold_once_and_its_release_is_no_press()
    {
        ushort?[] slots = [XInputPad.DpadLeftMask, null, null, null];
        long now = 0;
        int presses = 0, holds = 0;
        using var pad = new XInputPad(i => slots[i], nowMs: () => now);
        pad.DpadLeftPressed += () => presses++;
        pad.DpadLeftHeld += () => holds++;

        pad.PollOnce();
        now = PressHold.HoldMs - 1;
        pad.PollOnce();
        Assert.Equal(0, holds);
        now = PressHold.HoldMs;
        pad.PollOnce();
        now += 500;
        pad.PollOnce();
        slots[0] = 0;
        pad.PollOnce();

        Assert.Equal((0, 1), (presses, holds));
    }

    [Fact]
    public void Other_buttons_and_disconnects_do_not_fire_and_a_reconnect_starts_clean()
    {
        ushort?[] slots = [0x1000, null, XInputPad.DpadLeftMask, null];
        int presses = 0;
        using var pad = new XInputPad(i => slots[i], nowMs: () => 0);
        pad.DpadLeftPressed += () => presses++;

        pad.PollOnce(); // slot 2 down
        slots[2] = null;
        pad.PollOnce(); // disconnected while down: that press is dropped
        slots[2] = XInputPad.DpadLeftMask; // reconnected while still held: a fresh press
        pad.PollOnce();
        slots[2] = 0;
        pad.PollOnce();

        Assert.Equal(1, presses);
        Assert.Equal(0x0004, XInputPad.DpadLeftMask);
    }
}
