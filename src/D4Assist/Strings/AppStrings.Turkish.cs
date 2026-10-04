using D4Assist.Game;

namespace D4Assist.Strings;

public sealed partial record AppStrings
{
    /// <summary>Turkish (trTR).</summary>
    public static readonly AppStrings Turkish = new(
        AssistantActive: "Diablo 4 Assist etkin",
        WaitingForGame: "Diablo IV bekleniyor",
        EnteringDungeon: "Zindana giriliyor",
        LeavingDungeon: "Zindandan çıkılıyor",
        GuideOn: "Rehber açık.",
        GuideOff: "Rehber kapalı.",
        EnteringWorldFormat: "{0} alanına giriliyor",
        SoundExamples: ["Çıkış", "Açık zemin", "Duvar", "Kapı", "Keşfedilmemiş çıkış", "Görev sesi", "Rehber sinyali", "Varış"],
        ShelterGuide: "Sığınak rehberi.",
        ShelterReachedFormat: "Sığınak {0} / {1}.",
        GuideToPortal: "Her yer keşfedildi. Seni bir portala götürüyorum.",
        GuideToUsedPortal: "Her yer keşfedildi. Seni geldiğin portala geri götürüyorum.",
        AssistantClosed: "Diablo 4 Assist kapatıldı",
        Restarting: "Yardımcı yeniden başlatılıyor",
        SkillsStolen: "Mephisto yeteneklerini aldı. Küreleri topla.",
        SkillsRecovered: "Yetenekler geri alındı.",
        ReadFailures: new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "karakterin konumu okunamıyor, yardımcı çalışamaz",
            [ReadCheck.Addresses] = "bu oyun sürümünün verileri bulunamıyor, oda, radar ve nesne sesleri yok",
            [ReadCheck.Rooms] = "odalar okunamıyor, oda duyuruları ve radar yok",
            [ReadCheck.Actors] = "nesneler okunamıyor, portal, hedef ve karakter sesleri yok",
            [ReadCheck.Attributes] = "nesnelerin durumu okunamıyor, kullanılmış nesneler ses çıkarmaya devam edebilir ve görevler izlenmez",
            [ReadCheck.Menus] = "oyun menüleri bulunamıyor, sesler menülerde duraklatılmayacak",
        },
        ReadWarningFormat: "Uyarı: {0}. Diablo IV güncellenmiş olabilir. Lütfen günlüğü gönder.",
        Stopped: "Yardımcı bir hata nedeniyle durdu. Lütfen günlüğü gönder.",
        UpdateAvailableFormat: "Diablo 4 Assist {0} kullanılabilir. Şimdi güncellensin mi?",
        UpdateDownloading: "Güncelleme indiriliyor.",
        UpdateRestarting: "Güncellendi. Yeniden başlatılıyor.",
        UpdateFailed: "Güncelleme başarısız oldu; sürümünüz değişmedi.",
        UpdateFolderNotWritable: "Güncelleme başarısız oldu: uygulama klasörüne yazılamıyor. Sürümünüz değişmedi.",
        UpToDate: "En son sürümü kullanıyorsunuz.",
        UpdateCheckFailed: "Güncellemeler denetlenemedi.",
        UpdateOnlyDownloaded: "Güncellemeler yalnızca indirilen sürümde çalışır.");
}
