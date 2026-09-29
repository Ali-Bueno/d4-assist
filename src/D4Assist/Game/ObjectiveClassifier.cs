using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>Why an actor counts as a dungeon objective (logged with the loop).</summary>
public enum ObjectiveReason { Dpo, Gizmo, Prisoner }

/// <summary>Decides whether an actor SNO is a dungeon objective to interact with (research/dungeon-objectives.md §3-4).</summary>
/// <param name="definition">ActorDefinition classification of a SNO; null when unreadable.</param>
/// <param name="nameOf">CoreTOC name of a SNO.</param>
public sealed class ObjectiveClassifier(Func<int, ActorDefinitionInfo?> definition, Func<int, string?> nameOf)
{
    // Free-able prisoners are named DGN_NPC_<Region>_Prisoner_*; "Dead" excludes DGN_Standard_DeadPrisoner_*.
    private const string PrisonerToken = "Prisoner", DeadToken = "Dead";

    // Gizmo types that are objectives in every dungeon.
    private static readonly HashSet<int> AlwaysGizmos =
    [
        Conventions.GizmoCarryable, Conventions.GizmoCarryableReceptacle, Conventions.GizmoQuestSwitch, Conventions.GizmoQuestChest,
    ];

    // The world-independent part of the answer, cached per actor SNO.
    private readonly record struct Traits(ObjectiveReason? Fixed, bool IsMonster);

    private readonly Dictionary<int, Traits?> _traits = [];

    /// <param name="dpoActors">Actors the current dungeon's DPO quest names (DungeonObjectiveReader.ActorSnos).</param>
    public ObjectiveReason? Classify(int actorSno, IReadOnlySet<int> dpoActors)
    {
        if (TraitsOf(actorSno) is not { } traits) return null;
        if (traits.Fixed is { } reason) return reason;
        return !traits.IsMonster && dpoActors.Contains(actorSno) ? ObjectiveReason.Dpo : null;
    }

    /// <summary>True for monsters and NPCs, whose state changes as they animate: a gizmo-only "used" signal does not apply.</summary>
    public bool IsMonster(int actorSno) => TraitsOf(actorSno)?.IsMonster == true;

    private Traits? TraitsOf(int actorSno)
    {
        if (_traits.TryGetValue(actorSno, out var cached)) return cached;
        Traits? traits = definition(actorSno) is { } info
            ? new Traits(FixedReason(actorSno, info), info.EType == Conventions.ActorTypeMonster)
            : null;
        return _traits[actorSno] = traits;
    }

    private ObjectiveReason? FixedReason(int actorSno, ActorDefinitionInfo info)
    {
        if (IsPrisoner(nameOf(actorSno), info)) return ObjectiveReason.Prisoner;
        return AlwaysGizmos.Contains(info.GizmoType) ? ObjectiveReason.Gizmo : null;
    }

    private static bool IsPrisoner(string? name, ActorDefinitionInfo info) =>
        name is not null
        && name.Contains(PrisonerToken, StringComparison.OrdinalIgnoreCase)
        && !name.Contains(DeadToken, StringComparison.OrdinalIgnoreCase)
        && (info.EType == Conventions.ActorTypeMonster || info.GizmoType == Conventions.GizmoSwitch);
}
