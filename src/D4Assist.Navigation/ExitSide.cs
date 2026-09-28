namespace D4Assist.Navigation;

/// <summary>The four tile sides, in world/engine axes (not screen axes).</summary>
public enum ExitSide { North, East, South, West }

/// <summary>
/// Exit mask bits and per-side geometry for a DRLG tile. Bit values and side-axis
/// mapping are the memory reader's own convention (client-memory.md §1, a-44;
/// TileInput doc: N=1 S=2 E=4 W=8, N/S run along X, E/W along Y).
/// </summary>
public static class ExitMaskBits
{
    public const int North = 1; // tile's -X face
    public const int South = 2; // tile's +X face
    public const int East = 4;  // tile's +Y face
    public const int West = 8;  // tile's -Y face

    private static readonly (ExitSide Side, int Bit)[] All =
    {
        (ExitSide.North, North),
        (ExitSide.East, East),
        (ExitSide.South, South),
        (ExitSide.West, West),
    };

    /// <summary>Every side whose bit is set in <paramref name="mask"/>.</summary>
    public static IEnumerable<ExitSide> SidesOf(int mask)
    {
        foreach (var (side, bit) in All)
            if ((mask & bit) != 0) yield return side;
    }

    /// <summary>Outward unit normal of a side, in engine (X, Y) axes.</summary>
    public static (float X, float Y) Normal(ExitSide side) => side switch
    {
        ExitSide.North => (-1f, 0f),
        ExitSide.South => (1f, 0f),
        ExitSide.East => (0f, 1f),
        ExitSide.West => (0f, -1f),
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    /// <summary>Midpoint of the tile's edge on <paramref name="side"/>, in world units.</summary>
    public static (float X, float Y) EdgeMidpoint(TileInput tile, ExitSide side) => side switch
    {
        ExitSide.North => (tile.OriginX, tile.OriginY + tile.SizeY / 2f),
        ExitSide.South => (tile.OriginX + tile.SizeX, tile.OriginY + tile.SizeY / 2f),
        ExitSide.East => (tile.OriginX + tile.SizeX / 2f, tile.OriginY + tile.SizeY),
        ExitSide.West => (tile.OriginX + tile.SizeX / 2f, tile.OriginY),
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };
}
