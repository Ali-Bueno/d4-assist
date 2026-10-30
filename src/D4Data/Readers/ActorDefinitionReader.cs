using System.Numerics;
using D4Data.Casc;
using D4Data.Format;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>The ActorDefinition fields that classify an actor (looked up by field name in definitions.json).</summary>
/// <param name="BoundsCenter">aabbBounds centre, actor-local (a door: X = thin passage axis, Y = width).</param>
/// <param name="BoundsHalfExtents">aabbBounds half-extents, actor-local.</param>
/// <param name="DefaultTeam">eDefaultTeam: 19 is the hostile monsters' team.</param>
/// <param name="IsNpc">ActorMonsterData.fIsNPC: townsfolk, vendors, quest givers and followers (research/dungeon-objectives.md §9).</param>
/// <param name="MonsterFamilySno">ActorMonsterData.snoMonsterFamily, or -1.</param>
/// <param name="AcceptedCarryables">CarryableReceptacleGizmoData.arCarryables: the carryable SNOs a receptacle takes; null otherwise.</param>
/// <param name="TriggerRadius">A trigger gizmo's ground radius from its proximity shape (sphere or capsule radius, a box's
/// narrower half-extent); null for other actors or an unknown shape (research/dungeon-objectives.md §10).</param>
/// <param name="MonsterRarity">ActorMonsterData.eOverriddenMonsterRarity (Conventions.MonsterRarity*); none for ordinary monsters.</param>
/// <param name="MultiUseSwitch">SwitchGizmoData.fSwitchCanOperateMultipleTimes: a switch used again and again (levers, braziers).</param>
/// <param name="PropCenter">A breakable prop's ground box centre, actor-local (<see cref="PropHalfExtents"/>); zero for other actors.</param>
/// <param name="PropHalfExtents">A breakable prop's box: its collision box, else its appearance box (single props carry only a default
/// collision cylinder: MageHalls_Prop_Furniture_Table_Medium_01_Dyn's appearance is 1.2 x 2.2 units); zero for other actors.</param>
public readonly record struct ActorDefinitionInfo(
    int EType, int GizmoType, Vector3 BoundsCenter = default, Vector3 BoundsHalfExtents = default,
    int DefaultTeam = 0, bool IsNpc = false, int MonsterFamilySno = -1, IReadOnlyList<int>? AcceptedCarryables = null,
    float? TriggerRadius = null, int MonsterRarity = Conventions.MonsterRarityNone, bool MultiUseSwitch = false,
    Vector3 PropCenter = default, Vector3 PropHalfExtents = default);

/// <summary>Reads (and caches per SNO) <see cref="ActorDefinitionInfo"/> from ActorDefinition meta records.</summary>
public sealed class ActorDefinitionReader(SnoStore store, TypeSchema schema)
{
    private const string ActorDefinition = "ActorDefinition";
    private const string Aabb = "AABB";
    private const string MonsterData = "ActorMonsterData";
    private const string ReceptacleData = "CarryableReceptacleGizmoData", SwitchData = "SwitchGizmoData";
    private const string TriggerGizmo = "TriggerGizmoData", Trigger = "TriggerData", TriggerProximity = "TriggerProximityData";
    private const string ShapeBase = "ProximityDataBase", Sphere = "SphereProximityData", Capsule = "CapsuleProximityData", Box = "BoxProximityData";
    private const string CollisionData = "ActorCollisionData";
    private const int SnoIdSize = 4; // DT_SNO

    private readonly Dictionary<int, ActorDefinitionInfo?> _cache = [];

    /// <summary>Null when the SNO has no readable ActorDefinition record — including garbage ids read from a pool
    /// while the world unloads, whose record is some other, smaller type (live crash 2026-09-28).</summary>
    public ActorDefinitionInfo? Read(int actorSno)
    {
        if (_cache.TryGetValue(actorSno, out var cached)) return cached;
        var meta = store.Meta(schema.SnoGroup(ActorDefinition), actorSno);
        int bounds = schema.Offset(ActorDefinition, "aabbBounds");
        ActorDefinitionInfo? info = meta is null || meta.Length < schema.Size(ActorDefinition) ? null : new ActorDefinitionInfo(
            meta.I32(schema.Offset(ActorDefinition, "eType")),
            meta.I32(schema.Offset(ActorDefinition, "eActorGizmoType")),
            meta.Vec3(bounds + schema.Offset(Aabb, "wp")),
            meta.Vec3(bounds + schema.Offset(Aabb, "wvExt")),
            meta.I32(schema.Offset(ActorDefinition, "eDefaultTeam")));
        if (info is { } read && MonsterDataOffset(meta!) is { } monster)
            info = read with
            {
                IsNpc = meta!.I32(monster + schema.Offset(MonsterData, "fIsNPC")) != 0,
                MonsterFamilySno = meta.I32(monster + schema.Offset(MonsterData, "snoMonsterFamily")),
                MonsterRarity = meta.I32(monster + schema.Offset(MonsterData, "eOverriddenMonsterRarity")),
            };
        if (info is { GizmoType: Conventions.GizmoCarryableReceptacle } receptacle)
            info = receptacle with { AcceptedCarryables = AcceptedCarryables(meta!) };
        if (info is { GizmoType: Conventions.GizmoTrigger } trigger)
            info = trigger with { TriggerRadius = TriggerRadius(meta!) };
        if (info is { GizmoType: Conventions.GizmoSwitch } lever)
            info = lever with { MultiUseSwitch = MultiUse(meta!) };
        if (info is { GizmoType: Conventions.GizmoBreakable } prop)
        {
            var (center, half) = PropBox(meta!);
            info = prop with { PropCenter = center, PropHalfExtents = half };
        }
        _cache[actorSno] = info;
        return info;
    }

