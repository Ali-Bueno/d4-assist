namespace D4Assist.Game;

/// <summary>
/// The world a level area lies in, read from its name: a dungeon's or story area's areas begin with the world's own name
/// (<c>CSD_Frac_Underworld_AncientCity_Holdout</c> in <c>CSD_Frac_Underworld_AncientCity</c>, <c>Frac_Underworld_Depth_1</c> in
/// <c>Frac_Underworld</c>), the open world's begin with none (<c>Frac_Glacier_East</c>). The map's marker list keeps a dungeon's
/// markers after the player leaves it (tester 2026-11-01: the Ancient City's quest marker offered in the Darkened Way, 228 units
/// off in the dungeon's own coordinates). Not thread-safe: callers hold the game-data gate.
/// </summary>
/// <param name="nameOf">A level area's name by SNO; null when unknown.</param>
/// <param name="isWorld">True for a world name.</param>
public sealed class LevelAreaWorlds(Func<int, string?> nameOf, Func<string, bool> isWorld)
{
    private readonly Dictionary<int, string?> _worlds = [];

    /// <summary>The world whose name begins the level area's (the longest such), or null for none or an unknown area.</summary>
    public string? WorldOf(int levelAreaSno)
    {
        if (_worlds.TryGetValue(levelAreaSno, out var known)) return known;
        string? world = null;
        if (nameOf(levelAreaSno) is { } name)
            for (int end = name.Length; end > 0 && world is null; end = name.LastIndexOf('_', end - 1))
                if (isWorld(name[..end])) world = name[..end];
        return _worlds[levelAreaSno] = world;
    }

    /// <summary>False for a marker in a level area of another world than <paramref name="world"/>.</summary>
    public bool InWorld(int levelAreaSno, string world) => WorldOf(levelAreaSno) is not { } owner || owner == world;
}
