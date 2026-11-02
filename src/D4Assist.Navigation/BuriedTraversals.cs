namespace D4Assist.Navigation;

/// <summary>A traversal as the tile graph joins two floor regions with it: <see cref="PropCost"/> is the soft cost of the standing
/// props the player must break through to use it (0 when free floor lies within reach of both ends).</summary>
internal sealed record GraphTraversal(JumpLink Link, float PropCost)
{
    public bool Buried => PropCost > 0f;
}

/// <summary>
/// Traversals whose end a standing prop buries: no free floor within <see cref="RoomPathfinder.JumpReachUnits"/> of it. In the
/// Necropolis of the First Born the flesh webs that open only when a tumor dies wrap the platforms' ladders, and the guide sent the
/// player up a ladder into the web (tester log 2026-11-01, research/dungeon-objectives.md §12).
/// </summary>
internal static class BuriedTraversals
{
    public static IReadOnlyList<GraphTraversal> Of(DungeonMap map, ITileWalkability? walkability, IReadOnlyList<JumpLink> jumps, BlockedCells props) =>
        [.. jumps.Select(j => new GraphTraversal(j, RoomPathfinder.PropPenalty * (PropWalk(map, walkability, j.A, props) + PropWalk(map, walkability, j.B, props))))];

    // How far past reach of the end the nearest floor no prop stands on lies; 0 when the end isn't buried or its floor is unknown.
    private static float PropWalk(DungeonMap map, ITileWalkability? walkability, (float X, float Y) end, BlockedCells props)
    {
        if (props.IsEmpty || walkability is null || map.TileAt(end.X, end.Y) is not { } tile
            || !walkability.TryGet(tile.Input.SceneSno, out var grid)) return 0f;
        var turns = TileRotation.QuarterTurns(tile.Input.YawDegrees);
        if (turns < 0) return 0f;
        bool floorNear = false;
        float nearestFree = float.MaxValue;
        for (var r = 0; r < grid.Rows; r++)
            for (var c = 0; c < grid.Cols; c++)
            {
                if (!grid.IsWalkable(r, c)) continue;
                var cell = TileRotation.ToWorld(tile.Input, grid.CellCenter(r, c), turns);
                float distance = TileRoutes.Distance(cell, end);
                floorNear |= distance <= RoomPathfinder.JumpReachUnits;
                if (!props.Contains(cell.X, cell.Y)) nearestFree = MathF.Min(nearestFree, distance);
            }
        return !floorNear || nearestFree == float.MaxValue ? 0f : MathF.Max(0f, nearestFree - RoomPathfinder.JumpReachUnits);
    }
}
