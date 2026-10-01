namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>Portuguese (ptBR).</summary>
    public static readonly NavigationPhrases Portuguese = new()
    {
        SingularHeader = "Saída",
        PluralHeader = "Saídas",
        UnexploredFormat = "{0} inexplorada",
        GroupFormat = "{0} saídas {1}",
        CountWords = ["duas", "três", "quatro"],
        DeadEnd = "Beco sem saída.",
        Entrance = "Entrada.",
        NoOtherExits = "Sem outras saídas.",
        AllExplored = "Tudo explorado.",
        Rechecking = "Saída não encontrada. Revisando os becos sem saída.",
        ToQuestObjective = "Rumo ao objetivo da missão.",
        ToDungeonObjective = "Rumo a um objetivo da masmorra.",
        ToUnexplored = "Rumo a uma área inexplorada.",
        QuestObjectiveBelow = "O objetivo está mais abaixo.",
        QuestObjectiveAbove = "O objetivo está mais acima.",
        Directions = Map("cima", "cima à direita", "direita", "baixo à direita", "baixo", "baixo à esquerda", "esquerda", "cima à esquerda"),
        Labels = new() { [RoomKind.Junction] = "Cruzamento.", [RoomKind.BigRoom] = "Sala grande.", [RoomKind.Corridor] = "Corredor." },
    };
}
