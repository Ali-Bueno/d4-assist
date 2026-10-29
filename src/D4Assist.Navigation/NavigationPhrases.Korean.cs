namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>Korean (koKR).</summary>
    public static readonly NavigationPhrases Korean = new()
    {
        SingularHeader = "출구",
        PluralHeader = "출구",
        UnexploredFormat = "탐색하지 않은 {0}",
        GroupFormat = "{1} {0}",
        CountWords = ["두 개", "세 개", "네 개"],
        DeadEnd = "막다른 길.",
        Entrance = "입구.",
        NoOtherExits = "다른 출구 없음.",
        AllExplored = "모두 탐색했습니다.",
        Rechecking = "출구를 찾지 못했습니다. 막다른 길을 다시 확인합니다.",
        ToQuestObjective = "퀘스트 목표로.",
        ToDungeonObjective = "던전 목표로.",
        ToUnexplored = "탐색하지 않은 곳으로.",
        QuestObjectiveBelow = "목표가 더 아래에 있습니다.",
        QuestObjectiveAbove = "목표가 더 위에 있습니다.",
        ShutForNow = "길이 지금은 막혀 있습니다. 나중에 열립니다.",
        Directions = Map("위", "오른쪽 위", "오른쪽", "오른쪽 아래", "아래", "왼쪽 아래", "왼쪽", "왼쪽 위"),
        CompassDirections = Map("북쪽", "북동쪽", "동쪽", "남동쪽", "남쪽", "남서쪽", "서쪽", "북서쪽"),
        Labels = new() { [RoomKind.Junction] = "갈림길.", [RoomKind.BigRoom] = "큰 방.", [RoomKind.Corridor] = "복도." },
    };
}
