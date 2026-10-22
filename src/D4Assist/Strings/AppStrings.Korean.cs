using D4Assist.Game;

namespace D4Assist.Strings;

public sealed partial record AppStrings
{
    /// <summary>Korean (koKR).</summary>
    public static readonly AppStrings Korean = new(
        AssistantActive: "Diablo 4 Assist 실행 중",
        WaitingForGame: "Diablo IV를 기다리는 중",
        EnteringDungeon: "던전에 들어갑니다",
        LeavingDungeon: "던전을 나갑니다",
        GuideOn: "안내 켜짐.",
        GuideOff: "안내 꺼짐.",
        EnteringWorldFormat: "{0}에 들어갑니다",
        SoundExamples: ["출구", "열린 바닥", "벽", "문", "탐색하지 않은 출구", "퀘스트 소리", "안내 신호음", "도착"],
        ShelterGuide: "은신처 안내.",
        ShelterReachedFormat: "은신처 {1}곳 중 {0}번째.",
        GuideToPortal: "모두 탐색했습니다. 포털로 안내합니다.",
        GuideToUsedPortal: "모두 탐색했습니다. 타고 온 포털로 안내합니다.",
        AssistantClosed: "Diablo 4 Assist 종료",
        Restarting: "도우미를 다시 시작합니다",
        SkillsStolen: "메피스토가 스킬을 빼앗았습니다. 구슬을 주우세요.",
        SkillsRecovered: "스킬을 되찾았습니다.",
        ReadFailures: new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "캐릭터 위치를 읽을 수 없어 도우미가 작동하지 않습니다",
            [ReadCheck.Addresses] = "이 게임 버전의 데이터를 찾을 수 없어 방, 레이더, 사물 소리를 쓸 수 없습니다",
            [ReadCheck.Rooms] = "방을 읽을 수 없어 방 안내와 레이더를 쓸 수 없습니다",
            [ReadCheck.Actors] = "사물을 읽을 수 없어 포털, 목표, 캐릭터 소리를 쓸 수 없습니다",
            [ReadCheck.Attributes] = "사물의 상태를 읽을 수 없어 이미 쓴 사물의 소리가 계속 날 수 있고 퀘스트를 추적하지 못합니다",
            [ReadCheck.Menus] = "게임 메뉴를 찾을 수 없어 메뉴에서 소리가 멈추지 않습니다",
        },
        ReadWarningFormat: "경고: {0}. 로그를 보내 주세요.",
        Stopped: "오류로 도우미가 멈췄습니다. 로그를 보내 주세요.",
        UpdateAvailableFormat: "Diablo 4 Assist {0}을(를) 사용할 수 있습니다. 지금 업데이트할까요?",
        UpdateDownloading: "업데이트를 내려받는 중입니다.",
        UpdateRestarting: "업데이트했습니다. 다시 시작합니다.",
        UpdateFailed: "업데이트에 실패했습니다. 현재 버전은 그대로입니다.",
        UpdateFolderNotWritable: "업데이트에 실패했습니다. 앱 폴더에 쓸 수 없습니다. 현재 버전은 그대로입니다.",
        UpToDate: "최신 버전입니다.",
        UpdateCheckFailed: "업데이트를 확인하지 못했습니다.",
        UpdateOnlyDownloaded: "업데이트는 내려받은 버전에서만 작동합니다.",
        UpdateProgressFormat: "{0}%",
        UpdatedToFormat: "버전 {0}(으)로 업데이트했습니다.",
        GuideDungeonsOnly: "안내는 던전에서만 작동합니다.",
        ShelterRouteEnd: "은신처 안내 종료.",
        DebugMode: "디버그 모드 켜짐.",
        GuideToBossRoom: "모두 탐색했습니다. 보스 방으로 안내합니다.",
        TrailGuide: "꽃잎 길 안내.",
        TrailEnd: "길의 끝.",
        PathGuideFormat: "퀘스트 목표: {0}, {1}미터.",
        PathEnd: "퀘스트 목표에 도착했습니다.",
        PathNotFound: "길을 찾지 못했습니다. 방향을 알려 드립니다.",
        NoQuestPlace: "지도에 퀘스트 목표가 없습니다.",
        PentagramGuide: "오각별 안내.",
        PentagramEnd: "오각별 끝.",
        QuestMarker: "퀘스트 표시",
        PlaceCountFormat: "{1}개 중 {0}번째.",
        GuideExploreMode: "안내: 탐험.",
        GuideObjectivesMode: "안내: 목표.",
        GuideToMonstersFormat: "모두 탐색했습니다. 지도에 몬스터 {0}마리가 남았습니다. 안내합니다.");
}
