using System.Numerics;
using D4Assist.Navigation;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>A traversal end a scene places (a jump, duck-under or slide gizmo, or where one lands), in the scene's own axes.</summary>
public readonly record struct TraversalPoint(Vector3 Local, bool Slide = false);

/// <summary>
/// Paths over a fixed-grid world (the open world: 1220 scene chunks of 96 units, research/dungeon-data.md §3; a story area's
/// static scenes) from the offline surfaces of the chunks around the walk (SurfacePathfinder), joined across gaps by the
/// scenes' traversals. Thread-safe; scene surfaces and traversals are kept once read.
/// </summary>
/// <param name="chunks">The scene placements of a world by its CoreTOC name; empty for a world without a fixed grid.</param>
/// <param name="surfaces">A scene's surface voxels, scene-local (SceneReader.SurfaceVoxels); null when it has none.</param>
/// <param name="traversals">A scene's traversal ends, scene-local; null for none.</param>
/// <param name="worldTraversals">The traversal ends a world's own marker layers place, by its CoreTOC name, in world space: the
/// Darkened Way's jump to the wounded knight and its ladder down from the lift sit in <c>Frac_Underworld (…)</c> sets, in no
/// scene (research/dungeon-data.md §4.1c); null for none.</param>
public sealed class OverworldPaths(
    Func<string, IReadOnlyList<SceneChunkInfo>> chunks, Func<int, IReadOnlyList<Vector3>?> surfaces,
    Func<int, IReadOnlyList<TraversalPoint>>? traversals = null, Func<string, IReadOnlyList<TraversalPoint>>? worldTraversals = null)
{
    /// <summary>The chunks searched reach this far beyond the box of start and target, room for a detour.</summary>
    public const float MarginUnits = 150f;

    /// <summary>Longer walks aren't searched (the map grows with the square of the distance).</summary>
    public const float MaxDistanceUnits = 1500f;

    private const float ChunkUnits = 96f;

    private readonly object _gate = new();
    private readonly Dictionary<int, IReadOnlyList<Vector3>?> _scenes = [];
    private readonly Dictionary<int, IReadOnlyList<TraversalPoint>> _traversals = [];

    /// <summary>The route's points (SurfacePathfinder.Find), or null when there is none or the walk is too long.</summary>
    public IReadOnlyList<Vector2>? Find(string worldName, Vector3 from, Vector2 to)
    {
        var start = new Vector2(from.X, from.Y);
        if (Vector2.Distance(start, to) > MaxDistanceUnits) return null;
        float minX = MathF.Min(start.X, to.X) - MarginUnits, maxX = MathF.Max(start.X, to.X) + MarginUnits;
        float minY = MathF.Min(start.Y, to.Y) - MarginUnits, maxY = MathF.Max(start.Y, to.Y) + MarginUnits;
        var voxels = new List<Vector3>();
        var ends = new List<(Vector3 At, bool Slide)>();
        lock (_gate)
            foreach (var chunk in chunks(worldName))
            {
                if (chunk.Position.X + ChunkUnits <= minX || chunk.Position.X >= maxX || chunk.Position.Y + ChunkUnits <= minY || chunk.Position.Y >= maxY) continue;
                if (!_scenes.TryGetValue(chunk.SceneSno, out var local)) _scenes[chunk.SceneSno] = local = surfaces(chunk.SceneSno);
                if (local is null) continue;
                foreach (var v in local) voxels.Add(chunk.Position + Vector3.Transform(v, chunk.Rotation));
                if (traversals is null) continue;
                if (!_traversals.TryGetValue(chunk.SceneSno, out var points)) _traversals[chunk.SceneSno] = points = traversals(chunk.SceneSno);
                foreach (var p in points) ends.Add((chunk.Position + Vector3.Transform(p.Local, chunk.Rotation), p.Slide));
            }
        if (worldTraversals is not null)
            foreach (var p in worldTraversals(worldName))
                if (p.Local.X >= minX && p.Local.X <= maxX && p.Local.Y >= minY && p.Local.Y <= maxY) ends.Add((p.Local, p.Slide));
        return SurfacePathfinder.Find(voxels, start, to, from.Z, Links(ends));
    }

    /// <summary>Every two traversal ends within JumpLinkCollector.MaxPairUnits, a slide one way down (not nearest-only pairing:
    /// research/dungeon-data.md §4.1c).</summary>
    public static IReadOnlyList<SurfaceLink> Links(IReadOnlyList<(Vector3 At, bool Slide)> ends)
    {
        var links = new List<SurfaceLink>();
        for (int i = 0; i < ends.Count; i++)
            for (int j = i + 1; j < ends.Count; j++)
            {
                var (a, b) = ends[i].At.Z >= ends[j].At.Z ? (ends[i].At, ends[j].At) : (ends[j].At, ends[i].At);
                if (Vector2.Distance(new(a.X, a.Y), new(b.X, b.Y)) <= JumpLinkCollector.MaxPairUnits)
                    links.Add(new SurfaceLink(a, b, OneWay: ends[i].Slide || ends[j].Slide));
            }
        return links;
    }
}
