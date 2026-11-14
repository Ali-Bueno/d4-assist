using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Assist.Game;

/// <summary>A world marker layer that places collision props: the actors only it places, and the props as walls.</summary>
public sealed record WorldLayer(string Name, IReadOnlyList<(int Sno, Vector2 At)> Actors, IReadOnlyList<DoorWall> Walls);

/// <summary>
/// The invisible collision props a world's marker layers place, as walls for the surface paths: the walk surfaces show floor
/// under them (research/dungeon-data.md §4.1c). A layer's props stand once the client lists an actor only that layer places,
/// until a quest phase moves on with none of them listed.
/// </summary>
public sealed class LayerBlockers(Func<string, IReadOnlyList<WorldLayer>> layersOf, Action<string>? log = null)
{
    // A placed actor is listed on its marker's spot.
    private const float SameSpotUnits = 1f;

    private const string CollisionNamePart = "Collision";
    private static readonly string[] NoCollisionNameParts = ["NoCollision", "IgnoreCollision", "PierceCollision"];

    private readonly object _gate = new();
    private string? _world;
    private IReadOnlyList<WorldLayer> _layers = [];
    private readonly HashSet<string> _standing = [];
    private HashSet<string> _listed = [];
    private int _version;

    /// <summary>Changes whenever a layer's props start or stop being walls (LockedDoors.Version).</summary>
    public int Version { get { lock (_gate) return _version; } }

    /// <summary>A new world: none standing, its layers read in the background (the task ends once they are known).</summary>
    public Task EnterWorld(string world)
    {
        lock (_gate)
        {
            _world = world;
            _layers = [];
            _standing.Clear();
            _listed = [];
            _version++;
        }
        return Task.Run(() =>
        {
            IReadOnlyList<WorldLayer> layers;
            try { layers = layersOf(world); }
            catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
            {
                log?.Invoke($"LAYER read failed for {world}: {e.Message}");
                return;
            }
            lock (_gate)
                if (_world == world) _layers = layers;
        });
    }

    /// <summary>Takes an actor poll: a layer with one of its own actors listed stands.</summary>
    public void Update(IReadOnlyList<ActorState> actors)
    {
        lock (_gate)
        {
            if (_layers.Count == 0) return;
            var spots = actors.ToLookup(a => a.ActorSno, a => new Vector2(a.X, a.Y));
            var listed = new HashSet<string>();
            foreach (var layer in _layers)
            {
                if (!layer.Actors.Any(m => spots[m.Sno].Any(at => Vector2.Distance(at, m.At) <= SameSpotUnits))) continue;
                listed.Add(layer.Name);
                if (!_standing.Add(layer.Name)) continue;
                _version++;
                log?.Invoke($"LAYER on: {layer.Name}, {layer.Walls.Count} collision prop(s)");
            }
            _listed = listed;
        }
    }

    /// <summary>A quest phase moved on: a layer may be gone with it, so only those with an actor listed now keep standing.</summary>
    public void QuestMovedOn()
    {
        lock (_gate)
            foreach (string layer in _standing.Where(l => !_listed.Contains(l)).ToList())
            {
                _standing.Remove(layer);
                _version++;
                log?.Invoke($"LAYER off (quest moved on): {layer}");
            }
    }

    /// <summary>The collision props of every standing layer, for OverworldPaths.Find.</summary>
    public IReadOnlyList<DoorWall> Walls()
    {
        lock (_gate)
            return [.. _layers.Where(l => _standing.Contains(l.Name)).SelectMany(l => l.Walls)];
    }

