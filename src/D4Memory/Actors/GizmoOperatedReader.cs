using D4Memory.Scan;
using D4Memory.SceneRoute;

namespace D4Memory.Actors;

/// <summary>
/// The game's own "used" mark on a gizmo: the ACD attribute Gizmo_Has_Been_Operated, set on use and kept when the
/// actor is re-created (research/client-memory.md §4, live 2026-09-30: corpses, a herb, a sign, a door).
/// </summary>
public sealed class GizmoOperatedReader
{
    public const string AttributeName = "Gizmo_Has_Been_Operated";

    // The live descriptor table held 1366 attributes, ids up to 0x571.
    private const uint MaxAttributeId = 0x1000;

    private readonly IMemoryReader _reader;
    private readonly AcdReader _acds;
    private readonly uint _attributeId;

    private GizmoOperatedReader(IMemoryReader reader, AcdReader acds, uint attributeId) =>
        (_reader, _acds, _attributeId) = (reader, acds, attributeId);

    /// <summary>Null when the build has no ACD offsets, the pool is unreadable or the attribute isn't named in the table.</summary>
    public static GizmoOperatedReader? TryOpen(IMemoryReader reader, long moduleBase, GameBuildOffsets offsets)
    {
        if (offsets.Acd is not { } acd || !AcdReader.TryOpen(reader, moduleBase, offsets, out var acds)) return null;
        var names = new AttributeNameTable(reader, moduleBase, acd);
        for (uint id = 0; id < MaxAttributeId; id++)
            if (names.NameOf(id) == AttributeName) return new GizmoOperatedReader(reader, acds, id);
        return null;
    }

    /// <summary>True once the game marked the actor behind <paramref name="acdId"/> (an RActor's +0x0C) as operated.</summary>
    public bool IsOperated(uint acdId) =>
        _acds.TryGet(acdId, out var acd)
        && AcdAttributeReader.Read(_reader, acd.AttributesAddress).Any(a => a.AttributeId == _attributeId && a.RawValue != 0);
}
