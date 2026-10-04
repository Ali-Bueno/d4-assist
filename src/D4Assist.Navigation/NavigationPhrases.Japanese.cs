namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>Japanese (jaJP).</summary>
    public static readonly NavigationPhrases Japanese = new()
    {
        ExitsFormat = "{0}{1}：{2}。",
        NoExitsFormat = "{0}{1}",
        ListSeparator = "、",
        SingularHeader = "出口",
        PluralHeader = "出口",
        UnexploredFormat = "未探索の{0}",
        GroupFormat = "{1}に{0}",
        CountWords = ["二つ", "三つ", "四つ"],
        DeadEnd = "行き止まり。",
        Entrance = "入口。",
        NoOtherExits = "他に出口なし。",
        AllExplored = "すべて探索済み。",
        Rechecking = "出口が見つかりません。行き止まりを再確認します。",
        ToQuestObjective = "クエストの目的地へ。",
        ToDungeonObjective = "ダンジョンの目的地へ。",
        ToUnexplored = "未探索の場所へ。",
        QuestObjectiveBelow = "目的地はさらに下です。",
        QuestObjectiveAbove = "目的地はさらに上です。",
        Directions = Map("上", "右上", "右", "右下", "下", "左下", "左", "左上"),
        CompassDirections = Map("北", "北東", "東", "南東", "南", "南西", "西", "北西"),
        Labels = new() { [RoomKind.Junction] = "交差点。", [RoomKind.BigRoom] = "大部屋。", [RoomKind.Corridor] = "通路。" },
    };
}
