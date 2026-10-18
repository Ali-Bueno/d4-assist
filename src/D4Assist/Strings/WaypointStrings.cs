using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the waypoint list on the world map speaks (Shift+F1), per language.</summary>
/// <param name="OpenedFormat">{0}: how many activated waypoints the list holds.</param>
/// <param name="EntryFormat">{0} name, {1} distance in meters, {2} direction, {3} position, {4} count.</param>
/// <param name="CursorFormat">{0}: the waypoint the mouse cursor now points at.</param>
/// <param name="OffMapFormat">{0}: a waypoint outside the map's view.</param>
public sealed record WaypointStrings(
    string OpenedFormat, string EntryFormat, string Empty, string Unreadable, string MapUnreadable, string CursorFormat,
    string OffMapFormat, string Closed)
{
    public static readonly WaypointStrings Spanish = new(
        "Puntos de ruta por distancia: {0}.", "{0}, {1} metros, {2}. {3} de {4}.",
        "No hay puntos de ruta activados en este mundo.", "No puedo leer los puntos de ruta activados.",
        "No puedo leer la vista del mapa.", "Cursor sobre {0}.",
        "{0} está fuera de la vista del mapa. Aleja el mapa y elígelo otra vez.", "Lista cerrada.");

    public static readonly WaypointStrings English = new(
        "Waypoints by distance: {0}.", "{0}, {1} meters, {2}. {3} of {4}.",
        "No activated waypoints in this world.", "Can't read the activated waypoints.",
        "Can't read the map view.", "Cursor on {0}.",
        "{0} is outside the map view. Zoom out and choose it again.", "List closed.");

    public static readonly WaypointStrings German = new(
        "Wegpunkte nach Entfernung: {0}.", "{0}, {1} Meter, {2}. {3} von {4}.",
        "Keine aktivierten Wegpunkte in dieser Welt.", "Die aktivierten Wegpunkte können nicht gelesen werden.",
        "Die Kartenansicht kann nicht gelesen werden.", "Cursor auf {0}.",
        "{0} liegt außerhalb der Kartenansicht. Zoome heraus und wähle ihn erneut.", "Liste geschlossen.");

    public static readonly WaypointStrings French = new(
        "Points de passage par distance : {0}.", "{0}, {1} mètres, {2}. {3} sur {4}.",
        "Aucun point de passage activé dans ce monde.", "Impossible de lire les points de passage activés.",
        "Impossible de lire la vue de la carte.", "Curseur sur {0}.",
        "{0} est hors de la vue de la carte. Dézoomez et choisissez-le à nouveau.", "Liste fermée.");

    public static readonly WaypointStrings Italian = new(
        "Punti di viaggio per distanza: {0}.", "{0}, {1} metri, {2}. {3} di {4}.",
        "Nessun punto di viaggio attivato in questo mondo.", "Impossibile leggere i punti di viaggio attivati.",
        "Impossibile leggere la vista della mappa.", "Cursore su {0}.",
        "{0} è fuori dalla vista della mappa. Riduci lo zoom e sceglilo di nuovo.", "Lista chiusa.");

    public static readonly WaypointStrings Polish = new(
        "Punkty podróży według odległości: {0}.", "{0}, {1} metrów, {2}. {3} z {4}.",
        "Brak aktywowanych punktów podróży w tym świecie.", "Nie można odczytać aktywowanych punktów podróży.",
        "Nie można odczytać widoku mapy.", "Kursor na {0}.",
        "{0} jest poza widokiem mapy. Oddal mapę i wybierz go ponownie.", "Lista zamknięta.");

    public static readonly WaypointStrings Portuguese = new(
        "Pontos de viagem por distância: {0}.", "{0}, {1} metros, {2}. {3} de {4}.",
        "Nenhum ponto de viagem ativado neste mundo.", "Não foi possível ler os pontos de viagem ativados.",
        "Não foi possível ler a visão do mapa.", "Cursor em {0}.",
        "{0} está fora da visão do mapa. Afaste o zoom e escolha-o de novo.", "Lista fechada.");

    public static readonly WaypointStrings Russian = new(
        "Путевые точки по расстоянию: {0}.", "{0}, {1} метров, {2}. {3} из {4}.",
        "В этом мире нет активированных путевых точек.", "Не удаётся прочитать активированные путевые точки.",
        "Не удаётся прочитать вид карты.", "Курсор на {0}.",
        "{0} вне видимой части карты. Отдалите карту и выберите снова.", "Список закрыт.");

    public static readonly WaypointStrings Turkish = new(
        "Uzaklığa göre geçiş noktaları: {0}.", "{0}, {1} metre, {2}. {3} / {4}.",
        "Bu dünyada etkin geçiş noktası yok.", "Etkin geçiş noktaları okunamıyor.",
        "Harita görünümü okunamıyor.", "İmleç {0} üzerinde.",
        "{0} harita görünümünün dışında. Uzaklaştırıp yeniden seçin.", "Liste kapatıldı.");

    public static readonly WaypointStrings Japanese = new(
        "距離順のウェイポイント：{0}。", "{0}、{1}メートル、{2}。{4}件中{3}件目。",
        "このワールドに有効化されたウェイポイントはありません。", "有効化されたウェイポイントを読み取れません。",
        "マップの表示を読み取れません。", "カーソルを{0}に合わせました。",
        "{0}はマップの表示範囲外です。ズームアウトしてもう一度選んでください。", "リストを閉じました。");

    public static readonly WaypointStrings Korean = new(
        "거리순 웨이포인트: {0}개.", "{0}, {1}미터, {2}. {4}개 중 {3}번째.",
        "이 세계에 활성화된 웨이포인트가 없습니다.", "활성화된 웨이포인트를 읽을 수 없습니다.",
        "지도 보기를 읽을 수 없습니다.", "커서가 {0}에 있습니다.",
        "{0}은(는) 지도 보기 밖에 있습니다. 축소한 뒤 다시 선택하세요.", "목록을 닫았습니다.");

    public static readonly WaypointStrings ChineseSimplified = new(
        "按距离排列的传送点：{0}个。", "{0}，{1}米，{2}。第{3}个，共{4}个。",
        "这个世界里没有已激活的传送点。", "无法读取已激活的传送点。",
        "无法读取地图视图。", "光标已移到{0}。",
        "{0}在地图视图之外。请缩小地图后再选一次。", "列表已关闭。");

    public static readonly WaypointStrings ChineseTraditional = new(
        "依距離排列的傳送點：{0}個。", "{0}，{1}公尺，{2}。第{3}個，共{4}個。",
        "這個世界裡沒有已啟用的傳送點。", "無法讀取已啟用的傳送點。",
        "無法讀取地圖檢視。", "游標已移到{0}。",
        "{0}在地圖檢視之外。請縮小地圖後再選一次。", "清單已關閉。");

    public static WaypointStrings For(SpeechLanguage language) => language switch
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
