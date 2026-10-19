using System.Numerics;
using D4Assist.Navigation;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Paths over a fixed-grid world (the open world: 1220 scene chunks of 96 units, research/dungeon-data.md §3) from the
/// offline surfaces of the chunks around the walk (SurfacePathfinder). Thread-safe; scene surfaces are kept once read.
/// </summary>
/// <param name="chunks">The scene placements of a world by its CoreTOC name; empty for a world without a fixed grid.</param>
/// <param name="surfaces">A scene's surface voxels, scene-local (SceneReader.SurfaceVoxels); null when it has none.</param>
public sealed class OverworldPaths(Func<string, IReadOnlyList<SceneChunkInfo>> chunks, Func<int, IReadOnlyList<Vector3>?> surfaces)
{
    /// <summary>The chunks searched reach this far beyond the box of start and target, room for a detour.</summary>
    public const float MarginUnits = 150f;

    /// <summary>Longer walks aren't searched (the map grows with the square of the distance).</summary>
    public const float MaxDistanceUnits = 1500f;

    private const float ChunkUnits = 96f;

    private readonly object _gate = new();
    private readonly Dictionary<int, IReadOnlyList<Vector3>?> _scenes = [];

    /// <summary>The route's points (SurfacePathfinder.Find), or null when there is none or the walk is too long.</summary>
    public IReadOnlyList<Vector2>? Find(string worldName, Vector3 from, Vector2 to)
    {
        var start = new Vector2(from.X, from.Y);
        if (Vector2.Distance(start, to) > MaxDistanceUnits) return null;
        float minX = MathF.Min(start.X, to.X) - MarginUnits, maxX = MathF.Max(start.X, to.X) + MarginUnits;
        float minY = MathF.Min(start.Y, to.Y) - MarginUnits, maxY = MathF.Max(start.Y, to.Y) + MarginUnits;
        var voxels = new List<Vector3>();
        lock (_gate)
            foreach (var chunk in chunks(worldName))
            {
                if (chunk.Position.X + ChunkUnits <= minX || chunk.Position.X >= maxX || chunk.Position.Y + ChunkUnits <= minY || chunk.Position.Y >= maxY) continue;
                if (!_scenes.TryGetValue(chunk.SceneSno, out var local)) _scenes[chunk.SceneSno] = local = surfaces(chunk.SceneSno);
                if (local is null) continue;
                foreach (var v in local) voxels.Add(chunk.Position + Vector3.Transform(v, chunk.Rotation));
            }
        return SurfacePathfinder.Find(voxels, start, to, from.Z);
    }
}
