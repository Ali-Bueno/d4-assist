using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>A waypoint the player can travel to: its LevelArea (what the player's saved data lists once activated), world and spot.</summary>
public sealed record Waypoint(int LevelAreaSno, string LevelAreaName, int WorldSno, Vector3 Position);

/// <summary>
/// Every waypoint of the game, from the Global <c>global_markers</c> record (GlobalWaypointData.arGlobalMarkerActors whose
/// ptData is a GlobalMarkerActorWaypointData; 53 in build 3.2.3: towns of the open world, the Mercenary hideout, season hubs).
/// Names come from the StringList <c>LevelArea_&lt;name&gt;</c>, label <c>Name</c> (deDE: Skov_Philios_OracleTown → "Visionsende").
/// </summary>
public sealed class WaypointCatalog(SnoStore store, TypeSchema schema)
{
    private const string GlobalDefinition = "GlobalDefinition", GlobalMarkers = "global_markers";
    private const string WaypointData = "GlobalWaypointData", MarkerActor = "GlobalMarkerActor", MarkerWaypointData = "GlobalMarkerActorWaypointData";
    // A polymorphic element's type hash sits after its 8-byte header (DT_POLYMORPHIC: dwType at +8, research/dungeon-data.md).
    private const int TypeHashField = 8;

    private IReadOnlyList<Waypoint>? _all;

    /// <summary>Every waypoint; empty when the Global record is unreadable.</summary>
    public IReadOnlyList<Waypoint> All => _all ??= Read();

    /// <summary>The waypoint's name in <paramref name="locale"/> (a D4 code such as deDE), else its LevelArea's internal name.</summary>
    public string Name(Waypoint waypoint, string locale) =>
        store.Text(locale, $"LevelArea_{waypoint.LevelAreaName}", "Name") ?? waypoint.LevelAreaName;

    private List<Waypoint> Read()
    {
        int group = schema.SnoGroup(GlobalDefinition);
        if (store.Meta(group, store.Resolve(group, GlobalMarkers)) is not { } global) return [];
        var waypoints = new List<Waypoint>();
        foreach (int content in Elements(global, schema.Offset(GlobalDefinition, "ptContent")))
        {
            if (TypeOf(global, content) != WaypointData) continue;
            foreach (int marker in global.Array(content + schema.Offset(WaypointData, "arGlobalMarkerActors"), schema.Size(MarkerActor)))
            {
                if (!Elements(global, marker + schema.Offset(MarkerActor, "ptData")).Any(d => TypeOf(global, d) == MarkerWaypointData)) continue;
                int levelArea = global.I32(marker + schema.Offset(MarkerActor, "snoLevelArea"));
                if (levelArea <= 0 || store.Name(levelArea) is not { } name) continue;
                var position = global.Vec3(marker + schema.Offset(MarkerActor, "tWorldTransform") + schema.Offset("PRTransform", "wp"));
                waypoints.Add(new Waypoint(levelArea, name, global.I32(marker + schema.Offset(MarkerActor, "snoWorld")), position));
            }
        }
        return waypoints;
    }

    private IEnumerable<int> Elements(SnoBlob blob, int field) =>
        blob.PolymorphicElements(field, p => TypeOf(blob, p) is { } type ? schema.Size(type) : null) ?? [];

    private string? TypeOf(SnoBlob blob, int element) => schema.TypeName(blob.U32(element + TypeHashField));
}
