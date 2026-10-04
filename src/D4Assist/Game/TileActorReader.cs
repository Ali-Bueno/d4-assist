using System.Numerics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>An actor a tile places in its gameplay marker layers, at a point in the tile's own axes.</summary>
public readonly record struct TileActor(int ActorSno, Vector3 Local);

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
        try
        {
            actors = [.. scenes.Read(sceneSno).Markers
                .Where(m => keep(m.Actor.Sno))
                .Select(m => new TileActor(m.Actor.Sno, new Vector3(m.Local[0], m.Local[1], m.Local[2])))];
        }
        catch (InvalidDataException) { actors = []; } // no scene record: nothing placed
        return _cache[sceneSno] = actors;
    }
}
