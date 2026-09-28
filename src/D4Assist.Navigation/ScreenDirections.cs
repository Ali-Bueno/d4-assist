namespace D4Assist.Navigation;

/// <summary>
/// Converts a world-space offset (engine axes X=south, Y=east) to the controller
/// stick frame. This is D2R's `Diagonal` frame problem (radar.h:24-54): the stick's
/// own cardinals sit 45 degrees off the world axes, so a tile's N/S/E/W exit reads
/// as a screen diagonal. Calibration source: research/client-memory.md §3, four
/// live stick moves — stick up = (-1,-1), right = (-1,+1), down = (+1,+1),
/// left = (+1,-1) in engine (X, Y). The four diagonals fall out of summing adjacent
/// cardinals, the same 45-degree rotation D2R's `Rays` table encodes (radar.h:52-54).
/// </summary>
public static class ScreenDirections
{
    private static readonly (StickDirection Dir, double X, double Y)[] Sectors =
    {
        (StickDirection.Up, -1, -1),
        (StickDirection.UpRight, -1, 0),
        (StickDirection.Right, -1, 1),
        (StickDirection.DownRight, 0, 1),
        (StickDirection.Down, 1, 1),
        (StickDirection.DownLeft, 1, 0),
        (StickDirection.Left, 1, -1),
        (StickDirection.UpLeft, 0, -1),
    };

    /// <summary>
    /// Bins an engine-space (dx, dy) offset into the nearest of the 8 stick sectors.
    /// A zero vector (target at the player's own position) falls back to Up.
    /// </summary>
    public static StickDirection Bin(double engineDx, double engineDy)
    {
        if (engineDx == 0 && engineDy == 0) return StickDirection.Up;

        var best = StickDirection.Up;
        var bestScore = double.NegativeInfinity;
        foreach (var (dir, sx, sy) in Sectors)
        {
            // Cosine-similarity numerator against each sector's unit direction; the
            // offset's own magnitude is constant across sectors, so it drops out of the argmax.
            var norm = Math.Sqrt(sx * sx + sy * sy);
            var score = (engineDx * sx + engineDy * sy) / norm;
            if (score > bestScore) { bestScore = score; best = dir; }
        }
        return best;
    }
}
