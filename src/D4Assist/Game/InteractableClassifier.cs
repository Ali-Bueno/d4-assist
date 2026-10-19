using System.Collections.Concurrent;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>What a non-objective gizmo is to the player: something to open, loot, read, look at (again and again) or drink from.</summary>
public enum InteractableKind { Door, Chest, Readable, PointOfInterest, HealingWell }

/// <summary>A classified interactable and the D2R loop it gets (a WAV under Sounds/).</summary>
public readonly record struct Interactable(InteractableKind Kind, string Sound);

/// <summary>Classifies gizmos by ActorDefinition gizmo type and name (research/dungeon-objectives.md §6).</summary>
/// <param name="definition">ActorDefinition classification of a SNO; null when unreadable.</param>
/// <param name="nameOf">CoreTOC name of a SNO.</param>
public sealed class InteractableClassifier(Func<int, ActorDefinitionInfo?> definition, Func<int, string?> nameOf)
{
    /// <summary>Sound ids: D2R's exploration door loops and its chest, scroll and corpse loot loops.</summary>
    public const string WoodenDoorSound = "wooden-door", MetalDoorSound = "metal-door", StoneDoorSound = "stone-door",
        ChestSound = "chest", ScrollSound = "scroll", CorpseSound = "corpse";

    /// <summary>Sound id of the healing wells: D2R's well loop, Diablo IV's own drink sound (d2r-access docs/decisions.md).</summary>
    public const string HealingWellSound = "healing-well";

    // Healing_Well_Basic, _Outdoor, _Reusable, _Milk; Healing_Well_Empty is no gizmo (research/dungeon-objectives.md §22).
    private const string HealingWellPrefix = "Healing_Well_";

    // No material field in DoorGizmoData, so the door sound comes from its name; anything else sounds wooden.
    private static readonly string[] MetalTokens = ["Gate", "Portcullis", "Cage", "Bars", "Grate", "Iron"];
    private static readonly string[] StoneTokens = ["Stone", "Crypt", "Tomb", "Statue", "Blocker"];
    // Doors that start open (arena doors that shut during a fight): D2R loops closed doors only.
    private const string StartsOpenToken = "StartOpen";
    // Lore books placed as chests (Generic_Lorebook_Chest_01_Dyn) read like books, not loot.
    private const string LorebookToken = "Lorebook";
    // A body on the ground sounds like one, whether you loot it or read it (user request 2026-09-29).
    // "Dead" is left out: it also names quests (QST_Skov_DeadWeight_*). Hanged/Crucified name bodies read as signs
    // (TWN_Frac_Yelesna_Sign_VampireHanged, TWN_Kehj_GeaKul_Sign_HangedMan, TWN_Kehj_Tarsarak_Sign_CrucifiedNpc).
    private static readonly string[] BodyTokens = ["Corpse", "Remains", "Body", "Hanged", "Crucified"];

    private static readonly HashSet<int> ChestGizmos = [Conventions.GizmoChest, Conventions.GizmoRewardChest, Conventions.GizmoLockedChest];

    // Filled from several poll threads (timer ticks overlap); no lock is held while classifying, which takes the CASC gate.
    private readonly ConcurrentDictionary<int, Interactable?> _cache = new();

    public Interactable? Classify(int actorSno)
    {
        if (_cache.TryGetValue(actorSno, out var cached)) return cached;
        return _cache[actorSno] = definition(actorSno) is { } info ? Of(info.GizmoType, nameOf(actorSno) ?? "") : null;
    }

    private static Interactable? Of(int gizmoType, string name)
    {
        // A point of interest reads like a sign (user request 2026-09-30: "mirar"); it stays usable after a look.
        if (gizmoType == Conventions.GizmoLookAt) return new Interactable(InteractableKind.PointOfInterest, ScrollSound);
        if (gizmoType == Conventions.GizmoShrine)
            return name.StartsWith(HealingWellPrefix, StringComparison.OrdinalIgnoreCase) ? new Interactable(InteractableKind.HealingWell, HealingWellSound) : null;
        if (gizmoType == Conventions.GizmoDoor)
            return Has(name, StartsOpenToken) ? null : new Interactable(InteractableKind.Door, DoorSound(name));
        bool body = BodyTokens.Any(t => Has(name, t));
        if (gizmoType == Conventions.GizmoSign || (ChestGizmos.Contains(gizmoType) && Has(name, LorebookToken)))
            return new Interactable(InteractableKind.Readable, body ? CorpseSound : ScrollSound);
        return ChestGizmos.Contains(gizmoType) ? new Interactable(InteractableKind.Chest, body ? CorpseSound : ChestSound) : null;
    }

    private static string DoorSound(string name) =>
        MetalTokens.Any(t => Has(name, t)) ? MetalDoorSound
        : StoneTokens.Any(t => Has(name, t)) ? StoneDoorSound
        : WoodenDoorSound;

    private static bool Has(string name, string token) => name.Contains(token, StringComparison.OrdinalIgnoreCase);
}
