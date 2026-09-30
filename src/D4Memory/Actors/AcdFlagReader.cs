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

    /// <summary>A gizmo the quest controls (live 2026-09-30, Darkened Way: a scripted gate not usable yet, and the demon body
    /// that is the quest step); MinimapActive tells the two apart.</summary>
    public const string GizmoQuestRestricted = "Gizmo_Is_Quest_Restricted";

    /// <summary>Shown on the minimap (live 2026-09-30: the usable quest body, NPCs, an elevator; not the locked quest gate).</summary>
    public const string MinimapActive = "MinimapActive";

    /// <summary>A gizmo switched off by a game script; by name only, not seen set yet.</summary>
    public const string GizmoDisabledByScript = "Gizmo_Disabled_By_Script";

    /// <summary>The player's tracked quest SNO, chosen in the quest log (live 2026-09-30: follows the campaign quest as it advances).</summary>
    public const string TrackedQuest = "ManuallyTrackedQuestSNO";

    /// <summary>A tracked quest the game imposes over the player's choice; by name only, not seen set yet.</summary>
    public const string OverrideTrackedQuest = "OverrideTrackedQuestSNO";

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
    public IReadOnlySet<string> Read(uint acdId) => Values(acdId).Keys.ToHashSet();

    /// <summary>The raw values of the flags set (nonzero) on the actor behind <paramref name="acdId"/>; empty when its ACD is gone.</summary>
    public IReadOnlyDictionary<string, uint> Values(uint acdId)
    {
        var values = new Dictionary<string, uint>();
        if (!_acds.TryGet(acdId, out var acd)) return values;
        foreach (var a in AcdAttributeReader.Read(_reader, acd.AttributesAddress))
            if (a.RawValue != 0 && _flags.TryGetValue(a.AttributeId, out var name)) values[name] = a.RawValue;
        return values;
    }
}
