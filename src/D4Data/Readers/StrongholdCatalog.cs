using System.Numerics;
using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A stronghold: its quest (the one that frees it) and the quest's spot, which lies in the stronghold (Xanderos: 11 units
/// from its waypoint).</summary>
public sealed record Stronghold(int QuestSno, string QuestName, int WorldSno, Vector3 Position);

/// <summary>
/// The strongholds the renown counts per region: <c>zone_progress</c>'s <c>ZoneProgressPerZoneData.arCamps</c> quests (3 per region,
/// 21 in build 3.2.4), each at its <c>QuestDefinition.vecStartLocation</c>. Whether one is freed is not read yet: the list holds all.
/// </summary>
public sealed class StrongholdCatalog(SnoStore store, TypeSchema schema)
{
    private const string GlobalDefinition = "GlobalDefinition", Progress = "GlobalZoneProgressData", PerZone = "ZoneProgressPerZoneData",
        Quest = "QuestDefinition", ZoneProgressGlobal = "zone_progress";

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
        return [.. result.DistinctBy(s => s.QuestSno)];
    }
}
