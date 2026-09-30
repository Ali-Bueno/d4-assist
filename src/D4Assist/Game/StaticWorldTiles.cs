using D4Assist.Navigation;
using D4Data.Model;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// The tiles of a static world (eLayoutType 0/1, e.g. the Darkened Way, Tristram, the Mercenary den), built offline from
/// the world's placed scene chunks: the live CScene route only matches DRLG scenes, so it cannot serve these. Origin is the
/// chunk position, yaw from the rotation quaternion, exit mask 0. Each tile spans at least one world grid cell and its
/// whole walk map (a static scene's own tile size is just one 32-unit nav cell). Cached per world.
/// </summary>
public sealed class StaticWorldTiles(
    Func<int, (float GridSize, IReadOnlyList<SceneChunkInfo> Chunks)> worldOf, Func<int, WalkableMap?> mapOf, Func<int, string?> nameOf)
{
    // One node for every tile: a static world has no rooms, so the seams between its scenes are not exits.
    private const uint StaticRoomId = 1;

    private readonly Dictionary<int, IReadOnlyList<TileState>> _cache = [];

    public IReadOnlyList<TileState> Build(int worldSno)
    {
        if (_cache.TryGetValue(worldSno, out var cached)) return cached;

        var (gridSize, chunks) = worldOf(worldSno);
        var tiles = new List<TileState>();
        foreach (var chunk in chunks)
        {
            var map = mapOf(chunk.SceneSno);
            float sizeX = Math.Max(gridSize, map is null ? 0 : map.Origin[0] + map.Rows.Length * map.Resolution);
            float sizeY = Math.Max(gridSize, map is null || map.Rows.Length == 0 ? 0 : map.Origin[1] + map.Rows[0].Length * map.Resolution);
            float yaw = TileRotation.YawDegrees(chunk.Rotation.Z, chunk.Rotation.W);
            tiles.Add(new TileState(
                chunk.SceneSno, nameOf(chunk.SceneSno), chunk.Position.X, chunk.Position.Y,
                sizeX, sizeY, ExitMask: 0, NodeId: StaticRoomId, GroupId: StaticRoomId, YawDegrees: yaw));
        }
        return _cache[worldSno] = tiles;
    }
}
