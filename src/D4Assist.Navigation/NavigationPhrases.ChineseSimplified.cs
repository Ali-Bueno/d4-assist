namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>ChineseSimplified (zhCN).</summary>
    public static readonly NavigationPhrases ChineseSimplified = new()
    {
        ExitsFormat = "{0}{1}：{2}。",
        NoExitsFormat = "{0}{1}",
        ListSeparator = "、",
        SingularHeader = "出口",
        PluralHeader = "出口",
        UnexploredFormat = "未探索的{0}",
        GroupFormat = "{1}有{0}",
        CountWords = ["两个", "三个", "四个"],
        DeadEnd = "死胡同。",
        Entrance = "入口。",
        NoOtherExits = "没有其他出口。",
        AllExplored = "全部探索完毕。",
        Rechecking = "未找到出口。正在重新检查死胡同。",
        ToQuestObjective = "前往任务目标。",
        ToDungeonObjective = "前往地下城目标。",
        ToUnexplored = "前往未探索区域。",
        QuestObjectiveBelow = "目标在更下方。",
        QuestObjectiveAbove = "目标在更上方。",
        Directions = Map("上", "右上", "右", "右下", "下", "左下", "左", "左上"),
        CompassDirections = Map("北", "东北", "东", "东南", "南", "西南", "西", "西北"),
        Labels = new() { [RoomKind.Junction] = "岔路口。", [RoomKind.BigRoom] = "大房间。", [RoomKind.Corridor] = "走廊。" },
    };
}
