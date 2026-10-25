using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>
/// The region (Zone) and territory a LevelArea lies in: those of the first Subzone whose arLevelAreas lists it
/// (SubzoneDefinition.snoZoneParent, snoTerritory), named by the StringLists <c>Zone_&lt;name&gt;</c> and
/// <c>Territory_&lt;name&gt;</c>, label <c>Name</c> (deDE: Frac_Kyovashad → "Zersplitterte Gipfel"; Hawe_WetlandNorth → territory
/// "Auen von Fethis", the helltide's area, in "Hawezar"; all 53 waypoints of build 3.2.3 have a region).
/// </summary>
public sealed class ZoneCatalog(SnoStore store, TypeSchema schema)
{
    private const string Subzone = "SubzoneDefinition";

    private Dictionary<int, (int Zone, int Territory)>? _parents;

    /// <summary>The Zone SNO of <paramref name="levelAreaSno"/>; null when no subzone lists it.</summary>
    public int? ZoneOf(int levelAreaSno) => Parents().TryGetValue(levelAreaSno, out var p) && p.Zone > 0 ? p.Zone : null;

    /// <summary>The Territory SNO of <paramref name="levelAreaSno"/>; null when no subzone lists it or names none.</summary>
    public int? TerritoryOf(int levelAreaSno) => Parents().TryGetValue(levelAreaSno, out var p) && p.Territory > 0 ? p.Territory : null;

    /// <summary>The region's name in <paramref name="locale"/>; null when no subzone lists the LevelArea or the zone has no name text.</summary>
    public string? Name(int levelAreaSno, string locale) => NameOf(ZoneOf(levelAreaSno), "Zone", locale);

    /// <summary>The territory's name in <paramref name="locale"/>; null when none is known or it has no name text.</summary>
    public string? TerritoryName(int levelAreaSno, string locale) => NameOf(TerritoryOf(levelAreaSno), "Territory", locale);

    private string? NameOf(int? sno, string table, string locale) =>
        sno is { } id && store.Name(id) is { } name ? store.Text(locale, $"{table}_{name}", "Name") : null;

    // Every subzone is read once (~2000 in build 3.2.3, under a second).
    private Dictionary<int, (int Zone, int Territory)> Parents()
    {
        if (_parents is not null) return _parents;
        var parents = new Dictionary<int, (int, int)>();
        int group = schema.SnoGroup(Subzone);
        int areasField = schema.Offset(Subzone, "arLevelAreas");
        int zoneField = schema.Offset(Subzone, "snoZoneParent"), territoryField = schema.Offset(Subzone, "snoTerritory");
        foreach (var (id, _) in store.AllEntries(group))
        {
            if (store.Meta(group, id) is not { } subzone) continue;
            int zone = subzone.I32(zoneField), territory = subzone.I32(territoryField);
            if (zone <= 0 && territory <= 0) continue;
            foreach (int element in subzone.Array(areasField, sizeof(int)))
                if (subzone.I32(element) is > 0 and var area)
                    parents[area] = parents.TryGetValue(area, out var known)
                        ? (known.Item1 > 0 ? known.Item1 : zone, known.Item2 > 0 ? known.Item2 : territory) // the first one that names each
                        : (zone, territory);
        }
        return _parents = parents;
    }
}
