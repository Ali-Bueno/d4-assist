using System.Numerics;
using D4Data.Model;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>An actor a tile places in its gameplay marker layers, at a point in the tile's own axes.</summary>
/// <param name="Prefab">Which prefab placement of the tile placed it, numbered from 1 per scene (one in every layer that places it
/// at the same spot); 0 when placed directly.</param>
public readonly record struct TileActor(int ActorSno, Vector3 Local, int Prefab = 0);

/// <summary>
/// The actors each tile places in its "Game"/"Shared" marker layers that <paramref name="keep"/> accepts, read offline and cached
/// per scene SNO: known once the tile loads, before the game streams them in (~25 units; research/dungeon-objectives.md §18).
/// Callers serialize access (the CASC gate).
/// </summary>
public sealed class TileActorReader(SceneReader scenes, Func<int, bool> keep)
{
    private readonly Dictionary<int, IReadOnlyList<TileActor>> _cache = [];

    public IReadOnlyList<TileActor> In(int sceneSno)
    {
        if (_cache.TryGetValue(sceneSno, out var cached)) return cached;
        IReadOnlyList<TileActor> actors;
        var prefabs = new Dictionary<PrefabPlacement, int>();
        int PrefabOf(PrefabPlacement? placed) =>
            placed is null ? 0 : prefabs.TryGetValue(placed, out var id) ? id : prefabs[placed] = prefabs.Count + 1;
        try
        {
            actors = [.. scenes.Read(sceneSno).Markers
                .Where(m => keep(m.Actor.Sno))
                .Select(m => new TileActor(m.Actor.Sno, new Vector3(m.Local[0], m.Local[1], m.Local[2]), PrefabOf(m.Prefab)))];
        }
        catch (InvalidDataException) { actors = []; } // no scene record: nothing placed
        return _cache[sceneSno] = actors;
    }
}
