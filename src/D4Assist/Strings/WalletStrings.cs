using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What F2 speaks, per language: the helltide's cinders and Baneful Hearts, each as the game names it followed by the
/// amount ("Glut: 36, Unheilsherz: 1").</summary>
/// <param name="EntryFormat">{0}: a currency's name in the game's language, {1}: its amount or <paramref name="None"/>.</param>
/// <param name="None">Said for an amount of 0 ("Glut: keine").</param>
/// <param name="Separator">Between the two entries.</param>
/// <param name="Unreadable">The amounts can't be read (no game, or the player block not found).</param>
/// <param name="CindersFallback">The cinders' name when the game's text has none.</param>
/// <param name="HeartsFallback">The Baneful Hearts' name when the game's text has none.</param>
public sealed record WalletStrings(string EntryFormat, string None, string Separator, string Unreadable, string CindersFallback, string HeartsFallback)
{
    public static readonly WalletStrings Spanish = new("{0}: {1}", "ninguno", ", ", "No se pueden leer las ascuas ni los corazones.", "Ascuas", "Corazón funesto");
    public static readonly WalletStrings English = new("{0}: {1}", "none", ", ", "Cinders and hearts can't be read.", "Aberrant Cinders", "Baneful Heart");
    public static readonly WalletStrings German = new("{0}: {1}", "keine", ", ", "Glut und Herzen sind nicht lesbar.", "Glut", "Unheilsherz");
    public static readonly WalletStrings French = new("{0} : {1}", "aucun", ", ", "Impossible de lire les braises et les cœurs.", "Braises", "Cœur funeste");
    public static readonly WalletStrings Italian = new("{0}: {1}", "nessuno", ", ", "Impossibile leggere le braci e i cuori.", "Braci", "Cuore funesto");
    public static readonly WalletStrings Polish = new("{0}: {1}", "brak", ", ", "Nie można odczytać żaru ani serc.", "Żar", "Zgubne serce");
    public static readonly WalletStrings Portuguese = new("{0}: {1}", "nenhum", ", ", "Não é possível ler as cinzas nem os corações.", "Cinzas", "Coração funesto");
    public static readonly WalletStrings Russian = new("{0}: {1}", "нет", ", ", "Не удаётся прочитать угли и сердца.", "Угли", "Пагубное сердце");
    public static readonly WalletStrings Turkish = new("{0}: {1}", "yok", ", ", "Közler ve kalpler okunamıyor.", "Közler", "Uğursuz Kalp");
    public static readonly WalletStrings Japanese = new("{0}：{1}", "なし", "、", "残り火と心臓を読み取れません。", "残り火", "災いの心臓");
    public static readonly WalletStrings Korean = new("{0}: {1}", "없음", ", ", "잔불과 심장을 읽을 수 없습니다.", "잔불", "흉악한 심장");
    public static readonly WalletStrings ChineseSimplified = new("{0}：{1}", "无", "，", "无法读取余烬和心脏。", "余烬", "灾厄之心");
    public static readonly WalletStrings ChineseTraditional = new("{0}：{1}", "無", "，", "無法讀取餘燼和心臟。", "餘燼", "災厄之心");

    public static WalletStrings For(SpeechLanguage language) => language switch
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
