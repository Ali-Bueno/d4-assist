using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Decides whether an actor SNO is a way out of the current area: every portal gizmo, plus the switch-type warp pads
/// and portal switches (research/dungeon-objectives.md §7). The player's own town portal is not a dungeon exit.
/// </summary>
/// <param name="definition">ActorDefinition classification of a SNO; null when unreadable.</param>
/// <param name="nameOf">CoreTOC name of a SNO.</param>
public sealed class PortalClassifier(Func<int, ActorDefinitionInfo?> definition, Func<int, string?> nameOf)
{
    private static readonly string[] SwitchPortalTokens = ["Portal", "WarpPad"];
    private const string TownPortalToken = "TownPortal";

    private readonly Dictionary<int, bool> _cache = [];

    public bool IsPortal(int actorSno)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(actorSno, out var cached)) return cached;
            return _cache[actorSno] = Classify(actorSno);
        }
    }

    private bool Classify(int actorSno)
    {
        if (definition(actorSno) is not { } info) return false;
        string name = nameOf(actorSno) ?? "";
        if (name.Contains(TownPortalToken, StringComparison.OrdinalIgnoreCase)) return false;
        return info.GizmoType == Conventions.GizmoPortal
            || (info.GizmoType == Conventions.GizmoSwitch && SwitchPortalTokens.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }
}
