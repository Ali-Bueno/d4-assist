using D4Assist.Game;
using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the guide speaks inside a legion event (LegionTargets, QuestPathBridge), per language.</summary>
/// <param name="TargetFormat">{0}: the target's kind, {1}: its distance in meters.</param>
/// <param name="Boss">The legion's boss.</param>
/// <param name="Structure">A structure to destroy (shrine, idol, spire…).</param>
/// <param name="Captain">A captain, the unique elite that guards a structure.</param>
/// <param name="Chest">A reward chest, once the legion is won.</param>
/// <param name="Reached">Said on reaching a target; the guide holds there until the map stops marking it.</param>
/// <param name="Done">The map marks no legion target any more.</param>
/// <param name="Attack">A defense legion's spot under attack (said with its side on the screen).</param>
/// <param name="Defend">A defense legion's building to keep standing.</param>
public sealed record LegionStrings(string TargetFormat, string Boss, string Structure, string Captain, string Chest, string Reached, string Done,
    string Attack, string Defend)
{
    public static readonly LegionStrings Spanish = new("Legión: {0}, {1} metros.", "Jefe", "Estructura", "Capitán", "Cofre de recompensa",
        "Objetivo de la legión alcanzado.", "No quedan objetivos de la legión en el mapa.",
        "Ataque", "Edificio a defender");

    public static readonly LegionStrings English = new("Legion: {0}, {1} meters.", "Boss", "Structure", "Captain", "Reward chest",
        "Legion target reached.", "No legion targets left on the map.",
        "Attack", "Building to defend");

    public static readonly LegionStrings German = new("Legion: {0}, {1} Meter.", "Boss", "Struktur", "Hauptmann", "Belohnungstruhe",
        "Legionsziel erreicht.", "Keine Legionsziele mehr auf der Karte.",
        "Angriff", "Gebäude verteidigen");

    public static readonly LegionStrings French = new("Légion : {0}, {1} mètres.", "Boss", "Structure", "Capitaine", "Coffre de récompense",
        "Objectif de la légion atteint.", "Plus aucun objectif de la légion sur la carte.",
        "Attaque", "Bâtiment à défendre");

    public static readonly LegionStrings Italian = new("Legione: {0}, {1} metri.", "Boss", "Struttura", "Capitano", "Forziere della ricompensa",
        "Obiettivo della legione raggiunto.", "Nessun obiettivo della legione rimasto sulla mappa.",
        "Attacco", "Edificio da difendere");

    public static readonly LegionStrings Polish = new("Legion: {0}, {1} metrów.", "Boss", "Struktura", "Kapitan", "Skrzynia z nagrodą",
        "Cel legionu osiągnięty.", "Na mapie nie ma już celów legionu.",
        "Atak", "Budynek do obrony");

    public static readonly LegionStrings Portuguese = new("Legião: {0}, {1} metros.", "Chefe", "Estrutura", "Capitão", "Baú de recompensa",
        "Objetivo da legião alcançado.", "Não há mais objetivos da legião no mapa.",
        "Ataque", "Construção a defender");

    public static readonly LegionStrings Russian = new("Легион: {0}, {1} м.", "Босс", "Строение", "Капитан", "Сундук с наградой",
        "Цель легиона достигнута.", "На карте больше нет целей легиона.",
        "Атака", "Здание для защиты");

    public static readonly LegionStrings Turkish = new("Lejyon: {0}, {1} metre.", "Boss", "Yapı", "Kaptan", "Ödül sandığı",
        "Lejyon hedefine ulaşıldı.", "Haritada lejyon hedefi kalmadı.",
        "Saldırı", "Savunulacak bina");

    public static readonly LegionStrings Japanese = new("レギオン：{0}、{1}メートル。", "ボス", "構造物", "隊長", "報酬の宝箱",
        "レギオンの目標に到着しました。", "マップにレギオンの目標はもうありません。",
        "襲撃", "守るべき建物");

    public static readonly LegionStrings Korean = new("군단: {0}, {1}미터.", "우두머리", "구조물", "대장", "보상 상자",
        "군단 목표에 도착했습니다.", "지도에 남은 군단 목표가 없습니다.",
        "공격", "방어할 건물");

    public static readonly LegionStrings ChineseSimplified = new("军团：{0}，{1}米。", "首领", "建筑", "队长", "奖励宝箱",
        "已到达军团目标。", "地图上已没有军团目标。",
        "进攻", "需保卫的建筑");

    public static readonly LegionStrings ChineseTraditional = new("軍團：{0}，{1}公尺。", "首領", "建築", "隊長", "獎勵寶箱",
        "已到達軍團目標。", "地圖上已沒有軍團目標。",
        "進攻", "需保衛的建築");

    /// <summary>The spoken name of a target's kind.</summary>
    public string Name(LegionTargetKind kind) => kind switch
    {
        LegionTargetKind.Boss => Boss,
        LegionTargetKind.Structure => Structure,
        LegionTargetKind.Captain => Captain,
        LegionTargetKind.Chest => Chest,
        LegionTargetKind.Attack => Attack,
        LegionTargetKind.Defend => Defend,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static LegionStrings For(SpeechLanguage language) => language switch
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
