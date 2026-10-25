using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the helltide list on the world map speaks (Shift+F2), per language; its entries and the cursor jump speak as the
/// waypoint list (WaypointStrings).</summary>
/// <param name="OpenedFormat">{0}: the helltide's area ("Auen von Fethis, Hawezar"), {1}: how many objects the list holds.</param>
/// <param name="NoneOnMap">No helltide object on the map: they show only while the player is in the helltide's area.</param>
/// <param name="ZoomInFormat">{0}: an object the map, zoomed this far out, shows as no icon the cursor can point at.</param>
public sealed record HelltideStrings(string OpenedFormat, string NoneOnMap, string ZoomInFormat)
{
    public static readonly HelltideStrings Spanish = new(
        "Marea infernal en {0}: {1} objetos por distancia.",
        "No hay objetos de la Marea infernal en el mapa. Solo aparecen cuando estás en la zona de la marea, fuera de las ciudades.",
        "Acerca más el mapa y elige {0} otra vez; tan lejos, el mapa no lo muestra.");

    public static readonly HelltideStrings English = new(
        "Helltide in {0}: {1} objects by distance.",
        "No helltide objects on the map. They appear only once you are in the helltide's area, outside towns.",
        "Zoom in further and choose {0} again; this far out the map doesn't show it.");

    public static readonly HelltideStrings German = new(
        "Höllenflut in {0}: {1} Objekte nach Entfernung.",
        "Keine Höllenflut-Objekte auf der Karte. Sie erscheinen erst, wenn du im Höllenflut-Gebiet bist, außerhalb der Städte.",
        "Zoome weiter hinein und wähle {0} erneut; so weit draußen zeigt die Karte es nicht.");

    public static readonly HelltideStrings French = new(
        "Marée infernale à {0} : {1} objets par distance.",
        "Aucun objet de la Marée infernale sur la carte. Ils n'apparaissent que lorsque vous êtes dans la zone de la marée, hors des villes.",
        "Zoomez davantage et choisissez {0} à nouveau ; si loin, la carte ne l'affiche pas.");

    public static readonly HelltideStrings Italian = new(
        "Marea infernale in {0}: {1} oggetti per distanza.",
        "Nessun oggetto della Marea infernale sulla mappa. Compaiono solo quando sei nella zona della marea, fuori dalle città.",
        "Aumenta lo zoom e scegli di nuovo {0}; così lontano la mappa non lo mostra.");

    public static readonly HelltideStrings Polish = new(
        "Piekielny przypływ w {0}: {1} obiektów według odległości.",
        "Brak obiektów piekielnego przypływu na mapie. Pojawiają się dopiero, gdy jesteś w jego strefie, poza miastami.",
        "Przybliż mapę i wybierz {0} ponownie; z takiej odległości mapa go nie pokazuje.");

    public static readonly HelltideStrings Portuguese = new(
        "Maré Infernal em {0}: {1} objetos por distância.",
        "Nenhum objeto da Maré Infernal no mapa. Eles só aparecem quando você está na área da maré, fora das cidades.",
        "Aproxime mais o mapa e escolha {0} de novo; tão afastado, o mapa não o mostra.");

    public static readonly HelltideStrings Russian = new(
        "Адский прилив: {0}. Объектов по расстоянию: {1}.",
        "На карте нет объектов Адского прилива. Они появляются, только когда вы в его зоне, за пределами городов.",
        "Приблизьте карту и выберите {0} снова: при таком отдалении карта его не показывает.");

    public static readonly HelltideStrings Turkish = new(
        "Cehennem Dalgası, {0}: mesafeye göre {1} nesne.",
        "Haritada Cehennem Dalgası nesnesi yok. Yalnızca dalga bölgesindeyken, şehirlerin dışında görünürler.",
        "Haritayı daha da yakınlaştırın ve {0} öğesini yeniden seçin; bu kadar uzaktan harita onu göstermiyor.");

    public static readonly HelltideStrings Japanese = new(
        "ヘルタイド（{0}）：距離順に{1}件。",
        "地図にヘルタイドのオブジェクトがありません。ヘルタイドの地域内、町の外にいるときだけ表示されます。",
        "地図をさらに拡大して、もう一度{0}を選んでください。ここまで縮小すると地図に表示されません。");

    public static readonly HelltideStrings Korean = new(
        "지옥물결 {0}: 거리순 {1}개.",
        "지도에 지옥물결 오브젝트가 없습니다. 지옥물결 지역의 마을 밖에 있을 때만 나타납니다.",
        "지도를 더 확대한 뒤 {0}을(를) 다시 선택하세요. 이렇게 축소하면 지도에 표시되지 않습니다.");

    public static readonly HelltideStrings ChineseSimplified = new(
        "地狱狂潮，{0}：按距离 {1} 个。",
        "地图上没有地狱狂潮物件。只有在地狱狂潮区域内、城镇之外时才会出现。",
        "请再放大地图，然后重新选择{0}；缩得这么远时地图不显示它。");

    public static readonly HelltideStrings ChineseTraditional = new(
        "地獄狂潮，{0}：依距離 {1} 個。",
        "地圖上沒有地獄狂潮物件。只有在地獄狂潮區域內、城鎮之外時才會出現。",
        "請再放大地圖，然後重新選擇{0}；縮得這麼遠時地圖不顯示它。");

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
