using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>A portal a tile places in its gameplay marker layers, at a point in the tile's own axes.</summary>
public readonly record struct ScenePortal(int ActorSno, float LocalX, float LocalY);

/// <summary>
/// The portals each tile places in its "Game"/"Shared" marker layers, read offline and cached per scene SNO: a portal is known
/// once its tile loads, before the game streams the actor in, about 25 units away (research/dungeon-objectives.md §18).
/// Callers serialize access (the CASC gate).
/// </summary>
public sealed class ScenePortalReader(SceneReader scenes, Func<int, bool> isPortal)
{
    private readonly Dictionary<int, IReadOnlyList<ScenePortal>> _cache = [];

    public IReadOnlyList<ScenePortal> In(int sceneSno)
    {
        if (_cache.TryGetValue(sceneSno, out var cached)) return cached;
        IReadOnlyList<ScenePortal> portals;
        try
        {
            portals = [.. scenes.Read(sceneSno).Markers
                .Where(m => isPortal(m.Actor.Sno))
                .Select(m => new ScenePortal(m.Actor.Sno, m.Local[0], m.Local[1]))];
        }
        catch (InvalidDataException) { portals = []; } // no scene record: nothing placed
        return _cache[sceneSno] = portals;
    }
}
