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
    // A scene's markers the game stands somewhere else, by the shift its actors there agree on; kept per tile once read, as the
    // actors that showed it die or stream out.
    private readonly Dictionary<(int Scene, int Layer, Vector2 Tile), Vector3> _shifts = [];
    // Session reads and the exploration save run on different threads.
    private readonly object _gate = new();
    // This world's loaded tiles, for the map's markers: the map lists other worlds' too (live 2026-10-22: the open world's copy of
    // a boss-room trigger, 3000 units off, placed a layer there and the guide explored towards it for nine minutes).
    private List<Vector2> _tiles = [];
    private const float NearTilesUnits = 256f;

    /// <summary>A new world: its tiles are new.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _layers.Clear();
            _shifts.Clear();
        }
    }

    public IReadOnlyList<SavedLayer> Export()
    {
        lock (_gate)
            return [.. _layers.Select(l => new SavedLayer(l.Key, l.Value.Scene, l.Value.Wp.X, l.Value.Wp.Y, l.Value.Wp.Z, l.Value.Q.Z, l.Value.Q.W))];
    }

    /// <summary>The save wins: the first quest read can come before it (live 2026-10-22: room B took room A's tile 0.13 s before the
    /// save said it was room A's), so a layer that took a saved layer's tile lands again.</summary>
    public void Restore(IReadOnlyList<SavedLayer> saved)
    {
        lock (_gate)
            foreach (var l in saved)
            {
                var wp = new Vector3(l.X, l.Y, l.Z);
                foreach (int other in _layers.Where(o => o.Key != l.Layer && o.Value.Scene == l.Scene
                    && Vector2.Distance(Flat(o.Value.Wp), Flat(wp)) <= SameTileUnits).Select(o => o.Key).ToList())
                {
                    _layers.Remove(other);
                    log?.Invoke($"LAYER {other} of scene {l.Scene} unplaced: the saved layer {l.Layer} holds its tile");
                }
                _layers[l.Layer] = (l.Scene, wp, new Quaternion(0, 0, l.QZ, l.QW));
            }
    }

    /// <param name="placements">Where each scene is placed: its tiles' origins and rotations.</param>
    /// <param name="marked">The actors the game's map marks, wherever they are.</param>
    /// <param name="seen">The actors standing in the world now (and those the map marks), for markers the game stands elsewhere.</param>
    public IReadOnlyList<PlacedMarker> Place(
        IReadOnlyList<SceneMarker> markers, IReadOnlyDictionary<int, List<(Vector3 Wp, Quaternion Q)>> placements,
        IReadOnlyList<(int Sno, Vector3 Position)> marked, IReadOnlyList<(int Sno, Vector3 Position)>? seen = null)
    {
        var groups = new Dictionary<(int Scene, int Layer, Vector2 Tile), List<PlacedMarker>>();
        _tiles = [.. placements.Values.SelectMany(p => p.Select(t => Flat(t.Wp)))];
        lock (_gate)
        {
            foreach (var m in markers)
            {
                var tiles = placements.TryGetValue(m.SceneSno, out var at) ? at : [];
                (Vector3 Wp, Quaternion Q)? tile = m.Layer is { } layer ? LayerTile(layer, m, tiles, marked) : tiles.Count == 1 ? tiles[0] : null;
                if (tile is not { } t) continue;
                var key = (m.SceneSno, m.Layer ?? 0, Flat(t.Wp));
                if (!groups.TryGetValue(key, out var group)) groups[key] = group = [];
                group.Add(m.Marker with { Position = t.Wp + Vector3.Transform(m.Marker.Position, t.Q) });
            }
            foreach (var (key, group) in groups)
                if (Shift(key, group, seen ?? []) is { } shift)
                    for (int i = 0; i < group.Count; i++) group[i] = group[i] with { Position = group[i].Position + shift };
        }
        return [.. groups.Values.SelectMany(g => g)];
    }

    // Two of a scene's markers or more with their actors all standing one shift off: the game placed them there, not on the tile
    // (live 2026-10-24, Skartara's mindscape 4, CSD_Skov_Lycander_Mindscape_04: the six Hatred Spires of the step after phase 441
    // are markers of the holdout tunnel Sightless_Mephisto_EW_POI_HoldoutTunnel_01 on its tile at (0,-32), but the spires stood
    // (19.9,-30.5) off, all six; the guide led to an empty ledge at (7.6,-40.3) for minutes).
    private const float ShiftMatchUnits = 1.5f, MaxShiftUnits = 96f;
    private const int ShiftAgreement = 2;

    private Vector3? Shift((int Scene, int Layer, Vector2 Tile) key, List<PlacedMarker> group, IReadOnlyList<(int Sno, Vector3 Position)> seen)
    {
        if (_shifts.TryGetValue(key, out var known)) return known;
        if (group.Count < ShiftAgreement) return null;
        int Agree(Vector3 shift) => group.Count(p => seen.Any(a => a.Sno == p.Sno
            && Vector2.Distance(Flat(a.Position), Flat(p.Position + shift)) <= ShiftMatchUnits));
        Vector3? best = null;
        int most = Agree(Vector3.Zero);
        foreach (var p in group)
            foreach (var (sno, position) in seen)
                if (sno == p.Sno && position - p.Position is var shift && Flat(shift).Length() is > ShiftMatchUnits and <= MaxShiftUnits
                    && Agree(shift) is var count && count >= ShiftAgreement && count > most)
                    (best, most) = (shift, count);
        if (best is not { } found) return null;
        _shifts[key] = found;
        log?.Invoke($"SCENE {key.Scene} markers moved by ({found.X:F1},{found.Y:F1}): {most} of {group.Count} actors stand there");
        return found;
    }

    private (Vector3 Wp, Quaternion Q)? LayerTile(
        int layer, SceneMarker m, List<(Vector3 Wp, Quaternion Q)> tiles, IReadOnlyList<(int Sno, Vector3 Position)> marked)
    {
        if (_layers.TryGetValue(layer, out var known)) return (known.Wp, known.Q);
        var taken = _layers.Where(l => l.Key != layer && l.Value.Scene == m.SceneSno).Select(l => l.Value.Wp).ToList();
        bool Free(Vector3 wp) => !taken.Any(w => Vector2.Distance(Flat(w), Flat(wp)) <= SameTileUnits);
        var free = tiles.Where(t => Free(t.Wp)).ToList();
        string how;
        // The map marks the step's own trigger where the game has it (live 2026-10-22: room B's at (-16,205) during phase 280).
        if (MapAnchored([m.Marker], marked).Where(Free).ToList() is [var own])
        {
            free = [tiles.FirstOrDefault(t => Vector2.Distance(Flat(t.Wp), Flat(own)) <= SameTileUnits, (Wp: own, Q: Quaternion.Identity))];
            how = "the map's marker of it";
        }
        else if (free.Count == 1) how = "its tile";
        else if (free.Count == 0 && taken.Count > 0 && MapAnchored(m.Anchors ?? [], marked).Where(Free).ToList() is [var origin])
        {
            free = [(Wp: origin, Q: Quaternion.Identity)];
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
    private List<Vector3> MapAnchored(IReadOnlyList<PlacedMarker> anchors, IReadOnlyList<(int Sno, Vector3 Position)> marked)
    {
        var origins = new List<Vector3>();
        foreach (var anchor in anchors)
            foreach (var (sno, position) in marked)
                if (sno == anchor.Sno && position - anchor.Position is var origin && OnGrid(origin.X) && OnGrid(origin.Y)
                    && _tiles.Any(t => Vector2.Distance(t, Flat(origin)) <= NearTilesUnits)
                    && !origins.Any(o => Vector2.Distance(Flat(o), Flat(origin)) <= SameTileUnits))
                    origins.Add(origin);
        return origins;
    }

    // DRLG tiles are placed on a 16-unit grid (32x32, 64x64 and 96x96 tiles at multiples of 32 so far).
    private const float GridUnits = 16f, OffGridUnits = 1f;

    private static bool OnGrid(float v) => MathF.Abs(v - MathF.Round(v / GridUnits) * GridUnits) <= OffGridUnits;

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Y);
}
