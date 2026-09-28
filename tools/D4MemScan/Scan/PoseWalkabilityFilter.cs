using D4Data.Model;

namespace D4MemScan.Scan;

/// <summary>Checks a candidate position against the offline walkable-voxel map of the tile it falls inside.</summary>
/// <remarks>The voxel decode is the one already validated in D4DungeonDump/NavGridReader. Walkability is read
/// offline per SNO by the caller (PoseSnapshotCommand), not embedded in the `scenes` report.</remarks>
public static class PoseWalkabilityFilter
{
    /// <summary>True when the position is walkable, or when it cannot be verified (no map / outside any tile).</summary>
    public static bool IsWalkableOrUnknown(float x, float y, IReadOnlyList<TileHitRecord> tiles, IReadOnlyDictionary<int, SceneInfo> walkability)
    {
        foreach (var tile in tiles)
        {
            if (!walkability.TryGetValue(tile.Sno, out var def)) continue;
            float localX = x - tile.Wp[0], localY = y - tile.Wp[1];
            if (localX < 0 || localX > def.TileSize[0] || localY < 0 || localY > def.TileSize[1]) continue;

            var map = def.Walkability?.Map;
            if (map is null) return true; // inside this tile but no voxel map to check against

            int row = (int)((localX - map.Origin[0]) / map.Resolution);
            int col = (int)((localY - map.Origin[1]) / map.Resolution);
            if (row < 0 || row >= map.Rows.Length || col < 0 || col >= map.Rows[row].Length) return true;
            return map.Rows[row][col] == '#';
        }
        return true; // not inside any known tile footprint
    }
}
