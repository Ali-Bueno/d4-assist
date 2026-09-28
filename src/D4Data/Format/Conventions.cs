namespace D4Data.Format;

/// <summary>Enum values and axis conventions recovered empirically from game data (names are not in definitions.json).</summary>
/// <remarks>Evidence and confidence for each value: research/dungeon-data.md.</remarks>
public static class Conventions
{
    // WorldDefinition.eLayoutType: 0 = one fixed scene, 1 = fixed scene grid (ptSceneChunks filled),
    // 2 = DRLG, generated server-side (ptSceneChunks empty in client data for all 802 such worlds).
    public const int LayoutSingleScene = 0;
    public const int LayoutStaticGrid = 1;
    public const int LayoutDrlg = 2;

    // SceneDefinition.eDRLGTileType is an exit bitmask; matches 1739 tile names like "*_NSEW_01".
    public const int ExitNorth = 1;
    public const int ExitSouth = 2;
    public const int ExitEast = 4;
    public const int ExitWest = 8;

    // AICellData.m_cellId packs biased cell indexes: bits 31-24 = z + 0x80, bits 23-12 = x + 0x800, bits 11-0 = y + 0x800.
    private const int CellZShift = 24, CellZMask = 0xFF, CellZBias = 0x80;
    private const int CellXShift = 12, CellXYMask = 0xFFF, CellXYBias = 0x800;

    /// <summary>Scene-relative cell indexes (x, y, z) of an AI nav cell id.</summary>
    public static (int X, int Y, int Z) DecodeCellId(ulong cellId) => (
        (int)(cellId >> CellXShift & CellXYMask) - CellXYBias,
        (int)(cellId & CellXYMask) - CellXYBias,
        (int)(cellId >> CellZShift & CellZMask) - CellZBias);

    // Recursion guard for marker sets nested as prefabs; observed data nests 1-2 levels.
    public const int MaxPrefabDepth = 4;

    public static string LayoutKind(int layoutType) => layoutType switch
    {
        LayoutSingleScene => "single-scene",
        LayoutStaticGrid => "static-grid",
        LayoutDrlg => "drlg",
        _ => $"unknown-{layoutType}",
    };

    /// <summary>Compass letters for an exit mask. Engine compass: N = -X, S = +X, E = +Y, W = -Y.</summary>
    public static string[] Exits(int mask)
    {
        var exits = new List<string>(4);
        if ((mask & ExitNorth) != 0) exits.Add("N");
        if ((mask & ExitSouth) != 0) exits.Add("S");
        if ((mask & ExitEast) != 0) exits.Add("E");
        if ((mask & ExitWest) != 0) exits.Add("W");
        return [.. exits];
    }
}
