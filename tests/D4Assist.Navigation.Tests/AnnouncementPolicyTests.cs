using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class AnnouncementPolicyTests
{
    private static readonly PlayerInput Player = new(16, 16, 0);

    private static Room JunctionRoom(bool unexplored = true)
    {
        var tile = new MapTile(new TileInput(1, "t", 0, 0, 32, 32, 0, 1, 1));
        var openings = new[]
        {
            new RoomOpening(ExitSide.North, (0f, 16f), unexplored),
            new RoomOpening(ExitSide.South, (32f, 16f), unexplored),
            new RoomOpening(ExitSide.East, (16f, 32f), unexplored),
        };
        return new Room(new RoomKey(0, 0, true), new[] { tile }, openings);
    }

    private static Room CorridorRoom()
    {
        var tile = new MapTile(new TileInput(1, "t", 0, 0, 32, 32, 0, 1, 1));
        var openings = new[]
        {
            new RoomOpening(ExitSide.North, (0f, 16f), false),
            new RoomOpening(ExitSide.South, (32f, 16f), false),
        };
        return new Room(new RoomKey(0, 0, true), new[] { tile }, openings);
    }

    [Fact]
    public void Junction_is_announced_with_unexplored_openings_marked()
    {
        var policy = new AnnouncementPolicy();
        var text = policy.Describe(JunctionRoom(), Player).Text;

        Assert.NotNull(text);
        Assert.StartsWith("Cruce.", text);
        Assert.Contains("sin explorar", text);
    }

    [Fact]
    public void Corridors_are_silent_by_default_but_speak_when_enabled()
    {
        var policy = new AnnouncementPolicy();
        Assert.Null(policy.Describe(CorridorRoom(), Player).Text);

        policy.IncludeCorridors = true;
        Assert.NotNull(policy.Describe(CorridorRoom(), Player).Text);
    }

    [Fact]
    public void Disabled_policy_never_announces()
    {
        var policy = new AnnouncementPolicy { Enabled = false };
        Assert.Null(policy.Describe(JunctionRoom(), Player).Text);
    }

    [Fact]
    public void Mention_unexplored_toggle_drops_the_qualifier()
    {
        var policy = new AnnouncementPolicy { MentionUnexplored = false };
        var text = policy.Describe(JunctionRoom(unexplored: true), Player).Text;

        Assert.NotNull(text);
        Assert.DoesNotContain("sin explorar", text);
    }

    [Fact]
    public void Identical_consecutive_announcements_are_diff_gated()
    {
        var policy = new AnnouncementPolicy();
        var first = policy.Describe(JunctionRoom(), Player).Text;
        var second = policy.Describe(JunctionRoom(), Player).Text; // same room, same text

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void Reset_allows_the_same_text_to_be_announced_again()
    {
        var policy = new AnnouncementPolicy();
        policy.Describe(JunctionRoom(), Player);
        policy.Reset();

        Assert.NotNull(policy.Describe(JunctionRoom(), Player).Text);
    }

    [Fact]
    public void The_opening_the_player_came_through_is_excluded_from_the_spoken_list_but_kept_for_diagnostics()
    {
        var currentTile = new MapTile(new TileInput(1, "current", 0, 0, 32, 32, 0, 1, 1));
        var northOpening = new RoomOpening(ExitSide.North, (0f, 16f), Unexplored: false);
        var southOpening = new RoomOpening(ExitSide.South, (32f, 16f), Unexplored: false);
        var eastOpening = new RoomOpening(ExitSide.East, (16f, 32f), Unexplored: false);
        var room = new Room(new RoomKey(0, 0, true), new[] { currentTile }, new[] { northOpening, southOpening, eastOpening });

        // Sits just north of the current tile: the North opening's probe point (one unit past its
        // edge) lands inside it, so North is where the player just entered from.
        var cameFromTile = new MapTile(new TileInput(2, "from", -32, 0, 32, 32, 0, 1, 1));
        var cameFrom = new Room(new RoomKey(-32, 0, true), new[] { cameFromTile }, Array.Empty<RoomOpening>());

        var policy = new AnnouncementPolicy();
        var (text, routes) = policy.Describe(room, Player, cameFrom);

        Assert.NotNull(text);
        var northRoute = routes.Single(r => r.Opening == northOpening);
        var southRoute = routes.Single(r => r.Opening == southOpening);
        Assert.True(northRoute.IsEntry);
        Assert.False(southRoute.IsEntry);
        Assert.DoesNotContain(SpanishPhrases.Direction(northRoute.Direction), text);
        Assert.Contains(SpanishPhrases.Direction(southRoute.Direction), text);
    }

    [Fact]
    public void A_non_dead_end_room_with_no_spoken_openings_left_says_no_other_exits()
    {
        var tileA = new MapTile(new TileInput(1, "a", 0, 0, 32, 32, ExitMaskBits.North, 10, 10));
        var tileB = new MapTile(new TileInput(2, "b", 0, 32, 32, 32, 0, 10, 10)); // second member tile -> BigRoom
        var opening = new RoomOpening(ExitSide.North, (0f, 16f), Unexplored: false);
        var room = new Room(new RoomKey(0, 0, true), new[] { tileA, tileB }, new[] { opening });

        var cameFromTile = new MapTile(new TileInput(3, "from", -32, 0, 32, 32, 0, 1, 1));
        var cameFrom = new Room(new RoomKey(-32, 0, true), new[] { cameFromTile }, Array.Empty<RoomOpening>());

        var text = new AnnouncementPolicy().Describe(room, Player, cameFrom).Text;

        Assert.Equal("Sala grande. Sin otras salidas.", text);
    }

    [Fact]
    public void An_entrance_room_never_reads_as_a_dead_end()
    {
        var tile = new MapTile(new TileInput(1, "entrance", 0, 0, 32, 32, ExitMaskBits.North, 1, 1));
        var opening = new RoomOpening(ExitSide.North, (0f, 16f), Unexplored: false); // 1 opening -> DeadEnd normally

        var text = new AnnouncementPolicy().Describe(new Room(new RoomKey(0, 0, true), new[] { tile }, new[] { opening }),
            Player, cameFrom: null, isEntrance: true).Text;

        var direction = ScreenDirections.Bin(opening.Position.X - Player.X, opening.Position.Y - Player.Y);
        Assert.Equal($"Entrada. Salida: {SpanishPhrases.Direction(direction)}.", text);
    }
}
