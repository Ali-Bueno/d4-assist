namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>German (deDE).</summary>
    public static readonly NavigationPhrases German = new()
    {
        SingularHeader = "Ausgang",
        PluralHeader = "Ausgänge",
        UnexploredFormat = "{0} unerforscht",
        GroupFormat = "{0} Ausgänge {1}",
        CountWords = ["zwei", "drei", "vier"],
        DeadEnd = "Sackgasse.",
        Entrance = "Eingang.",
        NoOtherExits = "Keine weiteren Ausgänge.",
        AllExplored = "Alles erforscht.",
        Rechecking = "Ausgang nicht gefunden. Prüfe die Sackgassen erneut.",
        ToQuestObjective = "Zum Questziel.",
        ToDungeonObjective = "Zu einem Dungeonziel.",
        ToUnexplored = "Zu unerforschtem Gebiet.",
        QuestObjectiveBelow = "Das Ziel liegt weiter unten.",
        QuestObjectiveAbove = "Das Ziel liegt weiter oben.",
        ShutForNow = "Der Weg ist noch versperrt, er öffnet sich später.",
        Directions = Map("oben", "oben rechts", "rechts", "unten rechts", "unten", "unten links", "links", "oben links"),
        CompassDirections = Map("Nord", "Nordost", "Ost", "Südost", "Süd", "Südwest", "West", "Nordwest"),
        Labels = new() { [RoomKind.Junction] = "Kreuzung.", [RoomKind.BigRoom] = "Großer Raum.", [RoomKind.Corridor] = "Gang." },
    };
}
