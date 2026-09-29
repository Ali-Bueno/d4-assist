using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Readers;

/// <summary>The ActorDefinition fields that classify an actor (looked up by field name in definitions.json).</summary>
public readonly record struct ActorDefinitionInfo(int EType, int GizmoType);

/// <summary>Reads (and caches per SNO) <see cref="ActorDefinitionInfo"/> from ActorDefinition meta records.</summary>
public sealed class ActorDefinitionReader(SnoStore store, TypeSchema schema)
{
    private const string ActorDefinition = "ActorDefinition";

    private readonly Dictionary<int, ActorDefinitionInfo?> _cache = [];

    /// <summary>Null when the SNO has no readable ActorDefinition record — including garbage ids read from a pool
    /// while the world unloads, whose record is some other, smaller type (live crash 2026-09-28).</summary>
    public ActorDefinitionInfo? Read(int actorSno)
    {
        if (_cache.TryGetValue(actorSno, out var cached)) return cached;
        var meta = store.Meta(schema.SnoGroup(ActorDefinition), actorSno);
        ActorDefinitionInfo? info = meta is null || meta.Length < schema.Size(ActorDefinition) ? null : new ActorDefinitionInfo(
            meta.I32(schema.Offset(ActorDefinition, "eType")),
            meta.I32(schema.Offset(ActorDefinition, "eActorGizmoType")));
        _cache[actorSno] = info;
        return info;
    }
}
