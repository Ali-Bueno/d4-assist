using System.Numerics;
using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A stronghold: its quest (the one that frees it) and the quest's spot, which lies in the stronghold (Xanderos: 11 units
/// from its waypoint).</summary>
/// <param name="LevelAreas">The level areas that make it up (Kor Dragan: its outskirts, courtyard, chancery, cathedral and
/// archives), without the transitions leading to it; null or empty when the game data tells none.</param>
public sealed record Stronghold(int QuestSno, string QuestName, int WorldSno, Vector3 Position, IReadOnlySet<int>? LevelAreas = null);

/// <summary>
/// The strongholds the renown counts per region: <c>zone_progress</c>'s <c>ZoneProgressPerZoneData.arCamps</c> quests (3 per region,
/// 21 in build 3.2.4), each at its <c>QuestDefinition.vecStartLocation</c>. Whether one is freed is not read yet: the list holds all.
/// </summary>
public sealed class StrongholdCatalog(SnoStore store, TypeSchema schema)
{
    private const string GlobalDefinition = "GlobalDefinition", Progress = "GlobalZoneProgressData", PerZone = "ZoneProgressPerZoneData",
        Quest = "QuestDefinition", ZoneProgressGlobal = "zone_progress", Subzone = "SubzoneDefinition";

    private IReadOnlyList<Stronghold>? _all;

    /// <summary>Every stronghold with a spot; empty when the Global record is unreadable.</summary>
    public IReadOnlyList<Stronghold> All => _all ??= Read();

    /// <summary>The stronghold's name in <paramref name="locale"/> (its quest's, as the game names it: "Xanderos"), else the quest's internal name.</summary>
    public string Name(Stronghold stronghold, string locale) => store.Text(locale, $"Quest_{stronghold.QuestName}", "Name") ?? stronghold.QuestName;

    private List<Stronghold> Read()
    {
        var result = new List<Stronghold>();
        int globalGroup = schema.SnoGroup(GlobalDefinition), questGroup = schema.SnoGroup(Quest);
        int globalSno;
        try { globalSno = store.Resolve(globalGroup, ZoneProgressGlobal); }
        catch (ArgumentException) { return result; }
        if (store.Meta(globalGroup, globalSno) is not { } global) return result;
        int location = schema.Offset(Quest, "vecStartLocation"), world = schema.Offset(Quest, "snoWorldStartLocation");
        int stride = schema.Size(PerZone), camps = schema.Offset(PerZone, "arCamps");
        // arPerZoneData is a fixed array that runs up to the next field.
        int zones = (schema.Offset(Progress, "arPointsPerDataType") - schema.Offset(Progress, "arPerZoneData")) / stride;
        foreach (int content in global.PolymorphicArray(schema.Offset(GlobalDefinition, "ptContent"), schema.Size(Progress)).Take(1))
            for (int zone = 0; zone < zones; zone++)
                foreach (int entry in global.Array(content + schema.Offset(Progress, "arPerZoneData") + zone * stride + camps, sizeof(int)))
                {
                    int questSno = global.I32(entry);
                    if (store.Name(questSno) is not { } name || store.Meta(questGroup, questSno) is not { } quest) continue;
                    var position = quest.Vec3(location);
                    if (position == Vector3.Zero || !float.IsFinite(position.X)) continue;
                    result.Add(new Stronghold(questSno, name, quest.I32(world), position));
                }
        return [.. result.DistinctBy(s => s.QuestSno).Select(s => s with { LevelAreas = AreasOf(s) })];
    }

    /// <summary>A stronghold's own subzone lies within this of its quest's spot, map point by map point (Kichuk's spans 236
    /// units); a region's subzone spans 400 and more.</summary>
    public const float SubzoneReachUnits = 300f;

    private IReadOnlyList<MapPoint>? _points;
    private List<int[]>? _subzones;

    // The quest names no level area (snoLevelArea is unset), and the names don't match (CMP_Frac_Malnok lies in Frac_Camp_03):
    // the stronghold's subzone is the one whose map points all lie around the quest's spot, the nearest first.
    private IReadOnlySet<int> AreasOf(Stronghold stronghold)
    {
        _points ??= new GlobalMapPointReader(store, schema).Read();
        if (_subzones is null)
        {
            int group = schema.SnoGroup(Subzone), areas = schema.Offset(Subzone, "arLevelAreas");
            _subzones = [];
            foreach (var (id, _) in store.AllEntries(group))
                if (store.Meta(group, id) is { } zone)
                    _subzones.Add([.. zone.Array(areas, sizeof(int)).Select(zone.I32)]);
        }
        var spot = new Vector2(stronghold.Position.X, stronghold.Position.Y);
        float Away(MapPoint p) => Vector2.Distance(new(p.Position.X, p.Position.Y), spot);
        var here = _points.Where(p => p.WorldSno == stronghold.WorldSno).ToList();
        var best = _subzones
            .Select(zone => (zone, points: here.Where(p => zone.Contains(p.LevelAreaSno)).ToList()))
            .Where(z => z.points.Count > 0 && z.points.All(p => Away(p) <= SubzoneReachUnits))
            .OrderBy(z => z.points.Min(Away)).FirstOrDefault();
        return best.zone is null ? new HashSet<int>()
            : best.zone.Where(a => store.Name(a)?.Contains("Trans", StringComparison.OrdinalIgnoreCase) != true).ToHashSet();
    }
}
