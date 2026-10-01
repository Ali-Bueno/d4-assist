namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>Polish (plPL).</summary>
    public static readonly NavigationPhrases Polish = new()
    {
        SingularHeader = "Wyjście",
        PluralHeader = "Wyjścia",
        UnexploredFormat = "{0}, niezbadane",
        GroupFormat = "{0} wyjścia {1}",
        CountWords = ["dwa", "trzy", "cztery"],
        DeadEnd = "Ślepy zaułek.",
        Entrance = "Wejście.",
        NoOtherExits = "Brak innych wyjść.",
        AllExplored = "Wszystko zbadane.",
        Rechecking = "Nie znaleziono wyjścia. Sprawdzam ponownie ślepe zaułki.",
        ToQuestObjective = "Do celu zadania.",
        ToDungeonObjective = "Do celu w lochu.",
        ToUnexplored = "W stronę niezbadanego terenu.",
        QuestObjectiveBelow = "Cel jest niżej.",
        QuestObjectiveAbove = "Cel jest wyżej.",
        Directions = Map("góra", "góra, prawo", "prawo", "dół, prawo", "dół", "dół, lewo", "lewo", "góra, lewo"),
        Labels = new() { [RoomKind.Junction] = "Skrzyżowanie.", [RoomKind.BigRoom] = "Duża sala.", [RoomKind.Corridor] = "Korytarz." },
    };
}
