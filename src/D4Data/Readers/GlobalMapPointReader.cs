using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

public enum MapPointKind { Waypoint, Portal, PortalDestination, HiddenCache, Other }

/// <summary>What a portal marker says about where it leads (GlobalMarkerActorPortalData).</summary>
public sealed record PortalInfo(
    uint EntranceNameToFind, uint PortalType, int SpecifiedWorld, int DestSubzone, int DestSubzoneType, uint DestSubzoneFlags,
    uint DungeonArchetype, int DestLevelArea, int DestObjectiveQuest, bool HiddenMapIcon);

/// <summary>One marker actor of a Global GlobalWaypointData record: a waypoint, dungeon entrance or other map point.</summary>
/// <param name="EntranceName">dwEntranceName of a waypoint / destination marker (0 for the other kinds).</param>
/// <param name="ZoneLabel">hZoneLabel of a waypoint marker (0 for the other kinds).</param>
public sealed record MapPoint(
    int GlobalSno, MapPointKind Kind, string TypeName, int MarkerSet, int MarkerPathId, int ActorSno, int ActorType, int GizmoType,
    int WorldSno, Vector3 Position, int LevelAreaSno, int Subzones, uint EntranceName, uint ZoneLabel, PortalInfo? Portal);

/// <summary>Reads GlobalWaypointData.arGlobalMarkerActors from every Global SNO that holds one; all offsets come from the schema.</summary>
public sealed class GlobalMapPointReader(SnoStore store, TypeSchema schema)
{
    private const string GlobalDefinition = "GlobalDefinition", MarkerActor = "GlobalMarkerActor", WaypointData = "GlobalWaypointData",
        WaypointMarker = "GlobalMarkerActorWaypointData", PortalMarker = "GlobalMarkerActorPortalData",
        DestinationMarker = "GlobalMarkerActorPortalDestData", HiddenCacheMarker = "GlobalMarkerActorHiddenCacheData", Transform = "PRTransform";

    /// <summary>Every marker of every Global that holds map points; empty when none is readable.</summary>
    public IReadOnlyList<MapPoint> Read()
    {
        int group = schema.SnoGroup(GlobalDefinition);
        var points = new List<MapPoint>();
        foreach (var (id, _) in store.AllEntries(group))
            if (store.Meta(group, id) is { } global)
                points.AddRange(ReadGlobal(id, global));
        return points;
    }

    private IEnumerable<MapPoint> ReadGlobal(int globalSno, SnoBlob global)
    {
        var contents = global.PolymorphicElements(schema.Offset(GlobalDefinition, "ptContent"), p => schema.TypeName(global.U32(p + schema.Offset(WaypointData, "dwType"))) is { } t ? schema.Size(t) : null);
        if (contents is null) yield break;
        foreach (int content in contents.Where(c => schema.TypeName(global.U32(c + schema.Offset(WaypointData, "dwType"))) == WaypointData))
            foreach (int actor in global.Array(content + schema.Offset(WaypointData, "arGlobalMarkerActors"), schema.Size(MarkerActor)))
                yield return ReadActor(globalSno, global, actor);
    }

    private MapPoint ReadActor(int globalSno, SnoBlob g, int a)
    {
        int Field(string name) => a + schema.Offset(MarkerActor, name);
        var (kind, typeName, data) = ReadData(g, Field("ptData"));
        int transform = Field("tWorldTransform");
        return new MapPoint(globalSno, kind, typeName, g.I32(Field("snoMarkerSet")), g.I32(Field("dwMarkerPathID")), g.I32(Field("snoActor")),
            g.I32(Field("eActorType")), g.I32(Field("eGizmoType")), g.I32(Field("snoWorld")), g.Vec3(transform + schema.Offset(Transform, "wp")),
            g.I32(Field("snoLevelArea")), g.ArrayDescriptor(Field("arSubzones")).Size / sizeof(int), data.EntranceName, data.ZoneLabel, data.Portal);
    }

    private readonly record struct Variant(uint EntranceName, uint ZoneLabel, PortalInfo? Portal);

    // ptData holds at most one variant, which names its own type in dwType (the type hash).
    private (MapPointKind, string, Variant) ReadData(SnoBlob g, int array)
    {
        int typeAt(int p) => p + schema.Offset(WaypointMarker, "dwType");
        string? NameAt(int p) => typeAt(p) + sizeof(uint) <= g.Length ? schema.TypeName(g.U32(typeAt(p))) : null;
        if (g.PolymorphicElements(array, p => NameAt(p) is { } t ? schema.Size(t) : null) is not [var p, ..]) return (MapPointKind.Other, "", default);
        string type = NameAt(p)!;
        uint U(string t, string f) => g.U32(p + schema.Offset(t, f));
        int I(string t, string f) => g.I32(p + schema.Offset(t, f));
        return type switch
        {
            WaypointMarker => (MapPointKind.Waypoint, type, new Variant(U(type, "dwEntranceName"), U(type, "hZoneLabel"), null)),
            DestinationMarker => (MapPointKind.PortalDestination, type, new Variant(U(type, "dwEntranceName"), 0, null)),
            HiddenCacheMarker => (MapPointKind.HiddenCache, type, default),
            PortalMarker => (MapPointKind.Portal, type, new Variant(0, 0, new PortalInfo(
                U(type, "dwEntranceNameToFind"), U(type, "dwPortalType"), I(type, "snoSpecifiedWorld"), I(type, "snoPortalDestSubzone"),
                I(type, "ePortalDestSubzoneType"), U(type, "dwPortalDestSubzoneFlags"), U(type, "gbidDungeonArchetype"), I(type, "snoPortalDestLevelArea"),
                I(type, "snoPortalDestObjectiveQuest"), I(type, "fHiddenMapIcon") != 0))),
            _ => (MapPointKind.Other, type, default),
        };
    }
}
