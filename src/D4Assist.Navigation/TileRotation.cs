namespace D4Assist.Navigation;

/// <summary>
/// Maps a tile's yaw to a whole number of 90-degree turns, and rotates local offsets/sides by it.
/// All DRLG tiles seen so far carry an identity quaternion (research/client-memory.md §3, "Tiles so
/// far are unrotated"); this exists for the day a rotated tile shows up, and is unverified against a
/// live rotated tile. Rotation direction is only internally consistent (Rotate and RotateSide agree
/// with each other); callers that need the physical world direction must re-check it once a rotated
/// tile is observed.
/// </summary>
public static class TileRotation
{
    /// <summary>Slack around a 0/90/180/270 multiple, in degrees (float round-trip noise).</summary>
    public const float ToleranceDegrees = 1f;

    /// <summary>Yaw from a scene quaternion's (z, w), same convention as the actor heading
    /// (client-memory.md §3: yaw measured from +X towards +Y, quat = (0,0,sin(yaw/2),cos(yaw/2))).</summary>
    public static float YawDegrees(float quaternionZ, float quaternionW) =>
        2f * MathF.Atan2(quaternionZ, quaternionW) * (180f / MathF.PI);

    /// <summary>Nearest quarter-turn count (0-3), or -1 when yaw isn't within tolerance of a 90-degree
    /// multiple — callers must then fall back to the unrotated behaviour (task spec).</summary>
    public static int QuarterTurns(float yawDegrees)
    {
        var normalized = ((yawDegrees % 360f) + 360f) % 360f;
        var nearest = MathF.Round(normalized / 90f) * 90f;
        if (MathF.Abs(normalized - nearest) > ToleranceDegrees) return -1;
        return (int)(MathF.Round(nearest / 90f)) % 4;
    }

    /// <summary>Rotates a local (x, y) offset by <paramref name="turns"/> quarter turns.</summary>
    public static (float X, float Y) Rotate(float x, float y, int turns) => ((turns % 4 + 4) % 4) switch
    {
        0 => (x, y),
        1 => (-y, x),
        2 => (-x, -y),
        _ => (y, -x),
    };

    /// <summary>World point of a tile-local point: turned by <paramref name="turns"/>, then placed at the tile's origin.</summary>
    internal static (float X, float Y) ToWorld(TileInput tile, (float X, float Y) local, int turns)
    {
        var (rx, ry) = Rotate(local.X, local.Y, turns);
        return (tile.OriginX + rx, tile.OriginY + ry);
    }

    /// <summary>The inverse of <see cref="ToWorld"/>.</summary>
    internal static (float X, float Y) ToLocal(TileInput tile, (float X, float Y) world, int turns) =>
        Rotate(world.X - tile.OriginX, world.Y - tile.OriginY, -turns);

    /// <summary>
    /// The world-facing side after rotating a tile whose exit sides are authored in local axes by
    /// <paramref name="turns"/> quarter turns. Cycle derived from <see cref="Rotate"/> applied to each
    /// side's outward normal: North(-1,0) -> West(0,-1) -> South(1,0) -> East(0,1) -> North.
    /// </summary>
    public static ExitSide RotateSide(ExitSide side, int turns)
    {
        ExitSide[] cycle = [ExitSide.North, ExitSide.West, ExitSide.South, ExitSide.East];
        var index = Array.IndexOf(cycle, side);
        return cycle[(index + (turns % 4 + 4) % 4) % 4];
    }
}
