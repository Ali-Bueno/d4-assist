using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the treasure goblin list on the world map speaks (Shift+F3), per language; its entries and the cursor jump speak
/// as the waypoint list (WaypointStrings).</summary>
/// <param name="OpenedFormat">{0}: how many treasure goblins the map shows.</param>
/// <param name="NoneOnMap">The map shows no treasure goblin.</param>
public sealed record GoblinStrings(string OpenedFormat, string NoneOnMap)
{
    public static readonly GoblinStrings Spanish = new("Duendes del tesoro en el mapa: {0}.", "No hay duendes del tesoro en el mapa.");

    public static readonly GoblinStrings English = new("Treasure goblins on the map: {0}.", "No treasure goblin on the map.");

    public static readonly GoblinStrings German = new("Schatzgoblins auf der Karte: {0}.", "Kein Schatzgoblin auf der Karte.");

    public static readonly GoblinStrings French = new("Gobelins du trésor sur la carte : {0}.", "Aucun gobelin du trésor sur la carte.");

    public static readonly GoblinStrings Italian = new("Goblin del tesoro sulla mappa: {0}.", "Nessun goblin del tesoro sulla mappa.");

    public static readonly GoblinStrings Polish = new("Gobliny skarbów na mapie: {0}.", "Brak goblinów skarbów na mapie.");

    public static readonly GoblinStrings Portuguese = new("Goblins do tesouro no mapa: {0}.", "Nenhum goblin do tesouro no mapa.");

    public static readonly GoblinStrings Russian = new("Гоблинов-сокровищниц на карте: {0}.", "На карте нет гоблинов-сокровищниц.");

    public static readonly GoblinStrings Turkish = new("Haritadaki hazine goblinleri: {0}.", "Haritada hazine goblini yok.");

    public static readonly GoblinStrings Japanese = new("マップ上の宝物ゴブリン: {0}。", "マップに宝物ゴブリンはいません。");

    public static readonly GoblinStrings Korean = new("지도의 보물 고블린: {0}.", "지도에 보물 고블린이 없습니다.");

    public static readonly GoblinStrings ChineseSimplified = new("地图上的宝藏哥布林：{0}。", "地图上没有宝藏哥布林。");

    public static readonly GoblinStrings ChineseTraditional = new("地圖上的寶藏哥布林：{0}。", "地圖上沒有寶藏哥布林。");

    public static GoblinStrings For(SpeechLanguage language) => language switch
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
