namespace D4Assist.Navigation;

/// <summary>The ground a warning marks: a circle around its spot, a cone or a line out along its facing, or a ring; or a hazard
/// on the move: a wave (a wall sweeping the room along its facing) or a mover (a tornado drifting along its facing).</summary>
public enum TelegraphShape { Circle, Cone, Line, Ring, Wave, Mover }

/// <summary>
/// Which way to step out of a boss attack's warning, as a sighted player reads the marked ground (research/boss-telegraphs.md):
/// out of a circle the shortest way (away from the boss when the warning sits on the player), sideways out of a cone or line,
/// into a ring's middle. A direction whose path runs into a wall gives way to the next nearest open one.
/// </summary>
public static class DodgePlanner
{
    /// <summary>How far a circle's warning reaches: the game scales its unit-sized effects, so the definition can't tell. Live
    /// 2026-10-23 (the Horadric guardian's spirit bombs): hits landed within 1 unit, misses 6.5 units off.</summary>
    public const float CircleRadiusUnits = 6f;

    /// <summary>How wide a line or cone is taken to be on each side of its axis, and how far ahead it reaches.</summary>
    public const float LineHalfWidthUnits = 3f, LineReachUnits = 25f;

    /// <summary>How far a wave sweeps from where it rose: the Horadric guardian's wall (Horadric_Guardian_wall_demonic, 13 segments
    /// across the room) moved 12 units in 5 s at ~2.2 units/s (live 2026-10-23). It can't be passed, only outrun.</summary>
    public const float WaveReachUnits = 14f;

    /// <summary>A mover (the guardian's tornadoes, ~2 units/s) is dodged when it comes within this, ahead of it.</summary>
    public const float MoverHalfWidthUnits = 3f, MoverReachUnits = 8f;

    /// <summary>Extra ground to clear beyond the marked edge.</summary>
    public const float MarginUnits = 1.5f;

    // The eight stick directions in engine axes (ScreenDirections' sectors), clockwise from up.
    private static readonly (double X, double Y)[] Directions =
        [(-1, -1), (-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1)];

    /// <summary>The way to step, or null when the player stands clear of it.</summary>
    /// <param name="center">The warning's spot (engine axes).</param>
    /// <param name="yaw">Its facing in radians (engine axes): the axis of a cone or line.</param>
    /// <param name="boss">The attacker, when known: a circle on the player is left away from it.</param>
    /// <param name="blocked">True for a world point a step can't cross (a wall, a closed door); null for open ground everywhere.</param>
    public static StickDirection? Plan(
        TelegraphShape shape, (float X, float Y) center, float yaw, (float X, float Y) player, (float X, float Y)? boss = null,
        Func<float, float, bool>? blocked = null)
    {
        double dx = player.X - center.X, dy = player.Y - center.Y, distance = Math.Sqrt(dx * dx + dy * dy);
        (double X, double Y) want;
        double clear;
        switch (shape)
        {
            case TelegraphShape.Ring:
                // The ring burns around its middle: step in.
                if (distance < 1) return null;
                want = (-dx, -dy);
                clear = distance;
                break;
            case TelegraphShape.Wave:
            {
                // Ahead of the wall and within its sweep: run on ahead of it, out of its reach.
                double ax = Math.Cos(yaw), ay = Math.Sin(yaw), along = dx * ax + dy * ay;
                if (along < -1 || along > WaveReachUnits) return null;
                want = (ax, ay);
                clear = WaveReachUnits - along + MarginUnits;
                break;
            }
            case TelegraphShape.Mover:
            {
                double ax = Math.Cos(yaw), ay = Math.Sin(yaw), along = dx * ax + dy * ay, side = -dx * ay + dy * ax;
                if (along < -MoverHalfWidthUnits || along > MoverReachUnits || Math.Abs(side) > MoverHalfWidthUnits) return null;
                double sign = side >= 0 ? 1 : -1;
                want = (-ay * sign, ax * sign);
                clear = MoverHalfWidthUnits - Math.Abs(side) + MarginUnits;
                break;
            }
            case TelegraphShape.Cone or TelegraphShape.Line:
            {
                double ax = Math.Cos(yaw), ay = Math.Sin(yaw);
                double along = dx * ax + dy * ay, side = -dx * ay + dy * ax;
                if (along < -MarginUnits || along > LineReachUnits || Math.Abs(side) > LineHalfWidthUnits + (shape == TelegraphShape.Cone ? along * 0.5 : 0))
                    return null;
                double sign = side >= 0 ? 1 : -1;
                want = (-ay * sign, ax * sign);
                clear = LineHalfWidthUnits - Math.Abs(side) + MarginUnits;
                break;
            }
            default:
                if (distance > CircleRadiusUnits) return null;
                want = distance >= 0.5 ? (dx, dy)
                    : boss is { } b && Math.Abs(player.X - b.X) + Math.Abs(player.Y - b.Y) > 0.5 ? (player.X - b.X, player.Y - b.Y)
                    : (-1, -1); // on the spot with no attacker known: any way out will do
                clear = CircleRadiusUnits - distance + MarginUnits;
                break;
        }
        return Best(want, player, clear, blocked);
    }

    // The direction nearest the wanted one whose path stays open for the distance to clear.
    private static StickDirection Best((double X, double Y) want, (float X, float Y) from, double clear, Func<float, float, bool>? blocked)
    {
        double wantAngle = Math.Atan2(want.Y, want.X);
        var order = Enumerable.Range(0, Directions.Length)
            .OrderBy(i => AngleBetween(Math.Atan2(Directions[i].Y, Directions[i].X), wantAngle)).ToList();
        foreach (int i in order)
            if (blocked is null || IsOpen(Directions[i], from, clear, blocked)) return (StickDirection)i;
        return (StickDirection)order[0];
    }

    private static bool IsOpen((double X, double Y) direction, (float X, float Y) from, double clear, Func<float, float, bool> blocked)
    {
        double length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        for (double step = 1; step <= clear; step += 1)
            if (blocked((float)(from.X + direction.X / length * step), (float)(from.Y + direction.Y / length * step))) return false;
        return true;
    }

    private static double AngleBetween(double a, double b)
    {
        double d = Math.Abs(a - b) % (2 * Math.PI);
        return d > Math.PI ? 2 * Math.PI - d : d;
    }
}
