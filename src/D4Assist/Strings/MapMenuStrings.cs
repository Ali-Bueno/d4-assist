using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the map menu on Shift+F1 speaks (WaypointListBridge, user request 2026-11-06), per language.</summary>
/// <param name="MenuOpened">Said before the first entry when the menu opens.</param>
/// <param name="Waypoints">The activated waypoints' list.</param>
/// <param name="Dungeons">The dungeon entrances' list.</param>
/// <param name="Cellars">The cellar entrances' list.</param>
/// <param name="Strongholds">The strongholds' list.</param>
/// <param name="Lairs">The boss lairs' list.</param>
/// <param name="ListOpenedFormat">{0}: a list's name, {1}: how many entries it holds.</param>
/// <param name="EmptyFormat">{0}: a list's name; said when this world holds none.</param>
public sealed record MapMenuStrings(
    string MenuOpened, string Waypoints, string Dungeons, string Cellars, string Strongholds, string Lairs, string ListOpenedFormat, string EmptyFormat)
{
    public static readonly MapMenuStrings Spanish = new("Menú del mapa.", "Puntos de ruta", "Mazmorras", "Sótanos", "Fortalezas", "Guaridas de jefes",
        "{0} por distancia: {1}.", "{0}: ninguno en este mundo.");

    public static readonly MapMenuStrings English = new("Map menu.", "Waypoints", "Dungeons", "Cellars", "Strongholds", "Boss lairs",
        "{0} by distance: {1}.", "{0}: none in this world.");

    public static readonly MapMenuStrings German = new("Kartenmenü.", "Wegpunkte", "Dungeons", "Keller", "Stützpunkte", "Boss-Unterschlüpfe",
        "{0} nach Entfernung: {1}.", "{0}: keine in dieser Welt.");

    public static readonly MapMenuStrings French = new("Menu de la carte.", "Points de passage", "Donjons", "Caves", "Bastions", "Repaires de boss",
        "{0} par distance : {1}.", "{0} : aucun dans ce monde.");

    public static readonly MapMenuStrings Italian = new("Menu della mappa.", "Punti di teletrasporto", "Spedizioni", "Cantine", "Roccaforti", "Tane dei boss",
        "{0} per distanza: {1}.", "{0}: nessuno in questo mondo.");

    public static readonly MapMenuStrings Polish = new("Menu mapy.", "Punkty podróży", "Lochy", "Piwnice", "Twierdze", "Leża bossów",
        "{0} według odległości: {1}.", "{0}: brak w tym świecie.");

    public static readonly MapMenuStrings Portuguese = new("Menu do mapa.", "Pontos de teleporte", "Masmorras", "Porões", "Fortalezas", "Covis de chefes",
        "{0} por distância: {1}.", "{0}: nenhum neste mundo.");

    public static readonly MapMenuStrings Russian = new("Меню карты.", "Порталы", "Подземелья", "Погреба", "Твердыни", "Логова боссов",
        "{0} по расстоянию: {1}.", "{0}: нет в этом мире.");

    public static readonly MapMenuStrings Turkish = new("Harita menüsü.", "Işınlanma noktaları", "Zindanlar", "Mahzenler", "Kaleler", "Boss inleri",
        "{0}, uzaklığa göre: {1}.", "{0}: bu dünyada yok.");

    public static readonly MapMenuStrings Japanese = new("マップメニュー。", "ウェイポイント", "ダンジョン", "地下室", "拠点", "ボスの巣",
        "{0}（距離順）：{1}。", "{0}：この世界にはありません。");

    public static readonly MapMenuStrings Korean = new("지도 메뉴.", "순간이동 지점", "던전", "지하실", "요새", "우두머리 소굴",
        "{0}, 거리순: {1}.", "{0}: 이 세계에 없습니다.");

    public static readonly MapMenuStrings ChineseSimplified = new("地图菜单。", "传送点", "地下城", "地窖", "据点", "首领巢穴",
        "{0}（按距离）：{1}。", "{0}：此世界中没有。");

    public static readonly MapMenuStrings ChineseTraditional = new("地圖選單。", "傳送點", "地城", "地窖", "據點", "首領巢穴",
        "{0}（依距離）：{1}。", "{0}：此世界中沒有。");

    /// <summary>The spoken name of a list of the menu.</summary>
    public string Name(D4Assist.Game.MapListKind kind) => kind switch
    {
        D4Assist.Game.MapListKind.Waypoints => Waypoints,
        D4Assist.Game.MapListKind.Dungeons => Dungeons,
        D4Assist.Game.MapListKind.Cellars => Cellars,
        D4Assist.Game.MapListKind.Strongholds => Strongholds,
        D4Assist.Game.MapListKind.Lairs => Lairs,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static MapMenuStrings For(SpeechLanguage language) => language switch
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
