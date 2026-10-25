using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the helltide list on the world map speaks (Shift+F2), per language; its entries and the cursor jump speak as the
/// waypoint list (WaypointStrings).</summary>
/// <param name="OpenedFormat">{0}: the helltide's area ("Auen von Fethis, Hawezar"), {1}: how many objects the list holds.</param>
/// <param name="NoneOnMap">No helltide object on the map: they show only while the player is in the helltide's area.</param>
public sealed record HelltideStrings(string OpenedFormat, string NoneOnMap)
{
    public static readonly HelltideStrings Spanish = new(
        "Marea infernal en {0}: {1} objetos por distancia.",
        "No hay objetos de la Marea infernal en el mapa. Solo aparecen cuando estás en la zona de la marea, fuera de las ciudades.");

    public static readonly HelltideStrings English = new(
        "Helltide in {0}: {1} objects by distance.",
        "No helltide objects on the map. They appear only once you are in the helltide's area, outside towns.");

    public static readonly HelltideStrings German = new(
        "Höllenflut in {0}: {1} Objekte nach Entfernung.",
        "Keine Höllenflut-Objekte auf der Karte. Sie erscheinen erst, wenn du im Höllenflut-Gebiet bist, außerhalb der Städte.");

    public static readonly HelltideStrings French = new(
        "Marée infernale à {0} : {1} objets par distance.",
        "Aucun objet de la Marée infernale sur la carte. Ils n'apparaissent que lorsque vous êtes dans la zone de la marée, hors des villes.");

    public static readonly HelltideStrings Italian = new(
        "Marea infernale in {0}: {1} oggetti per distanza.",
        "Nessun oggetto della Marea infernale sulla mappa. Compaiono solo quando sei nella zona della marea, fuori dalle città.");

    public static readonly HelltideStrings Polish = new(
        "Piekielny przypływ w {0}: {1} obiektów według odległości.",
        "Brak obiektów piekielnego przypływu na mapie. Pojawiają się dopiero, gdy jesteś w jego strefie, poza miastami.");

    public static readonly HelltideStrings Portuguese = new(
        "Maré Infernal em {0}: {1} objetos por distância.",
        "Nenhum objeto da Maré Infernal no mapa. Eles só aparecem quando você está na área da maré, fora das cidades.");

    public static readonly HelltideStrings Russian = new(
        "Адский прилив: {0}. Объектов по расстоянию: {1}.",
        "На карте нет объектов Адского прилива. Они появляются, только когда вы в его зоне, за пределами городов.");

    public static readonly HelltideStrings Turkish = new(
        "Cehennem Dalgası, {0}: mesafeye göre {1} nesne.",
        "Haritada Cehennem Dalgası nesnesi yok. Yalnızca dalga bölgesindeyken, şehirlerin dışında görünürler.");

    public static readonly HelltideStrings Japanese = new(
        "ヘルタイド（{0}）：距離順に{1}件。",
        "地図にヘルタイドのオブジェクトがありません。ヘルタイドの地域内、町の外にいるときだけ表示されます。");

    public static readonly HelltideStrings Korean = new(
        "지옥물결 {0}: 거리순 {1}개.",
        "지도에 지옥물결 오브젝트가 없습니다. 지옥물결 지역의 마을 밖에 있을 때만 나타납니다.");

    public static readonly HelltideStrings ChineseSimplified = new(
        "地狱狂潮，{0}：按距离 {1} 个。",
        "地图上没有地狱狂潮物件。只有在地狱狂潮区域内、城镇之外时才会出现。");

    public static readonly HelltideStrings ChineseTraditional = new(
        "地獄狂潮，{0}：依距離 {1} 個。",
        "地圖上沒有地獄狂潮物件。只有在地獄狂潮區域內、城鎮之外時才會出現。");

    public static HelltideStrings For(SpeechLanguage language) => language switch
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
