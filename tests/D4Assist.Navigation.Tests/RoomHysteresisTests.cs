using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

// Ported semantics from D2R's Rooms::Hysteresis (rooms.h:42-54): settleMs=1000,
// dwellMs=400. A flip must hold dwellMs unless the current room is unsettled
// (first room, or entered less than settleMs ago) — see class doc for why that's
// unchanged for D4.
public class RoomHysteresisTests
{
    private static readonly RoomKey RoomA = new(0, 0, true);
    private static readonly RoomKey RoomB = new(100, 0, true);

    [Fact]
    public void The_first_room_is_announced_immediately()
    {
        var h = new RoomHysteresis();
        Assert.True(h.Observe(RoomA, 0));
    }

    [Fact]
    public void Border_jitter_that_reverts_before_the_dwell_window_does_not_announce()
    {
        var h = new RoomHysteresis();
        Assert.True(h.Observe(RoomA, 0));

        // Player nudges across the border and immediately back, well inside dwellMs.
        Assert.False(h.Observe(RoomB, 100));
        Assert.False(h.Observe(RoomA, 150)); // back in A: A is already current, no announcement
    }

    [Fact]
    public void A_flip_that_holds_the_dwell_window_is_announced_once()
    {
        var h = new RoomHysteresis();
        h.Observe(RoomA, 0);

        Assert.False(h.Observe(RoomB, 100)); // candidate starts
        Assert.False(h.Observe(RoomB, 400)); // still short of 400ms dwell since candidacy at 100
        Assert.True(h.Observe(RoomB, 500));  // 500 - 100 = 400ms held -> confirmed
    }

    [Fact]
    public void After_the_settle_window_a_new_room_is_announced_without_waiting_for_dwell()
    {
        var h = new RoomHysteresis();
        h.Observe(RoomA, 0);

        // 1200ms since entering A: settled, so the very next observed change speaks at once.
        Assert.True(h.Observe(RoomB, 1200));
    }

    [Fact]
    public void Losing_the_room_keeps_the_current_one_not_knowing_is_not_leaving()
    {
        var h = new RoomHysteresis();
        h.Observe(RoomA, 0);
        Assert.False(h.Observe(RoomKey.Invalid, 100));
    }

    [Fact]
    public void Reset_forgets_the_current_room_so_the_next_observation_announces_again()
    {
        var h = new RoomHysteresis();
        h.Observe(RoomA, 0);
        h.Reset();
        Assert.True(h.Observe(RoomA, 10));
    }
}
