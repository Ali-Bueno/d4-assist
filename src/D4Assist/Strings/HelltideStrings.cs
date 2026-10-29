using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the helltide list on the world map speaks (Shift+F2), per language: the active helltides first, then the objects
/// of the one chosen; the objects and the cursor jump speak as the waypoint list (WaypointStrings).</summary>
/// <param name="OpenedFormat">{0}: the helltide's area ("Auen von Fethis, Hawezar"), {1}: how many objects the list holds.</param>
/// <param name="NoneOnMap">No helltide object on the map: they show only while the player is in the helltide's area.</param>
/// <param name="ZoomInFormat">{0}: an object the map, zoomed this far out, shows as no icon the cursor can point at.</param>
/// <param name="RegionsOpened">Said before the first active helltide when the list opens.</param>
/// <param name="RegionEntryFormat">{0}: an active helltide's region, {1}: the minutes it has left.</param>
/// <param name="NoneActiveFormat">{0}: minutes until the next helltide rises.</param>
/// <param name="NotEnteredFormat">{0}: the region of a helltide whose objects the map doesn't show yet.</param>
/// <param name="PositionFormat">{0}: a helltide's position, {1}: how many are up; said only when there are two.</param>
public sealed record HelltideStrings(
    string OpenedFormat, string NoneOnMap, string ZoomInFormat, string RegionsOpened, string RegionEntryFormat, string NoneActiveFormat,
    string NotEnteredFormat, string PositionFormat)
{
    public static readonly HelltideStrings Spanish = new(
        "Marea infernal en {0}: {1} objetos por distancia.",
        "No hay objetos de la Marea infernal en el mapa. Solo aparecen cuando estás en la zona de la marea, fuera de las ciudades.",
        "Acerca más el mapa y elige {0} otra vez; tan lejos, el mapa no lo muestra.",
        "Marea infernal:", "{0}, quedan {1} minutos.",
        "Ahora no hay Marea infernal. La próxima empieza en {0} minutos.",
        "El mapa muestra los objetos de la Marea infernal en {0} solo cuando entras en su zona, fuera de las ciudades.",
        "{0} de {1}.");

    public static readonly HelltideStrings English = new(
        "Helltide in {0}: {1} objects by distance.",
        "No helltide objects on the map. They appear only once you are in the helltide's area, outside towns.",
        "Zoom in further and choose {0} again; this far out the map doesn't show it.",
        "Helltide:", "{0}, {1} minutes left.",
        "No helltide right now. The next one starts in {0} minutes.",
        "The map shows the objects of the helltide in {0} only once you enter its area, outside towns.",
        "{0} of {1}.");

    public static readonly HelltideStrings German = new(
        "Höllenflut in {0}: {1} Objekte nach Entfernung.",
        "Keine Höllenflut-Objekte auf der Karte. Sie erscheinen erst, wenn du im Höllenflut-Gebiet bist, außerhalb der Städte.",
        "Zoome weiter hinein und wähle {0} erneut; so weit draußen zeigt die Karte es nicht.",
        "Höllenflut:", "{0}, noch {1} Minuten.",
        "Gerade keine Höllenflut. Die nächste beginnt in {0} Minuten.",
        "Die Karte zeigt die Objekte der Höllenflut in {0} erst, wenn du ihr Gebiet betrittst, außerhalb der Städte.",
        "{0} von {1}.");

    public static readonly HelltideStrings French = new(
        "Marée infernale à {0} : {1} objets par distance.",
        "Aucun objet de la Marée infernale sur la carte. Ils n'apparaissent que lorsque vous êtes dans la zone de la marée, hors des villes.",
        "Zoomez davantage et choisissez {0} à nouveau ; si loin, la carte ne l'affiche pas.",
        "Marée infernale :", "{0}, encore {1} minutes.",
        "Pas de Marée infernale pour l'instant. La prochaine commence dans {0} minutes.",
        "La carte ne montre les objets de la Marée infernale à {0} que lorsque vous entrez dans sa zone, hors des villes.",
        "{0} sur {1}.");

    public static readonly HelltideStrings Italian = new(
        "Marea infernale in {0}: {1} oggetti per distanza.",
        "Nessun oggetto della Marea infernale sulla mappa. Compaiono solo quando sei nella zona della marea, fuori dalle città.",
        "Aumenta lo zoom e scegli di nuovo {0}; così lontano la mappa non lo mostra.",
        "Marea infernale:", "{0}, ancora {1} minuti.",
        "Nessuna Marea infernale ora. La prossima inizia tra {0} minuti.",
        "La mappa mostra gli oggetti della Marea infernale in {0} solo quando entri nella sua zona, fuori dalle città.",
        "{0} di {1}.");

    public static readonly HelltideStrings Polish = new(
        "Piekielny przypływ w {0}: {1} obiektów według odległości.",
        "Brak obiektów piekielnego przypływu na mapie. Pojawiają się dopiero, gdy jesteś w jego strefie, poza miastami.",
        "Przybliż mapę i wybierz {0} ponownie; z takiej odległości mapa go nie pokazuje.",
        "Piekielny przypływ:", "{0}, zostało {1} min.",
        "Teraz nie ma piekielnego przypływu. Następny zaczyna się za {0} min.",
        "Mapa pokazuje obiekty piekielnego przypływu w {0} dopiero, gdy wejdziesz do jego strefy, poza miastami.",
        "{0} z {1}.");

    public static readonly HelltideStrings Portuguese = new(
        "Maré Infernal em {0}: {1} objetos por distância.",
        "Nenhum objeto da Maré Infernal no mapa. Eles só aparecem quando você está na área da maré, fora das cidades.",
        "Aproxime mais o mapa e escolha {0} de novo; tão afastado, o mapa não o mostra.",
        "Maré Infernal:", "{0}, faltam {1} minutos.",
        "Nenhuma Maré Infernal agora. A próxima começa em {0} minutos.",
        "O mapa mostra os objetos da Maré Infernal em {0} só quando você entra na área dela, fora das cidades.",
        "{0} de {1}.");

    public static readonly HelltideStrings Russian = new(
        "Адский прилив: {0}. Объектов по расстоянию: {1}.",
        "На карте нет объектов Адского прилива. Они появляются, только когда вы в его зоне, за пределами городов.",
        "Приблизьте карту и выберите {0} снова: при таком отдалении карта его не показывает.",
        "Адский прилив:", "{0}, осталось {1} мин.",
        "Сейчас Адского прилива нет. Следующий начнётся через {0} мин.",
        "Карта показывает объекты Адского прилива ({0}), только когда вы входите в его зону, за пределами городов.",
        "{0} из {1}.");

    public static readonly HelltideStrings Turkish = new(
        "Cehennem Dalgası, {0}: mesafeye göre {1} nesne.",
        "Haritada Cehennem Dalgası nesnesi yok. Yalnızca dalga bölgesindeyken, şehirlerin dışında görünürler.",
        "Haritayı daha da yakınlaştırın ve {0} öğesini yeniden seçin; bu kadar uzaktan harita onu göstermiyor.",
        "Cehennem Dalgası:", "{0}, {1} dakika kaldı.",
        "Şu anda Cehennem Dalgası yok. Sonraki {0} dakika sonra başlıyor.",
        "Harita, {0} bölgesindeki Cehennem Dalgası nesnelerini ancak bölgesine girdiğinizde, şehirlerin dışında gösterir.",
        "{0} / {1}.");

    public static readonly HelltideStrings Japanese = new(
        "ヘルタイド（{0}）：距離順に{1}件。",
        "地図にヘルタイドのオブジェクトがありません。ヘルタイドの地域内、町の外にいるときだけ表示されます。",
        "地図をさらに拡大して、もう一度{0}を選んでください。ここまで縮小すると地図に表示されません。",
        "ヘルタイド：", "{0}、残り{1}分。",
        "現在ヘルタイドはありません。次は{0}分後に始まります。",
        "{0}のヘルタイドのオブジェクトは、その地域（町の外）に入ったときだけ地図に表示されます。",
        "{1}件中{0}件目。");

    public static readonly HelltideStrings Korean = new(
        "지옥물결 {0}: 거리순 {1}개.",
        "지도에 지옥물결 오브젝트가 없습니다. 지옥물결 지역의 마을 밖에 있을 때만 나타납니다.",
        "지도를 더 확대한 뒤 {0}을(를) 다시 선택하세요. 이렇게 축소하면 지도에 표시되지 않습니다.",
        "지옥물결:", "{0}, {1}분 남음.",
        "지금은 지옥물결이 없습니다. 다음은 {0}분 후에 시작합니다.",
        "{0}의 지옥물결 오브젝트는 그 지역(마을 밖)에 들어가야 지도에 표시됩니다.",
        "{1}개 중 {0}번째.");

    public static readonly HelltideStrings ChineseSimplified = new(
        "地狱狂潮，{0}：按距离 {1} 个。",
        "地图上没有地狱狂潮物件。只有在地狱狂潮区域内、城镇之外时才会出现。",
        "请再放大地图，然后重新选择{0}；缩得这么远时地图不显示它。",
        "地狱狂潮：", "{0}，还剩 {1} 分钟。",
        "现在没有地狱狂潮。下一次在 {0} 分钟后开始。",
        "只有进入{0}的地狱狂潮区域（城镇之外）后，地图才会显示其物件。",
        "第 {0} 个，共 {1} 个。");

    public static readonly HelltideStrings ChineseTraditional = new(
        "地獄狂潮，{0}：依距離 {1} 個。",
        "地圖上沒有地獄狂潮物件。只有在地獄狂潮區域內、城鎮之外時才會出現。",
        "請再放大地圖，然後重新選擇{0}；縮得這麼遠時地圖不顯示它。",
        "地獄狂潮：", "{0}，還剩 {1} 分鐘。",
        "現在沒有地獄狂潮。下一次在 {0} 分鐘後開始。",
        "只有進入{0}的地獄狂潮區域（城鎮之外）後，地圖才會顯示其物件。",
        "第 {0} 個，共 {1} 個。");

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
