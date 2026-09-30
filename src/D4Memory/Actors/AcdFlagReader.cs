using D4Memory.Scan;
using D4Memory.SceneRoute;

namespace D4Memory.Actors;

/// <summary>
/// Named ACD attributes that mark an actor's state, resolved by name in the game's own table and read per actor
/// (research/client-memory.md §4).
/// </summary>
public sealed class AcdFlagReader
{
    /// <summary>Set on use and kept when the actor is re-created (live 2026-09-30: corpses, a herb, a sign, a door).</summary>
    public const string GizmoOperated = "Gizmo_Has_Been_Operated";

    /// <summary>The quest a companion NPC follows the player for (live 2026-09-30: Neyrelle and Vigo, and no other NPC).</summary>
    public const string FollowerQuest = "Follower_Quest";

    // The live descriptor table held 1366 attributes, ids up to 0x571.
    private const uint MaxAttributeId = 0x1000;

    private readonly IMemoryReader _reader;
    private readonly AcdReader _acds;
    private readonly Dictionary<uint, string> _flags;

    private AcdFlagReader(IMemoryReader reader, AcdReader acds, Dictionary<uint, string> flags) =>
        (_reader, _acds, _flags) = (reader, acds, flags);

    /// <summary>Null when the build has no ACD offsets, the pool is unreadable or none of the names is in the table.</summary>
    public static AcdFlagReader? TryOpen(IMemoryReader reader, long moduleBase, GameBuildOffsets offsets, IReadOnlyCollection<string> names)
    {
        if (offsets.Acd is not { } acd || !AcdReader.TryOpen(reader, moduleBase, offsets, out var acds)) return null;
        var table = new AttributeNameTable(reader, moduleBase, acd);
        var flags = new Dictionary<uint, string>();
        for (uint id = 0; id < MaxAttributeId && flags.Count < names.Count; id++)
            if (table.NameOf(id) is { } name && names.Contains(name)) flags[id] = name;
        return flags.Count == 0 ? null : new AcdFlagReader(reader, acds, flags);
    }

    /// <summary>The names found in the game's table; a missing one is never reported set.</summary>
    public IReadOnlyCollection<string> Known => _flags.Values;

    /// <summary>The flags set (nonzero) on the actor behind <paramref name="acdId"/> (an RActor's +0x0C); empty when its ACD is gone.</summary>
    public IReadOnlySet<string> Read(uint acdId) =>
        _acds.TryGet(acdId, out var acd)
            ? AcdAttributeReader.Read(_reader, acd.AttributesAddress)
                .Where(a => a.RawValue != 0 && _flags.ContainsKey(a.AttributeId)).Select(a => _flags[a.AttributeId]).ToHashSet()
            : [];
}
