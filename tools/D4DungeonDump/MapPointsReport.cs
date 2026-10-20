using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4DungeonDump;

/// <summary>--map-points: the overworld map points (waypoints, dungeon entrances, ...) of the Global records, one line each.</summary>
internal static class MapPointsReport
{
    public static void Print(SnoStore store, TypeSchema schema, string[] locales, TextWriter output)
    {
        var points = new GlobalMapPointReader(store, schema).Read();
        var names = new LevelAreaNameReader(store, schema);
        output.WriteLine($"{points.Count} map points, build {store.BuildVersion}");
        foreach (var p in points)
        {
            string at = $"({p.Position.X:0.#}, {p.Position.Y:0.#}, {p.Position.Z:0.#})";
            output.WriteLine($"{p.Kind,-17} {Name(store, p.ActorSno)} type {p.ActorType}/{p.GizmoType} world {Name(store, p.WorldSno)} {at} area {Name(store, p.LevelAreaSno)}" +
                             LabelOf(p, names, locales) +
                             $" entrance {p.EntranceName:x8} zone {p.ZoneLabel:x8}" + (p.Portal is { } portal ? $" portal {portal}" : ""));
        }
    }

    // Waypoints are named after their own level area, dungeon entrances after the area they lead to.
    private static string LabelOf(MapPoint p, LevelAreaNameReader names, string[] locales)
    {
        int area = p.Portal?.DestLevelArea > 0 ? p.Portal.DestLevelArea : p.LevelAreaSno;
        return " " + string.Join(" | ", locales.Select(l => $"{l}: {names.Name(area, l) ?? "(no name)"}"));
    }

    private static string Name(SnoStore store, int sno) => sno <= 0 ? "-" : store.Name(sno) ?? $"#{sno}";
}
