namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>ChineseTraditional (zhTW).</summary>
    public static readonly NavigationPhrases ChineseTraditional = new()
    {
        ExitsFormat = "{0}{1}：{2}。",
        NoExitsFormat = "{0}{1}",
        ListSeparator = "、",
        SingularHeader = "出口",
        PluralHeader = "出口",
        UnexploredFormat = "未探索的{0}",
        GroupFormat = "{1}有{0}",
        CountWords = ["兩個", "三個", "四個"],
        DeadEnd = "死路。",
        Entrance = "入口。",
        NoOtherExits = "沒有其他出口。",
        AllExplored = "全部探索完畢。",
        Rechecking = "找不到出口。正在重新檢查死路。",
        ToQuestObjective = "前往任務目標。",
        ToDungeonObjective = "前往地城目標。",
        ToUnexplored = "前往未探索區域。",
        QuestObjectiveBelow = "目標在更下方。",
        QuestObjectiveAbove = "目標在更上方。",
        ShutForNow = "前方的路被封住了，稍後會打開。",
        Directions = Map("上", "右上", "右", "右下", "下", "左下", "左", "左上"),
        CompassDirections = Map("北", "東北", "東", "東南", "南", "西南", "西", "西北"),
        Labels = new() { [RoomKind.Junction] = "岔路口。", [RoomKind.BigRoom] = "大房間。", [RoomKind.Corridor] = "走廊。" },
    };
}
