using D4Assist.Navigation;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// The tiles of a static-grid world (eLayoutType 1, e.g. the Mercenary den), built offline from the world's placed
/// scene chunks: the live CScene route only matches DRLG scenes, so it cannot serve these. Origin is the chunk position,
/// size comes from the scene's own data, yaw from the rotation quaternion, exit mask 0. Cached per world.
/// </summary>
public sealed class StaticWorldTiles(
    Func<int, IReadOnlyList<SceneChunkInfo>> chunksOf, Func<int, float[]> tileSizeOf, Func<int, string?> nameOf)
{
    private readonly Dictionary<int, IReadOnlyList<TileState>> _cache = [];

    public IReadOnlyList<TileState> Build(int worldSno)
    {
        if (_cache.TryGetValue(worldSno, out var cached)) return cached;

        var tiles = new List<TileState>();
        uint id = 1; // unique node/group id per scene, so nothing merges them
        foreach (var chunk in chunksOf(worldSno))
        {
            var size = tileSizeOf(chunk.SceneSno);
            float yaw = TileRotation.YawDegrees(chunk.Rotation.Z, chunk.Rotation.W);
            tiles.Add(new TileState(
                chunk.SceneSno, nameOf(chunk.SceneSno), chunk.Position.X, chunk.Position.Y,
                size.Length > 0 ? size[0] : 0f, size.Length > 1 ? size[1] : 0f, ExitMask: 0, NodeId: id, GroupId: id, YawDegrees: yaw));
            id++;
        }
        return _cache[worldSno] = tiles;
    }
}
