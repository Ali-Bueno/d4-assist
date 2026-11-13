using System.Numerics;
using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A stronghold: its quest (the one that frees it) and its spot on the map, where the game draws its icon.</summary>
/// <param name="LevelAreas">The level areas that make it up (Kor Dragan: its outskirts, courtyard, chancery, cathedral and
/// archives), without the transitions leading to it; null or empty when the game data tells none.</param>
public sealed record Stronghold(int QuestSno, string QuestName, int WorldSno, Vector3 Position, IReadOnlySet<int>? LevelAreas = null);

/// <summary>
/// The strongholds the renown counts per region: <c>zone_progress</c>'s <c>ZoneProgressPerZoneData.arCamps</c> quests (3 per region,
/// 21 in build 3.2.4). Each is placed where its quest chain (<c>QuestChainDefinition</c> whose <c>arSteps</c> name the quest) puts its
/// map icon, <c>vecMarkerLocation</c>: the quest's <c>vecStartLocation</c> lies up to 65 units off it (Onyx Watchtower, Qara-Yisu,
/// Temple of Rot: the map cursor landed beside the icon, live 2026-11-13), and only without a chain. The chain's <c>snoSubzone</c> is
/// the stronghold's subzone. Whether one is freed is not read yet: the list holds all.
/// </summary>
public sealed class StrongholdCatalog(SnoStore store, TypeSchema schema)
{
    private const string GlobalDefinition = "GlobalDefinition", Progress = "GlobalZoneProgressData", PerZone = "ZoneProgressPerZoneData",
        Quest = "QuestDefinition", ZoneProgressGlobal = "zone_progress", Subzone = "SubzoneDefinition", Chain = "QuestChainDefinition",
        ChainStep = "QuestChainStep";

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
                    var chain = Chains().GetValueOrDefault(questSno);
                    var position = Usable(chain.Marker) ? chain.Marker : quest.Vec3(location);
                    if (!Usable(position)) continue;
                    result.Add(new Stronghold(questSno, name, quest.I32(world), position));
                }
        return [.. result.DistinctBy(s => s.QuestSno).Select(s => s with { LevelAreas = AreasOf(s, Chains().GetValueOrDefault(s.QuestSno).Subzone) })];
    }

    private static bool Usable(Vector3 position) => position != Vector3.Zero && float.IsFinite(position.X);

    private Dictionary<int, (Vector3 Marker, int Subzone)>? _chains;

    // Each quest's chain: its map icon's spot and its subzone, by the quests its steps name.
    private Dictionary<int, (Vector3 Marker, int Subzone)> Chains()
    {
        if (_chains is not null) return _chains;
        _chains = [];
        int group = schema.SnoGroup(Chain), steps = schema.Offset(Chain, "arSteps"), stride = schema.Size(ChainStep),
            stepQuest = schema.Offset(ChainStep, "snoQuest"), marker = schema.Offset(Chain, "vecMarkerLocation"), subzone = schema.Offset(Chain, "snoSubzone");
        foreach (var (id, _) in store.AllEntries(group))
            if (store.Meta(group, id) is { } chain)
                foreach (int step in chain.Array(steps, stride))
                    _chains.TryAdd(chain.I32(step + stepQuest), (chain.Vec3(marker), chain.I32(subzone)));
        return _chains;
    }

    /// <summary>A stronghold's own subzone lies within this of its quest's spot, map point by map point (Kichuk's spans 236
    /// units); a region's subzone spans 400 and more.</summary>
    public const float SubzoneReachUnits = 300f;

    private IReadOnlyList<MapPoint>? _points;
    private List<int[]>? _subzones;

    // The chain's subzone when it names one; else (the quest names no level area, and the names don't match: CMP_Frac_Malnok
    // lies in Frac_Camp_03) the subzone whose map points all lie around the stronghold's spot, the nearest first.
    private IReadOnlySet<int> AreasOf(Stronghold stronghold, int chainSubzone)
    {
        int subzoneGroup = schema.SnoGroup(Subzone), areas = schema.Offset(Subzone, "arLevelAreas");
        if (chainSubzone > 0 && store.Meta(subzoneGroup, chainSubzone) is { } own)
            return WithoutTransitions([.. own.Array(areas, sizeof(int)).Select(own.I32)]);
        _points ??= new GlobalMapPointReader(store, schema).Read();
        if (_subzones is null)
        {
            _subzones = [];
            foreach (var (id, _) in store.AllEntries(subzoneGroup))
                if (store.Meta(subzoneGroup, id) is { } zone)
                    _subzones.Add([.. zone.Array(areas, sizeof(int)).Select(zone.I32)]);
        }
        var spot = new Vector2(stronghold.Position.X, stronghold.Position.Y);
        float Away(MapPoint p) => Vector2.Distance(new(p.Position.X, p.Position.Y), spot);
        var here = _points.Where(p => p.WorldSno == stronghold.WorldSno).ToList();
        var best = _subzones
            .Select(zone => (zone, points: here.Where(p => zone.Contains(p.LevelAreaSno)).ToList()))
            .Where(z => z.points.Count > 0 && z.points.All(p => Away(p) <= SubzoneReachUnits))
            .OrderBy(z => z.points.Min(Away)).FirstOrDefault();
        return best.zone is null ? new HashSet<int>() : WithoutTransitions(best.zone);
    }

    private HashSet<int> WithoutTransitions(IEnumerable<int> areas) =>
        areas.Where(a => store.Name(a)?.Contains("Trans", StringComparison.OrdinalIgnoreCase) != true).ToHashSet();
}