    // ptCollData's collision box where it has one (the furniture arrangements), else the appearance box.
    private (Vector3 Center, Vector3 Half) PropBox(SnoBlob meta)
    {
        foreach (var data in meta.Array(schema.Offset(ActorDefinition, "ptCollData"), schema.Size(CollisionData)).Take(1))
        {
            int collision = data + schema.Offset(CollisionData, "aabbCollision");
            var half = meta.Vec3(collision + schema.Offset(Aabb, "wvExt"));
            if (half.X > 0 && half.Y > 0) return (meta.Vec3(collision + schema.Offset(Aabb, "wp")), half);
        }
        int appearance = schema.Offset(ActorDefinition, "aabbAppearanceBoundingBox");
        return (meta.Vec3(appearance + schema.Offset(Aabb, "wp")), meta.Vec3(appearance + schema.Offset(Aabb, "wvExt")));
    }

    // ptGizmoData holds one SwitchGizmoData.
    private bool MultiUse(SnoBlob meta) =>
        meta.PolymorphicArray(schema.Offset(ActorDefinition, "ptGizmoData"), schema.Size(SwitchData)).Take(1)
            .Any(gizmo => meta.I32(gizmo + schema.Offset(SwitchData, "fSwitchCanOperateMultipleTimes")) != 0);

    // ptGizmoData holds one polymorphic element, here a CarryableReceptacleGizmoData.
    private int[] AcceptedCarryables(SnoBlob meta) =>
        [.. meta.PolymorphicArray(schema.Offset(ActorDefinition, "ptGizmoData"), schema.Size(ReceptacleData)).Take(1)
            .SelectMany(gizmo => meta.Array(gizmo + schema.Offset(ReceptacleData, "arCarryables"), SnoIdSize))
            .Select(meta.I32)];

    // ptGizmoData holds one TriggerGizmoData; its first proximity shape sizes the zone.
    private float? TriggerRadius(SnoBlob meta)
    {
        int gizmo = meta.PolymorphicArray(schema.Offset(ActorDefinition, "ptGizmoData"), schema.Size(TriggerGizmo)).FirstOrDefault(-1);
        if (gizmo < 0) return null;
        int shapes = gizmo + schema.Offset(TriggerGizmo, "tTriggerData") + schema.Offset(Trigger, "tTriggerProxData")
            + schema.Offset(TriggerProximity, "ptProxData");
        if (meta.PolymorphicElements(shapes, p => ShapeAt(meta, p) is { } t ? schema.Size(t) : null) is not [var shape, ..]) return null;
        return ShapeAt(meta, shape) switch
        {
            Sphere => meta.F32(shape + schema.Offset(Sphere, "flRadius")),
            Capsule => meta.F32(shape + schema.Offset(Capsule, "flRadius")),
            Box => Narrower(meta.Vec3(shape + schema.Offset(Box, "wvOBB"))),
            _ => null,
        };
    }

    private static float Narrower(Vector3 halfExtents) => Math.Min(halfExtents.X, halfExtents.Y);

    // A proximity shape names its own type in dwType (the type hash).
    private string? ShapeAt(SnoBlob meta, int shape)
    {
        int typeOffset = shape + schema.Offset(ShapeBase, "dwType");
        return typeOffset + sizeof(uint) <= meta.Length ? schema.TypeName(meta.U32(typeOffset)) : null;
    }

    // Where the actor's ActorMonsterData block starts in its record, or null when it has none.
    private int? MonsterDataOffset(SnoBlob meta)
    {
        var (data, size) = meta.ArrayDescriptor(schema.Offset(ActorDefinition, "ptMonsterData"));
        return data > 0 && size >= schema.Size(MonsterData) && data + schema.Size(MonsterData) <= meta.Length ? data : null;
    }

    /// <summary>Every player-class actor SNO (one per class and sex), read from the data so a new class needs no code change.</summary>
    public HashSet<int> PlayerActorSnos()
    {
        int group = schema.SnoGroup(ActorDefinition);
        int eType = schema.Offset(ActorDefinition, "eType");
        return store.AllEntries(group)
            .Where(entry => store.Meta(group, entry.Id) is { } meta && meta.Length >= schema.Size(ActorDefinition)
                && meta.I32(eType) == Conventions.ActorTypePlayer)
            .Select(entry => entry.Id)
            .ToHashSet();
    }
}
