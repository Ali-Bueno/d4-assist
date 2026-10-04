namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>Italian (itIT).</summary>
    public static readonly NavigationPhrases Italian = new()
    {
        SingularHeader = "Uscita",
        PluralHeader = "Uscite",
        UnexploredFormat = "{0} inesplorata",
        GroupFormat = "{0} uscite {1}",
        CountWords = ["due", "tre", "quattro"],
        DeadEnd = "Vicolo cieco.",
        Entrance = "Ingresso.",
        NoOtherExits = "Nessun'altra uscita.",
        AllExplored = "Tutto esplorato.",
        Rechecking = "Uscita non trovata. Ricontrollo i vicoli ciechi.",
        ToQuestObjective = "Verso l'obiettivo della missione.",
        ToDungeonObjective = "Verso un obiettivo del dungeon.",
        ToUnexplored = "Verso una zona inesplorata.",
        QuestObjectiveBelow = "L'obiettivo è più in basso.",
        QuestObjectiveAbove = "L'obiettivo è più in alto.",
        Directions = Map("su", "su a destra", "destra", "giù a destra", "giù", "giù a sinistra", "sinistra", "su a sinistra"),
        CompassDirections = Map("nord", "nord-est", "est", "sud-est", "sud", "sud-ovest", "ovest", "nord-ovest"),
        Labels = new() { [RoomKind.Junction] = "Incrocio.", [RoomKind.BigRoom] = "Sala grande.", [RoomKind.Corridor] = "Corridoio." },
    };
}
