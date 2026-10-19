using D4Assist.Game;

namespace D4Assist.Strings;

public sealed partial record AppStrings
{
    /// <summary>ChineseTraditional (zhTW).</summary>
    public static readonly AppStrings ChineseTraditional = new(
        AssistantActive: "Diablo 4 Assist 已啟動",
        WaitingForGame: "正在等待 Diablo IV",
        EnteringDungeon: "進入地城",
        LeavingDungeon: "離開地城",
        GuideOn: "導引已開啟。",
        GuideOff: "導引已關閉。",
        EnteringWorldFormat: "進入{0}",
        SoundExamples: ["出口", "可通行地面", "牆壁", "門", "未探索的出口", "任務提示音", "導引提示音", "抵達"],
        ShelterGuide: "避難所導引。",
        ShelterReachedFormat: "第{0}個避難所，共{1}個。",
        GuideToPortal: "全部探索完畢。正在帶你前往傳送門。",
        GuideToUsedPortal: "全部探索完畢。正在帶你回到來時的傳送門。",
        AssistantClosed: "Diablo 4 Assist 已關閉",
        Restarting: "正在重新啟動助手",
        SkillsStolen: "墨菲斯托奪走了你的技能。請撿起法球。",
        SkillsRecovered: "技能已找回。",
        ReadFailures: new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "無法讀取角色位置，助手無法運作",
            [ReadCheck.Addresses] = "找不到此遊戲版本的資料，沒有房間、雷達和物件音效",
            [ReadCheck.Rooms] = "無法讀取房間，沒有房間播報和雷達",
            [ReadCheck.Actors] = "無法讀取物件，沒有傳送門、目標和角色音效",
            [ReadCheck.Attributes] = "無法讀取物件狀態，已使用的物件可能仍會發聲，任務也無法追蹤",
            [ReadCheck.Menus] = "找不到遊戲選單，選單中音效不會暫停",
        },
        ReadWarningFormat: "警告：{0}。請傳送記錄檔。",
        Stopped: "助手因錯誤而停止。請傳送記錄檔。",
        UpdateAvailableFormat: "Diablo 4 Assist {0} 已推出。現在更新嗎？",
        UpdateDownloading: "正在下載更新。",
        UpdateRestarting: "已更新。正在重新啟動。",
        UpdateFailed: "更新失敗，目前版本維持不變。",
        UpdateFolderNotWritable: "更新失敗：無法寫入應用程式資料夾。目前版本維持不變。",
        UpToDate: "你已使用最新版本。",
        UpdateCheckFailed: "無法檢查更新。",
        UpdateOnlyDownloaded: "更新僅適用於下載的版本。",
        UpdateProgressFormat: "{0}%",
        UpdatedToFormat: "已更新至版本 {0}。",
        GuideDungeonsOnly: "導引僅在地城中有效。",
        ShelterRouteEnd: "避難所導引結束。",
        DebugMode: "偵錯模式已開啟。",
        GuideToBossRoom: "全部探索完畢。正在帶你前往首領房間。",
        TrailGuide: "花瓣路徑導引。",
        TrailEnd: "路徑終點。",
        PathGuide: "正在引導前往任務目標。",
        PathEnd: "已到達任務目標。",
        PathNotFound: "找不到路徑，指示方向。");
}
