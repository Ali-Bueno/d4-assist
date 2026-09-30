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

    // ActorDefinition.eType (+8): 1 = monster/NPC (has ptMonsterData / ptNPCData), 2 = gizmo. research/dungeon-objectives.md �3.
    public const int ActorTypeMonster = 1;
    public const int ActorTypePlayer = 7;           // only the player-class actors (<class>M / <class>F)
    public const int HostileTeam = 19;              // ActorDefinition.eDefaultTeam of hostile monsters, incl. hostile NPC stand-ins

    // ActorDefinition.eActorGizmoType (+392); each value is named after the data class at ptGizmoData
    // (definitions.json has no enum strings). research/dungeon-objectives.md �3.
    public const int GizmoDoor = 0;                 // DoorGizmoData
    public const int GizmoChest = 1;                // ChestGizmoData
    public const int GizmoPortal = 2;               // PortalGizmoData: dungeon portals, level transitions (research/dungeon-objectives.md �7)
    public const int GizmoRewardChest = 19;         // data class unread; every actor is a *_Chest_Reward* event chest
    public const int GizmoSign = 98;                // SignGizmoData: lore books, notes, notices
    public const int GizmoLockedChest = 106;        // data class unread; mostly *_Chest_Rare_Locked_* key chests
    public const int GizmoSwitch = 57;              // SwitchGizmoData
    public const int GizmoCarryable = 94;           // CarryableGizmoData
    public const int GizmoCarryableReceptacle = 95; // CarryableReceptacleGizmoData
    public const int GizmoQuestSwitch = 100;        // QuestSwitchGizmoData
    public const int GizmoQuestChest = 105;         // QuestChestGizmoData

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
