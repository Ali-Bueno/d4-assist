using D4Assist.Navigation;

namespace D4Assist.Navigation.Tests;

// Engine axes: stick up = (-1,-1), right = (-1,+1), down = (+1,+1), left = (+1,-1) (ScreenDirections).
public class DodgePlannerTests
{
    [Fact]
    public void Out_of_a_circle_the_shortest_way()
    {
        // The warning 2 units "up" of the player: step down, away from it.
        Assert.Equal(StickDirection.Down, DodgePlanner.Plan(TelegraphShape.Circle, (-1.4f, -1.4f), 0, (0, 0)));
    }

    [Fact]
    public void A_circle_on_the_player_is_left_away_from_the_boss()
    {
        // Live 2026-10-23: the guardian's spirit warning lands right under the player.
        Assert.Equal(StickDirection.Right, DodgePlanner.Plan(TelegraphShape.Circle, (0, 0), 0, (0, 0), boss: (3, -3)));
    }

    [Fact]
    public void A_wall_on_the_way_out_gives_way_to_the_next_open_direction()
    {
        // Down is a wall (every point with X+Y > 1 on the down side): the nearest open neighbour is taken instead.
        static bool Wall(float x, float y) => x > 1 && y > 1;
        var dodge = DodgePlanner.Plan(TelegraphShape.Circle, (-1.4f, -1.4f), 0, (0, 0), blocked: Wall);
        Assert.Contains(dodge, new StickDirection?[] { StickDirection.DownLeft, StickDirection.DownRight });
    }

    [Fact]
    public void Clear_of_a_circle_needs_no_step() =>
        Assert.Null(DodgePlanner.Plan(TelegraphShape.Circle, (20, 20), 0, (0, 0)));

    [Fact]
    public void Out_of_a_line_sideways_toward_the_side_already_taken()
    {
        // A line from (0,0) along +X (screen down-left); the player 1 unit to its +Y side (screen down-right of the axis).
        var dodge = DodgePlanner.Plan(TelegraphShape.Line, (0, 0), 0, (5, 1));
        Assert.Equal(StickDirection.DownRight, dodge);
        Assert.Null(DodgePlanner.Plan(TelegraphShape.Line, (0, 0), 0, (5, 10))); // well to the side
        Assert.Null(DodgePlanner.Plan(TelegraphShape.Line, (0, 0), 0, (-10, 0))); // behind it
    }

    [Fact]
    public void Into_a_rings_middle() =>
        Assert.Equal(StickDirection.Up, DodgePlanner.Plan(TelegraphShape.Ring, (0, 0), 0, (4, 4)));

    [Fact]
    public void Ahead_of_a_wave_run_on_ahead_out_of_its_sweep()
    {
        // Live 2026-10-23: the guardian's wall rose at y=24 and swept toward -Y (yaw -88 degrees), 12 units in 5 s.
        float yaw = -MathF.PI / 2;
        Assert.Equal(StickDirection.UpLeft, DodgePlanner.Plan(TelegraphShape.Wave, (16, 24), yaw, (18, 20)));
        Assert.Null(DodgePlanner.Plan(TelegraphShape.Wave, (16, 24), yaw, (18, 27))); // behind it
        Assert.Null(DodgePlanner.Plan(TelegraphShape.Wave, (16, 24), yaw, (18, 5))); // beyond its reach
    }

    [Fact]
    public void A_mover_coming_on_is_left_sideways()
    {
        // A tornado at (0,0) drifting toward +X (screen down-left), the player 4 units ahead and a little to its +Y side.
        Assert.Equal(StickDirection.DownRight, DodgePlanner.Plan(TelegraphShape.Mover, (0, 0), 0, (4, 1)));
        Assert.Null(DodgePlanner.Plan(TelegraphShape.Mover, (0, 0), 0, (20, 0))); // far ahead yet
    }
}
