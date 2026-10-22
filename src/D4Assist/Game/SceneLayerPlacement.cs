using System.Numerics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>A scene layer's tile, kept across a restart of the app (ExplorationSnapshot.Layers).</summary>
public sealed record SavedLayer(int Layer, int Scene, float X, float Y, float Z, float QZ, float QW);

/// <summary>
/// Lands scene-local quest markers on the tile that carries them. A scene placed once carries them; a scene placed twice can carry
/// a different layer on each tile, so a layer keeps the tile it first landed on and another layer of that scene never takes it:
/// it lands on the scene's other tile, or, before that tile loads, where the map marks one of the layer's own actors. Live
/// 2026-10-22, "Die Leere" (QST_Skov_Celestia_03): lock room A (phase 276) and B (phase 280) are both <c>Cave_Tomb_EW_01</c>;
/// phase 280 landed on room A's tile, where the player had just been, while the map marked room B's demonic barrier wall at
/// (-15.9, 203), on a tile not loaded yet. Shared by the world's sessions and saved with its exploration, so a restart of the app
/// still knows which tile room A was.
/// </summary>
public sealed class SceneLayerPlacement(Action<string>? log = null)
{
    // The same tile, read twice; two placements of one scene are a tile apart.
    private const float SameTileUnits = 2f;

    private readonly Dictionary<int, (int Scene, Vector3 Wp, Quaternion Q)> _layers = [];
    // Session reads and the exploration save run on different threads.
    private readonly object _gate = new();

    /// <summary>A new world: its tiles are new.</summary>
    public void Reset()
    {
        lock (_gate) _layers.Clear();
    }

    public IReadOnlyList<SavedLayer> Export()
    {
        lock (_gate)
            return [.. _layers.Select(l => new SavedLayer(l.Key, l.Value.Scene, l.Value.Wp.X, l.Value.Wp.Y, l.Value.Wp.Z, l.Value.Q.Z, l.Value.Q.W))];
    }

    public void Restore(IReadOnlyList<SavedLayer> saved)
    {
        lock (_gate)
            foreach (var l in saved) _layers.TryAdd(l.Layer, (l.Scene, new Vector3(l.X, l.Y, l.Z), new Quaternion(0, 0, l.QZ, l.QW)));
    }

    /// <param name="placements">Where each scene is placed: its tiles' origins and rotations.</param>
    /// <param name="marked">The actors the game's map marks, wherever they are.</param>
    public IReadOnlyList<PlacedMarker> Place(
        IReadOnlyList<SceneMarker> markers, IReadOnlyDictionary<int, List<(Vector3 Wp, Quaternion Q)>> placements,
        IReadOnlyList<(int Sno, Vector3 Position)> marked)
    {
        var placed = new List<PlacedMarker>();
        lock (_gate)
            foreach (var m in markers)
            {
                var tiles = placements.TryGetValue(m.SceneSno, out var at) ? at : [];
                (Vector3 Wp, Quaternion Q)? tile = m.Layer is { } layer ? LayerTile(layer, m, tiles, marked) : tiles.Count == 1 ? tiles[0] : null;
                if (tile is { } t) placed.Add(m.Marker with { Position = t.Wp + Vector3.Transform(m.Marker.Position, t.Q) });
            }
        return placed;
    }

    private (Vector3 Wp, Quaternion Q)? LayerTile(
        int layer, SceneMarker m, List<(Vector3 Wp, Quaternion Q)> tiles, IReadOnlyList<(int Sno, Vector3 Position)> marked)
    {
        if (_layers.TryGetValue(layer, out var known)) return (known.Wp, known.Q);
        var taken = _layers.Where(l => l.Key != layer && l.Value.Scene == m.SceneSno).Select(l => l.Value.Wp).ToList();
        bool Free(Vector3 wp) => !taken.Any(w => Vector2.Distance(Flat(w), Flat(wp)) <= SameTileUnits);
        var free = tiles.Where(t => Free(t.Wp)).ToList();
        string how;
        if (free.Count == 1) how = "its tile";
        else if (free.Count == 0 && taken.Count > 0 && MapAnchored(m, marked).Where(Free).ToList() is [var origin])
        {
            free = [(origin, Quaternion.Identity)];
            how = "the map's marker of its actor";
        }
        else return null;
        _layers[layer] = (m.SceneSno, free[0].Wp, free[0].Q);
        log?.Invoke($"LAYER {layer} of scene {m.SceneSno} placed at ({free[0].Wp.X:F1},{free[0].Wp.Y:F1}) by {how}");
        return free[0];
    }

    // The tile origins the map's markers of the layer's own actors imply, unturned as every DRLG tile seen so far; one per tile.
    // Only an origin on the tile grid: the same barrier wall stands in other layers too (the banishment room's, at another spot
    // in its tile), and its marker there implies an origin off the grid.
    private static List<Vector3> MapAnchored(SceneMarker m, IReadOnlyList<(int Sno, Vector3 Position)> marked)
    {
        var origins = new List<Vector3>();
        foreach (var anchor in m.Anchors ?? [])
            foreach (var (sno, position) in marked)
                if (sno == anchor.Sno && position - anchor.Position is var origin && OnGrid(origin.X) && OnGrid(origin.Y)
                    && !origins.Any(o => Vector2.Distance(Flat(o), Flat(origin)) <= SameTileUnits))
                    origins.Add(origin);
        return origins;
    }

    // DRLG tiles are placed on a 16-unit grid (32x32, 64x64 and 96x96 tiles at multiples of 32 so far).
    private const float GridUnits = 16f, OffGridUnits = 1f;

    private static bool OnGrid(float v) => MathF.Abs(v - MathF.Round(v / GridUnits) * GridUnits) <= OffGridUnits;

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Y);
}
