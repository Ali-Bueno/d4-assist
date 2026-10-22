using System.Collections.Concurrent;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>Decides whether an actor is a place the player must stand in to be protected (an ally's barrier in a boss fight, a
/// light to stay in), and how close the player must be to stand inside it (research/dungeon-objectives.md §10).</summary>
/// <param name="definition">ActorDefinition classification of a SNO; null when unreadable.</param>
/// <param name="nameOf">CoreTOC name of a SNO.</param>
public sealed class SafeZoneClassifier(Func<int, ActorDefinitionInfo?> definition, Func<int, string?> nameOf)
{
    // Trigger zones named for their shelter: QST_Hell_Battlefield_Donan_SuppressingBubble, BCD_Trigger_EliasPalace_SafeZone,
    // CAP_Tier4_Scos_ArchivesofIssalia_SuppressingShrineTrigger.
    private static readonly string[] TriggerTokens = ["SafeZone", "Bubble", "Suppressing"];

    // Other gizmos named for it: X1_Raid_Switch_BetaMiniBoss_SafeZone. Its visuals (environmentVFX_*_safeZone_*) are no gizmos.
    private const string GizmoToken = "SafeZone";

    // Shelters with no trigger in the client data: the caster of Lilith's bubble (QST_X2_Lilith_Bubble_Caster), the edge of
    // Vigo's dome against Lilith's Lament (knight_brute_barrier_border, an effect), and the lights to stay in
    // (QST_Naha_Lingering_Light, CMP_Naha_GatesOfTheNecropolis_MovingLight, QST_Skov_RathmasArchive_MovingLight).
    private static readonly string[] UnsizedTokens = ["Bubble_Caster", "Barrier_Border", "Lingering_Light", "MovingLight"];

    /// <summary>A shelter with no readable size; the sheltering zones in the data measure 5 to 5.25 units.</summary>
    public const float FallbackRadius = 5f;

    /// <summary>The player counts as inside only this far within the edge, so the cue never drops on the rim.</summary>
    public const float EdgeMargin = 1f;

    // Filled from several poll threads (timer ticks overlap); no lock is held while classifying, which takes the CASC gate.
    private readonly ConcurrentDictionary<int, float?> _cache = new();

    /// <summary>Distance from the actor within which the player is sheltered; null when the actor is no safe zone.</summary>
    public float? InsideRadius(int actorSno)
    {
        if (_cache.TryGetValue(actorSno, out var cached)) return cached;
        return _cache[actorSno] = Classify(actorSno);
    }

    /// <summary>For a light that travels to where the quest needs it and shelters the player on the way, the distance within
    /// which the player is inside it; null for any other actor. Rathma's archive: each spirit fire lit sends a light back to
    /// the golem chamber, and the player walks inside it (live 2026-10-20).</summary>
    public float? EscortRadius(int actorSno) => Has(nameOf(actorSno) ?? "", MovingToken) ? InsideRadius(actorSno) : null;

    private const string MovingToken = "MovingLight";

    private float? Classify(int actorSno)
    {
        if (definition(actorSno) is not { } info || info.DefaultTeam == Conventions.HostileTeam) return null;
        string name = nameOf(actorSno) ?? "";
        float? radius = info.GizmoType == Conventions.GizmoTrigger ? (TriggerTokens.Any(t => Has(name, t)) ? info.TriggerRadius : null)
            : info.GizmoType >= 0 ? (Has(name, GizmoToken) ? FallbackRadius : null)
            : UnsizedTokens.Any(t => Has(name, t)) ? FallbackRadius : null;
        return radius is > EdgeMargin ? radius - EdgeMargin : null;
    }

    private static bool Has(string name, string token) => name.Contains(token, StringComparison.OrdinalIgnoreCase);
}
