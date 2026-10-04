namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>French (frFR).</summary>
    public static readonly NavigationPhrases French = new()
    {
        SingularHeader = "Sortie",
        PluralHeader = "Sorties",
        UnexploredFormat = "{0} inexplorée",
        GroupFormat = "{0} sorties {1}",
        CountWords = ["deux", "trois", "quatre"],
        DeadEnd = "Cul-de-sac.",
        Entrance = "Entrée.",
        NoOtherExits = "Pas d'autre sortie.",
        AllExplored = "Tout est exploré.",
        Rechecking = "Sortie introuvable. Nouvelle vérification des culs-de-sac.",
        ToQuestObjective = "Vers l'objectif de la quête.",
        ToDungeonObjective = "Vers un objectif du donjon.",
        ToUnexplored = "Vers une zone inexplorée.",
        QuestObjectiveBelow = "L'objectif est plus bas.",
        QuestObjectiveAbove = "L'objectif est plus haut.",
        Directions = Map("haut", "haut droite", "droite", "bas droite", "bas", "bas gauche", "gauche", "haut gauche"),
        CompassDirections = Map("nord", "nord-est", "est", "sud-est", "sud", "sud-ouest", "ouest", "nord-ouest"),
        Labels = new() { [RoomKind.Junction] = "Carrefour.", [RoomKind.BigRoom] = "Grande salle.", [RoomKind.Corridor] = "Couloir." },
        ExitsFormat = "{0} {1} : {2}.",
    };
}
