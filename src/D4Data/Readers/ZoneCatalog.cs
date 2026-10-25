using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>
/// The region (Zone) a LevelArea lies in: the Zone of the first Subzone whose arLevelAreas lists it
/// (SubzoneDefinition.snoZoneParent), named by the StringList <c>Zone_&lt;name&gt;</c>, label <c>Name</c>
/// (deDE: Frac_Kyovashad → "Zersplitterte Gipfel"; all 53 waypoints of build 3.2.3 have one).
/// </summary>
public sealed class ZoneCatalog(SnoStore store, TypeSchema schema)
{
    private const string Subzone = "SubzoneDefinition";

    private Dictionary<int, int>? _zones;

    /// <summary>The Zone SNO of <paramref name="levelAreaSno"/>; null when no subzone lists it.</summary>
    public int? ZoneOf(int levelAreaSno) => Zones().TryGetValue(levelAreaSno, out int zone) ? zone : null;

    /// <summary>The region's name in <paramref name="locale"/>; null when no subzone lists the LevelArea or the zone has no name text.</summary>
    public string? Name(int levelAreaSno, string locale) =>
        ZoneOf(levelAreaSno) is { } zone && store.Name(zone) is { } name ? store.Text(locale, $"Zone_{name}", "Name") : null;

    // Every subzone is read once (~2000 in build 3.2.3, under a second).
    private Dictionary<int, int> Zones()
    {
        if (_zones is not null) return _zones;
        var zones = new Dictionary<int, int>();
        int group = schema.SnoGroup(Subzone);
        int areasField = schema.Offset(Subzone, "arLevelAreas"), parentField = schema.Offset(Subzone, "snoZoneParent");
        foreach (var (id, _) in store.AllEntries(group))
        {
            if (store.Meta(group, id) is not { } subzone) continue;
            int parent = subzone.I32(parentField);
            if (parent <= 0) continue;
            foreach (int element in subzone.Array(areasField, sizeof(int)))
                if (subzone.I32(element) is > 0 and var area) zones.TryAdd(area, parent);
        }
        return _zones = zones;
    }
}
