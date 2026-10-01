namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>Russian (ruRU).</summary>
    public static readonly NavigationPhrases Russian = new()
    {
        SingularHeader = "Выход",
        PluralHeader = "Выходы",
        UnexploredFormat = "неисследованный выход {0}",
        GroupFormat = "{0} выхода {1}",
        CountWords = ["два", "три", "четыре"],
        DeadEnd = "Тупик.",
        Entrance = "Вход.",
        NoOtherExits = "Других выходов нет.",
        AllExplored = "Всё исследовано.",
        Rechecking = "Выход не найден. Проверяю тупики.",
        ToQuestObjective = "К цели задания.",
        ToDungeonObjective = "К цели в подземелье.",
        ToUnexplored = "К неисследованной зоне.",
        QuestObjectiveBelow = "Цель находится ниже.",
        QuestObjectiveAbove = "Цель находится выше.",
        Directions = Map("вверх", "вверх вправо", "вправо", "вниз вправо", "вниз", "вниз влево", "влево", "вверх влево"),
        Labels = new() { [RoomKind.Junction] = "Перекрёсток.", [RoomKind.BigRoom] = "Большая комната.", [RoomKind.Corridor] = "Коридор." },
    };
}
