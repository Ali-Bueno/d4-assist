using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class NavigationEngineTests
{
    private static readonly TileInput JunctionTile =
        new(1, "junction", 0, 0, 32, 32, ExitMaskBits.North | ExitMaskBits.South | ExitMaskBits.East, 1, 1);
    private static readonly TileInput CorridorTile =
        new(2, "corridor", 0, 0, 32, 32, ExitMaskBits.North | ExitMaskBits.South, 1, 1);
    private static readonly PlayerInput InTile = new(16, 16, 0);
    private static readonly PlayerInput OffGrid = new(1000, 1000, 0);

    [Fact]
    public void Entering_the_first_room_raises_one_entrance_announcement()
    {
        // The first room accepted after construction/Reset is always the entrance, regardless of its
        // shape (bug fix: an entrance tile shaped like a dead end/junction must not read as one).
        var engine = new NavigationEngine();
        var announcements = new List<(string Text, bool Interrupt)>();
        engine.Announce += (text, interrupt) => announcements.Add((text, interrupt));

        engine.OnTiles(new[] { JunctionTile });
        engine.OnPlayer(InTile, TimeSpan.Zero);

        Assert.Single(announcements);
        Assert.StartsWith("Entrada.", announcements[0].Text);
        Assert.False(announcements[0].Interrupt); // default: never cut the game's own speech
    }

    [Fact]
    public void Staying_in_the_same_room_does_not_repeat_the_announcement()
    {
        var engine = new NavigationEngine();
        var count = 0;
        engine.Announce += (_, _) => count++;

        engine.OnTiles(new[] { JunctionTile });
        engine.OnPlayer(InTile, TimeSpan.Zero);
        engine.OnPlayer(InTile, TimeSpan.FromMilliseconds(50));
        engine.OnPlayer(InTile, TimeSpan.FromMilliseconds(2000));

        Assert.Equal(1, count);
    }

    [Fact]
    public void A_corridor_is_silent_by_default()
    {
        var engine = new NavigationEngine();
        var count = 0;
        engine.Announce += (_, _) => count++;

        engine.OnTiles(new[] { CorridorTile });
        engine.OnPlayer(InTile, TimeSpan.Zero);

        Assert.Equal(0, count);
    }

    [Fact]
    public void An_off_grid_player_raises_nothing_and_does_not_throw()
    {
        var engine = new NavigationEngine();
        var count = 0;
        engine.Announce += (_, _) => count++;

        engine.OnTiles(new[] { JunctionTile });
        engine.OnPlayer(OffGrid, TimeSpan.Zero);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Reset_clears_the_map_and_diff_gate_so_the_same_room_announces_again()
    {
        var engine = new NavigationEngine();
        var count = 0;
        engine.Announce += (_, _) => count++;

        engine.OnTiles(new[] { JunctionTile });
        engine.OnPlayer(InTile, TimeSpan.Zero);
        Assert.Equal(1, count);

        engine.Reset();
        engine.OnTiles(new[] { JunctionTile });
        engine.OnPlayer(InTile, TimeSpan.Zero);

        Assert.Equal(2, count);
    }

    [Fact]
    public void Interrupt_setting_is_forwarded_to_the_announce_event()
    {
        var engine = new NavigationEngine { Interrupt = true };
        var interrupts = new List<bool>();
        engine.Announce += (_, interrupt) => interrupts.Add(interrupt);

        engine.OnTiles(new[] { JunctionTile });
        engine.OnPlayer(InTile, TimeSpan.Zero);

        Assert.Equal(new[] { true }, interrupts);
    }

    [Fact]
    public void Oscillating_a_short_distance_across_a_room_boundary_does_not_flap()
    {
        var engine = new NavigationEngine();
        var count = 0;
        engine.Announce += (_, _) => count++;

        var roomA = new TileInput(1, "a", 0, 0, 32, 32, 0, 1, 1);
        var roomB = new TileInput(2, "b", 32, 0, 32, 32, 0, 2, 2);
        engine.OnTiles(new[] { roomA, roomB });

        engine.OnPlayer(new PlayerInput(16, 16, 0), TimeSpan.Zero); // enter A: first room, announced
        Assert.Equal(1, count);

        // Step 1 unit into B, back into A, and into B again — all within the 4-unit stickiness margin.
        engine.OnPlayer(new PlayerInput(33, 16, 0), TimeSpan.FromMilliseconds(100));
        engine.OnPlayer(new PlayerInput(31, 16, 0), TimeSpan.FromMilliseconds(200));
        engine.OnPlayer(new PlayerInput(33, 16, 0), TimeSpan.FromMilliseconds(300));
        Assert.Equal(1, count); // no flapping

        // 5 units past the boundary, well outside the margin, after the hysteresis has settled:
        // a real switch into B.
        engine.OnPlayer(new PlayerInput(37, 16, 0), TimeSpan.FromMilliseconds(1200));
        Assert.Equal(2, count);
    }

    [Fact]
    public void A_room_that_grows_a_new_member_tile_rekeys_silently_without_announcing()
    {
        var engine = new NavigationEngine();
        var count = 0;
        engine.Announce += (_, _) => count++;

        var tileA = new TileInput(1, "a", 32, 0, 32, 32, 0, 5, 5);
        engine.OnTiles(new[] { tileA });
        engine.OnPlayer(new PlayerInput(48, 16, 0), TimeSpan.Zero);
        Assert.Equal(1, count); // first room: entrance announcement

        // A same-NodeId neighbour tile loads: the merged room's anchor (lowest-origin tile) becomes
        // this new tile, changing the room's key even though the player never left it.
        var tileC = new TileInput(2, "c", 0, 0, 32, 32, 0, 5, 5);
        engine.OnTiles(new[] { tileA, tileC });
        engine.OnPlayer(new PlayerInput(48, 16, 0), TimeSpan.FromMilliseconds(100));

        Assert.Equal(1, count); // rekeyed in place, not announced as a room change
    }
}
