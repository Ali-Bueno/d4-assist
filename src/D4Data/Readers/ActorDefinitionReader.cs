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
public readonly record struct ActorDefinitionInfo(
    int EType, int GizmoType, Vector3 BoundsCenter = default, Vector3 BoundsHalfExtents = default,
    int DefaultTeam = 0, bool IsNpc = false, int MonsterFamilySno = -1, IReadOnlyList<int>? AcceptedCarryables = null);

/// <summary>Reads (and caches per SNO) <see cref="ActorDefinitionInfo"/> from ActorDefinition meta records.</summary>
public sealed class ActorDefinitionReader(SnoStore store, TypeSchema schema)
{
    private const string ActorDefinition = "ActorDefinition";
    private const string Aabb = "AABB";
    private const string MonsterData = "ActorMonsterData";
    private const string ReceptacleData = "CarryableReceptacleGizmoData";
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
            };
        if (info is { GizmoType: Conventions.GizmoCarryableReceptacle } receptacle)
            info = receptacle with { AcceptedCarryables = AcceptedCarryables(meta!) };
        _cache[actorSno] = info;
        return info;
    }

    // ptGizmoData holds one polymorphic element, here a CarryableReceptacleGizmoData.
    private int[] AcceptedCarryables(SnoBlob meta) =>
        [.. meta.PolymorphicArray(schema.Offset(ActorDefinition, "ptGizmoData"), schema.Size(ReceptacleData)).Take(1)
            .SelectMany(gizmo => meta.Array(gizmo + schema.Offset(ReceptacleData, "arCarryables"), SnoIdSize))
            .Select(meta.I32)];

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
