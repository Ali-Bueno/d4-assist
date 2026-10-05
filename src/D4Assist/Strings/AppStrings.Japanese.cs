using D4Assist.Game;

namespace D4Assist.Strings;

public sealed partial record AppStrings
{
    /// <summary>Japanese (jaJP).</summary>
    public static readonly AppStrings Japanese = new(
        AssistantActive: "Diablo 4 Assist 起動",
        WaitingForGame: "Diablo IVを待っています",
        EnteringDungeon: "ダンジョンに入ります",
        LeavingDungeon: "ダンジョンを出ます",
        GuideOn: "ガイドオン。",
        GuideOff: "ガイドオフ。",
        EnteringWorldFormat: "{0}に入ります",
        SoundExamples: ["出口", "通れる床", "壁", "ドア", "未探索の出口", "クエストの音", "ガイドの合図音", "到着"],
        ShelterGuide: "避難所ガイド。",
        ShelterReachedFormat: "避難所{1}か所中{0}か所目。",
        GuideToPortal: "すべて探索済み。ポータルへ案内します。",
        GuideToUsedPortal: "すべて探索済み。来たポータルへ案内します。",
        AssistantClosed: "Diablo 4 Assist 終了",
        Restarting: "アシスタントを再起動します",
        SkillsStolen: "メフィストにスキルを奪われました。オーブを拾ってください。",
        SkillsRecovered: "スキルを取り戻しました。",
        ReadFailures: new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "キャラクターの位置を読み取れません。アシスタントは動作しません",
            [ReadCheck.Addresses] = "このバージョンのゲームのデータが見つかりません。部屋、レーダー、オブジェクトの音は使えません",
            [ReadCheck.Rooms] = "部屋を読み取れません。部屋の案内とレーダーは使えません",
            [ReadCheck.Actors] = "オブジェクトを読み取れません。ポータル、目的、キャラクターの音は使えません",
            [ReadCheck.Attributes] = "オブジェクトの状態を読み取れません。使用済みのオブジェクトの音が鳴り続けたり、クエストを追跡できなかったりします",
            [ReadCheck.Menus] = "ゲームのメニューが見つかりません。メニュー中も音が止まりません",
        },
        ReadWarningFormat: "警告：{0}。ログを送ってください。",
        Stopped: "エラーのためアシスタントが停止しました。ログを送ってください。",
        UpdateAvailableFormat: "Diablo 4 Assist {0} が利用できます。今すぐ更新しますか？",
        UpdateDownloading: "更新をダウンロードしています。",
        UpdateRestarting: "更新しました。再起動します。",
        UpdateFailed: "更新に失敗しました。バージョンは変わっていません。",
        UpdateFolderNotWritable: "更新に失敗しました。アプリのフォルダーに書き込めません。バージョンは変わっていません。",
        UpToDate: "最新バージョンです。",
        UpdateCheckFailed: "更新を確認できませんでした。",
        UpdateOnlyDownloaded: "更新はダウンロードしたバージョンでのみ使えます。",
        UpdateProgressFormat: "{0}%",
        UpdatedToFormat: "バージョン {0} に更新しました。");
}
