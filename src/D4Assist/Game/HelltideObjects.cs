using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>
/// The map markers the helltide list offers (live 2026-10-25, the helltide in Hawezar's Hawe_Wetland): gizmos whose actor names
/// the helltide (Helltide_RewardChest_Random, "Tortured Gift of Mysteries", nine across the area; Warplan_Helltide_HellsPrize*,
/// "Hell's Prize"), and the accursed ritual's summoning spot, S4_PROTOTYPE_SummonLocPin. The helltide's monsters (kind 10) and
/// the season's ruptures are left out.
/// </summary>
public static class HelltideObjects
{
    private const string AccursedRitual = "S4_PROTOTYPE_SummonLocPin";

    public static bool Is(int kind, string? actorName) =>
        kind == MapMarkerReader.KindGizmo && actorName is { } name &&
        (name.Contains("Helltide", StringComparison.OrdinalIgnoreCase) || name == AccursedRitual);
}
