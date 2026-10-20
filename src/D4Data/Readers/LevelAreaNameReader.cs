using D4Data.Casc;
using D4Data.Schema;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Readers;

/// <summary>The display name the game shows for a level area (a waypoint's or a dungeon's name on the map), per locale.</summary>
/// <remarks>The name lives in the area's sibling StringList <c>LevelArea_&lt;area name&gt;</c>, label <c>Name</c>
/// (research/map-points.md); that StringList has its own SNO id, found through the CoreTOC name.</remarks>
public sealed class LevelAreaNameReader(SnoStore store, TypeSchema schema)
{
    private const string NameLabel = "Name", SiblingPrefix = "LevelArea_", StringListDefinition = "StringListDefinition";

    private readonly int _stringListGroup = schema.SnoGroup(StringListDefinition);

    /// <summary>Null when the area has no localized name in <paramref name="locale"/> (not installed, no sibling list or no label).</summary>
    public string? Name(int levelAreaSno, string locale)
    {
        if (store.Name(levelAreaSno) is not { } area || store.Strings(locale) is not { } catalog) return null;
        int table;
        try { table = store.Resolve(_stringListGroup, SiblingPrefix + area); }
        catch (ArgumentException) { return null; }
        return catalog.TryGet(table, NameLabel, out var text) && text.Length > 0 ? text : null;
    }
}
