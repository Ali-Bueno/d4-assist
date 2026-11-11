using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the overworld guide (QuestPathBridge) speaks about its path, per language.</summary>
/// <param name="Blocked">The player stood stuck where the path leads on: that way counts as blocked and another is found.</param>
public sealed record PathStrings(string Blocked)
{
    public static readonly PathStrings Spanish = new("Por aquí no se pasa; busco otro camino.");
    public static readonly PathStrings English = new("No way through here; finding another path.");
    public static readonly PathStrings German = new("Hier geht es nicht weiter; ich suche einen anderen Weg.");
    public static readonly PathStrings French = new("Pas de passage ici ; je cherche un autre chemin.");
    public static readonly PathStrings Italian = new("Di qui non si passa; cerco un altro percorso.");
    public static readonly PathStrings Polish = new("Tędy nie przejdziesz; szukam innej drogi.");
    public static readonly PathStrings Portuguese = new("Não há passagem aqui; procurando outro caminho.");
    public static readonly PathStrings Russian = new("Здесь не пройти; ищу другой путь.");
    public static readonly PathStrings Turkish = new("Buradan geçilmiyor; başka bir yol arıyorum.");
    public static readonly PathStrings Japanese = new("ここは通れません。別の道を探します。");
    public static readonly PathStrings Korean = new("여기는 지나갈 수 없습니다. 다른 길을 찾습니다.");
    public static readonly PathStrings ChineseSimplified = new("此处无法通过，正在寻找其他路径。");
    public static readonly PathStrings ChineseTraditional = new("此處無法通過，正在尋找其他路徑。");

    public static PathStrings For(SpeechLanguage language) => language switch
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