    /// <summary>Layers read from the game data's <c>&lt;World&gt; (&lt;Layer&gt;)</c> marker sets; every read holds <paramref name="gate"/>.</summary>
    public static LayerBlockers FromGameData(
        SnoStore store, TypeSchema schema, object gate, Func<int, ActorDefinitionInfo?> definition, Action<string>? log = null)
    {
        var markers = new MarkerReader(store, schema);
        int markerSets = schema.SnoGroup("MarkerSetDefinition"), actors = schema.SnoGroup("ActorDefinition");
        return new LayerBlockers(world =>
        {
            lock (gate)
                return Layers(
                    [.. store.EntriesWithPrefix(markerSets, world + " (").Select(set => (set.Name,
                        (IReadOnlyList<(int, Matrix4x4)>)[.. markers.ReadAllPlaced(set.Id).Where(m => m.Marker.Group == actors).Select(m => (m.Marker.Sno, m.ToFrame))]))],
                    sno => IsBlocker(store.Name(sno), definition(sno)), definition);
        }, log);
    }

    /// <summary>A server prop named for its collision (census 2026-11-13: <c>InvisibleCollision_Cube_ServerProp</c>,
    /// <c>Hotfix_ServerProp_InvisCollision_*</c>, <c>*_CollisionCube</c>, <c>QST_CollisionBlocker_PlayerOnly</c>...).</summary>
    public static bool IsBlocker(string? name, ActorDefinitionInfo? info) =>
        name is not null && info?.EType == Conventions.ActorTypeServerProp
        && name.Contains(CollisionNamePart, StringComparison.OrdinalIgnoreCase)
        && !NoCollisionNameParts.Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));

    /// <summary>The layers that place a collision prop, each with the actors no other layer places on the same spot.</summary>
    /// <param name="sets">Every layer of the world: its name and its actor markers, each with its transform into world space.</param>
    public static IReadOnlyList<WorldLayer> Layers(
        IReadOnlyList<(string Name, IReadOnlyList<(int Sno, Matrix4x4 ToWorld)> Placed)> sets,
        Func<int, bool> isBlocker, Func<int, ActorDefinitionInfo?> definition)
    {
        var layers = new List<WorldLayer>();
        foreach (var (name, placed) in sets)
        {
            var walls = new List<DoorWall>();
            foreach (var (sno, toWorld) in placed)
                if (isBlocker(sno) && definition(sno) is { } info && Footprint(info.BoundsCenter, info.BoundsHalfExtents, toWorld) is { } wall)
                    walls.Add(wall);
            if (walls.Count == 0) continue;
            bool PlacedElsewhere(int sno, Vector2 at) => sets.Any(other => other.Name != name
                && other.Placed.Any(m => m.Sno == sno && Vector2.Distance(Spot(m.ToWorld), at) <= SameSpotUnits));
            layers.Add(new WorldLayer(name, [.. placed.Select(m => (m.Sno, At: Spot(m.ToWorld))).Where(m => !PlacedElsewhere(m.Sno, m.At))], walls));
        }
        return layers;
    }

    private static Vector2 Spot(Matrix4x4 toWorld) => new(toWorld.Translation.X, toWorld.Translation.Y);

    /// <summary>The 1-unit cells whose centre lies in the prop's bounds box as its marker scales, turns and places it, with the
    /// box's lowest point and height; null for a flat or unreadable box.</summary>
    public static DoorWall? Footprint(Vector3 center, Vector3 half, Matrix4x4 toWorld)
    {
        if (half.X <= 0 || half.Y <= 0 || !Matrix4x4.Invert(toWorld, out var toLocal)) return null;
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (int corner = 0; corner < 8; corner++)
        {
            var sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
            var p = Vector3.Transform(center + half * sign, toWorld);
            (min, max) = (Vector3.Min(min, p), Vector3.Max(max, p));
        }
        float middleZ = (min.Z + max.Z) / 2;
        var cells = new HashSet<(int X, int Y)>();
        for (int x = (int)MathF.Floor(min.X); x <= (int)MathF.Floor(max.X); x++)
            for (int y = (int)MathF.Floor(min.Y); y <= (int)MathF.Floor(max.Y); y++)
            {
                var local = Vector3.Transform(new Vector3(x + 0.5f, y + 0.5f, middleZ), toLocal) - center;
                if (MathF.Abs(local.X) <= half.X && MathF.Abs(local.Y) <= half.Y) cells.Add((x, y));
            }
        return cells.Count == 0 ? null : new DoorWall(cells, min.Z, max.Z - min.Z);
    }
}
