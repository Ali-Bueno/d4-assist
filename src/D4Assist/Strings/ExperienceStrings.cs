using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What Ctrl+Alt+Shift+X speaks, per language (ExperienceAnnouncer).</summary>
/// <param name="ToNextLevel">{0}: the level, {1}: the experience left to the next one.</param>
/// <param name="ToNextParagonLevel">At the level cap. {0}: the level, {1}: the paragon level, {2}: the experience left to the next one.</param>
/// <param name="LevelOnly">Nothing left to earn. {0}: the level.</param>
/// <param name="Unreadable">The level can't be read (no game, or no character in a world).</param>
public sealed record ExperienceStrings(string ToNextLevel, string ToNextParagonLevel, string LevelOnly, string Unreadable)
{
    public static readonly ExperienceStrings Spanish = new(
        "Nivel {0}. Faltan {1} de experiencia para el siguiente nivel.",
        "Nivel {0}, leyenda {1}. Faltan {2} de experiencia para el siguiente nivel de leyenda.",
        "Nivel {0}.", "No se puede leer la experiencia.");
    public static readonly ExperienceStrings English = new(
        "Level {0}. {1} experience to the next level.",
        "Level {0}, paragon {1}. {2} experience to the next paragon level.",
        "Level {0}.", "The experience can't be read.");
    public static readonly ExperienceStrings German = new(
        "Stufe {0}. Noch {1} Erfahrung bis zur nächsten Stufe.",
        "Stufe {0}, Paragon {1}. Noch {2} Erfahrung bis zur nächsten Paragonstufe.",
        "Stufe {0}.", "Die Erfahrung ist nicht lesbar.");
    public static readonly ExperienceStrings French = new(
        "Niveau {0}. Encore {1} points d'expérience avant le niveau suivant.",
        "Niveau {0}, parangon {1}. Encore {2} points d'expérience avant le niveau de parangon suivant.",
        "Niveau {0}.", "Impossible de lire l'expérience.");
    public static readonly ExperienceStrings Italian = new(
        "Livello {0}. Mancano {1} punti esperienza al prossimo livello.",
        "Livello {0}, eccellenza {1}. Mancano {2} punti esperienza al prossimo livello d'eccellenza.",
        "Livello {0}.", "Impossibile leggere l'esperienza.");
    public static readonly ExperienceStrings Polish = new(
        "Poziom {0}. Do następnego poziomu brakuje {1} doświadczenia.",
        "Poziom {0}, mistrzowski {1}. Do następnego poziomu mistrzowskiego brakuje {2} doświadczenia.",
        "Poziom {0}.", "Nie można odczytać doświadczenia.");
    public static readonly ExperienceStrings Portuguese = new(
        "Nível {0}. Faltam {1} de experiência para o próximo nível.",
        "Nível {0}, excelência {1}. Faltam {2} de experiência para o próximo nível de excelência.",
        "Nível {0}.", "Não é possível ler a experiência.");
    public static readonly ExperienceStrings Russian = new(
        "Уровень {0}. До следующего уровня осталось {1} опыта.",
        "Уровень {0}, совершенствование {1}. До следующего уровня совершенствования осталось {2} опыта.",
        "Уровень {0}.", "Не удаётся прочитать опыт.");
    public static readonly ExperienceStrings Turkish = new(
        "Seviye {0}. Sonraki seviyeye {1} deneyim kaldı.",
        "Seviye {0}, paragon {1}. Sonraki paragon seviyesine {2} deneyim kaldı.",
        "Seviye {0}.", "Deneyim okunamıyor.");
    public static readonly ExperienceStrings Japanese = new(
        "レベル{0}。次のレベルまであと経験値{1}。",
        "レベル{0}、パラゴン{1}。次のパラゴンレベルまであと経験値{2}。",
        "レベル{0}。", "経験値を読み取れません。");
    public static readonly ExperienceStrings Korean = new(
        "레벨 {0}. 다음 레벨까지 경험치 {1} 남음.",
        "레벨 {0}, 정복자 {1}. 다음 정복자 레벨까지 경험치 {2} 남음.",
        "레벨 {0}.", "경험치를 읽을 수 없습니다.");
    public static readonly ExperienceStrings ChineseSimplified = new(
        "等级 {0}。距离下一级还需 {1} 经验。",
        "等级 {0}，巅峰 {1}。距离下一巅峰等级还需 {2} 经验。",
        "等级 {0}。", "无法读取经验值。");
    public static readonly ExperienceStrings ChineseTraditional = new(
        "等級 {0}。距離下一級還需 {1} 經驗。",
        "等級 {0}，巔峰 {1}。距離下一巔峰等級還需 {2} 經驗。",
        "等級 {0}。", "無法讀取經驗值。");

    public static ExperienceStrings For(SpeechLanguage language) => language switch
    {
        SpeechLanguage.English => English,
        SpeechLanguage.French => French,
        SpeechLanguage.German => German,
        SpeechLanguage.Italian => Italian,
        SpeechLanguage.Polish => Polish,
        SpeechLanguage.Portuguese => Portuguese,
        SpeechLanguage.Russian => Russian,
        SpeechLanguage.Turkish => Turkish,
        SpeechLanguage.Japanese => Japanese,
        SpeechLanguage.Korean => Korean,
        SpeechLanguage.ChineseSimplified => ChineseSimplified,
        SpeechLanguage.ChineseTraditional => ChineseTraditional,
        _ => Spanish,
    };
}
