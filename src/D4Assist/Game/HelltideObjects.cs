using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>
/// The map markers the helltide list offers (live 2026-10-25, the helltide in Hawezar's Hawe_Wetland): gizmos whose actor names
/// the helltide (Helltide_RewardChest_Random, "Tortured Gift of Mysteries", nine across the area), the accursed ritual's summoning
/// spot (S4_PROTOTYPE_SummonLocPin), the helltide's events, marked as monsters (SMP_Helltide_FallenAltar_Structure "Gefallener
/// Altar", SMP_Helltide_Structure_DemonicSpire "Spitze der Verderbnis"), and the doomsayer (NPC_QST_S04_*_Doomsayer_*).
/// Left out: Hell's prize (Warplan_Helltide_HellsPrize*, a reward marker the user found led nowhere), the helltide's monsters and
/// the season's ruptures.
/// </summary>
public static class HelltideObjects
{
    private const string AccursedRitual = "S4_PROTOTYPE_SummonLocPin", Events = "SMP_Helltide_", HellsPrize = "Warplan_Helltide_HellsPrize";

    public static bool Is(int kind, string? actorName) => actorName is { } name && !name.StartsWith(HellsPrize, StringComparison.Ordinal) &&
        (kind == MapMarkerReader.KindGizmo && (name.Contains("Helltide", StringComparison.OrdinalIgnoreCase) || name == AccursedRitual)
         || name.StartsWith(Events, StringComparison.Ordinal) && !name.EndsWith("_spawner", StringComparison.OrdinalIgnoreCase)
         || name.StartsWith("NPC_QST_S04_", StringComparison.Ordinal) && name.Contains("_Doomsayer", StringComparison.Ordinal));
}
