using System.Numerics;
using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

public class PathFollowerTests
{
    // East 20 units, then south 20.
    private static PathFollower L() => new([new(0, 0), new(10, 0), new(20, 0), new(20, -10), new(20, -20)]);

    [Fact]
    public void Aims_ahead_along_the_path_from_the_players_spot_on_it()
    {
        var (aim, off, remaining) = L().Follow(new(5, 2), lookahead: 8);

        Assert.Equal(new Vector2(13, 0), aim);
        Assert.Equal(2f, off, 3);
        Assert.Equal(35f, remaining, 3);
    }

    [Fact]
    public void Passing_a_point_aside_never_aims_back_at_it()
    {
        var follower = L();
        follower.Follow(new(5, 0), 8);

        // Walking past (10, 0) five units to the side: the aim stays ahead of the player.
        var (aim, _, _) = follower.Follow(new(12, 5), 8);

        Assert.True(aim.X > 12);
    }

    [Fact]
    public void The_aim_turns_the_corner_and_stops_at_the_end()
    {
        var follower = L();

        Assert.Equal(new Vector2(20, -5), follower.Follow(new(17, 0), 8).Aim);
        Assert.Equal(new Vector2(20, -20), follower.Follow(new(20, -18), 8).Aim);
    }

    [Fact]
    public void Off_path_is_the_distance_to_the_nearest_leg()
    {
        var (_, off, _) = L().Follow(new(10, 15), 8);

        Assert.Equal(15f, off, 3);
    }

    [Fact]
    public void A_path_that_doubles_back_does_not_jump_to_its_far_end()
    {
        // Out 20 units east and back 1 unit north of the way out: standing at the start, the far end is not where the player is.
        var follower = new PathFollower([new(0, 0), new(20, 0), new(20, 1), new(0, 1)]);

        var (aim, _, remaining) = follower.Follow(new(1, 0.6f), 5);

        Assert.Equal(new Vector2(6, 0), aim);
        Assert.InRange(remaining, 39f, 41f);
    }
}
