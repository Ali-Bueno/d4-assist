using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the waypoint list on the world map speaks (Shift+F1), per language.</summary>
/// <param name="OpenedFormat">{0}: how many activated waypoints the list holds.</param>
/// <param name="EntryFormat">{0} name, {1} distance in meters, {2} direction, {3} position, {4} count.</param>
/// <param name="CursorFormat">{0}: the waypoint the mouse cursor now points at.</param>
/// <param name="OffMapFormat">{0}: a waypoint outside the map's view, {1}: its direction from the view's center.</param>
/// <param name="ZoomOutFormat">{0}: a waypoint the map shows once zoomed out further.</param>
/// <param name="HopFormat">{0}: a waypoint too far for the map's view, {1}: the waypoint in view the cursor now points at on the way.</param>
public sealed record WaypointStrings(
    string OpenedFormat, string EntryFormat, string Empty, string Unreadable, string MapUnreadable, string CursorFormat,
    string OffMapFormat, string Closed, string HopFormat, string ZoomOutFormat)
{
    public static readonly WaypointStrings Spanish = new(
        "Puntos de ruta por distancia: {0}.", "{0}, {1} metros, {2}. {3} de {4}.",
        "No hay puntos de ruta activados en este mundo.", "No puedo leer los puntos de ruta activados.",
        "No puedo leer la vista del mapa.", "Cursor sobre {0}.",
        "{0} está fuera de la vista del mapa, {1}. Desplaza el mapa hacia allí y elígelo otra vez.", "Lista cerrada.",
        "{0} está demasiado lejos para el mapa. Parada intermedia: cursor sobre {1}. Después del viaje, elige {0} otra vez.",
        "{0} está fuera de la vista del mapa. Aleja el mapa y elígelo otra vez.");

    public static readonly WaypointStrings English = new(
        "Waypoints by distance: {0}.", "{0}, {1} meters, {2}. {3} of {4}.",
        "No activated waypoints in this world.", "Can't read the activated waypoints.",
        "Can't read the map view.", "Cursor on {0}.",
        "{0} is outside the map view, {1}. Move the map that way and choose it again.", "List closed.",
        "{0} is too far for the map. Stopover: cursor on {1}. After the trip, choose {0} again.",
        "{0} is outside the map view. Zoom out and choose it again.");

    public static readonly WaypointStrings German = new(
        "Wegpunkte nach Entfernung: {0}.", "{0}, {1} Meter, {2}. {3} von {4}.",
        "Keine aktivierten Wegpunkte in dieser Welt.", "Die aktivierten Wegpunkte können nicht gelesen werden.",
        "Die Kartenansicht kann nicht gelesen werden.", "Cursor auf {0}.",
        "{0} liegt außerhalb der Kartenansicht, {1}. Schiebe die Karte dorthin und wähle ihn erneut.", "Liste geschlossen.",
        "{0} ist zu weit weg für die Karte. Zwischenstopp: Cursor auf {1}. Nach der Reise wähle {0} erneut.",
        "{0} liegt außerhalb der Kartenansicht. Zoome heraus und wähle ihn erneut.");

    public static readonly WaypointStrings French = new(
        "Points de passage par distance : {0}.", "{0}, {1} mètres, {2}. {3} sur {4}.",
        "Aucun point de passage activé dans ce monde.", "Impossible de lire les points de passage activés.",
        "Impossible de lire la vue de la carte.", "Curseur sur {0}.",
        "{0} est hors de la vue de la carte, {1}. Déplacez la carte dans cette direction et choisissez-le à nouveau.", "Liste fermée.",
        "{0} est trop loin pour la carte. Étape : curseur sur {1}. Après le voyage, choisissez {0} à nouveau.",
        "{0} est hors de la vue de la carte. Dézoomez et choisissez-le à nouveau.");

    public static readonly WaypointStrings Italian = new(
        "Punti di viaggio per distanza: {0}.", "{0}, {1} metri, {2}. {3} di {4}.",
        "Nessun punto di viaggio attivato in questo mondo.", "Impossibile leggere i punti di viaggio attivati.",
        "Impossibile leggere la vista della mappa.", "Cursore su {0}.",
        "{0} è fuori dalla vista della mappa, {1}. Sposta la mappa in quella direzione e sceglilo di nuovo.", "Lista chiusa.",
        "{0} è troppo lontano per la mappa. Tappa intermedia: cursore su {1}. Dopo il viaggio, scegli di nuovo {0}.",
        "{0} è fuori dalla vista della mappa. Riduci lo zoom e sceglilo di nuovo.");

    public static readonly WaypointStrings Polish = new(
        "Punkty podróży według odległości: {0}.", "{0}, {1} metrów, {2}. {3} z {4}.",
        "Brak aktywowanych punktów podróży w tym świecie.", "Nie można odczytać aktywowanych punktów podróży.",
        "Nie można odczytać widoku mapy.", "Kursor na {0}.",
        "{0} jest poza widokiem mapy, {1}. Przesuń mapę w tę stronę i wybierz go ponownie.", "Lista zamknięta.",
        "{0} jest za daleko dla mapy. Przystanek: kursor na {1}. Po podróży wybierz {0} ponownie.",
        "{0} jest poza widokiem mapy. Oddal mapę i wybierz go ponownie.");

    public static readonly WaypointStrings Portuguese = new(
        "Pontos de viagem por distância: {0}.", "{0}, {1} metros, {2}. {3} de {4}.",
        "Nenhum ponto de viagem ativado neste mundo.", "Não foi possível ler os pontos de viagem ativados.",
        "Não foi possível ler a visão do mapa.", "Cursor em {0}.",
        "{0} está fora da visão do mapa, {1}. Mova o mapa nessa direção e escolha-o de novo.", "Lista fechada.",
        "{0} está longe demais para o mapa. Parada: cursor em {1}. Depois da viagem, escolha {0} de novo.",
        "{0} está fora da visão do mapa. Afaste o zoom e escolha-o de novo.");

    public static readonly WaypointStrings Russian = new(
        "Путевые точки по расстоянию: {0}.", "{0}, {1} метров, {2}. {3} из {4}.",
        "В этом мире нет активированных путевых точек.", "Не удаётся прочитать активированные путевые точки.",
        "Не удаётся прочитать вид карты.", "Курсор на {0}.",
        "{0} вне видимой части карты, {1}. Сдвиньте карту туда и выберите снова.", "Список закрыт.",
        "{0} слишком далеко для карты. Промежуточная точка: курсор на {1}. После перемещения выберите {0} снова.",
        "{0} вне видимой части карты. Отдалите карту и выберите снова.");

    public static readonly WaypointStrings Turkish = new(
        "Uzaklığa göre geçiş noktaları: {0}.", "{0}, {1} metre, {2}. {3} / {4}.",
        "Bu dünyada etkin geçiş noktası yok.", "Etkin geçiş noktaları okunamıyor.",
        "Harita görünümü okunamıyor.", "İmleç {0} üzerinde.",
        "{0} harita görünümünün dışında, {1}. Haritayı o yöne kaydırıp yeniden seçin.", "Liste kapatıldı.",
        "{0} harita için çok uzak. Ara durak: imleç {1} üzerinde. Yolculuktan sonra {0} noktasını yeniden seçin.",
        "{0} harita görünümünün dışında. Uzaklaştırıp yeniden seçin.");

    public static readonly WaypointStrings Japanese = new(
        "距離順のウェイポイント：{0}。", "{0}、{1}メートル、{2}。{4}件中{3}件目。",
        "このワールドに有効化されたウェイポイントはありません。", "有効化されたウェイポイントを読み取れません。",
        "マップの表示を読み取れません。", "カーソルを{0}に合わせました。",
        "{0}はマップの表示範囲外です（{1}）。マップをその方向へ動かして、もう一度選んでください。", "リストを閉じました。",
        "{0}はマップには遠すぎます。経由地：カーソルを{1}に合わせました。移動したら、もう一度{0}を選んでください。",
        "{0}はマップの表示範囲外です。ズームアウトしてもう一度選んでください。");

    public static readonly WaypointStrings Korean = new(
        "거리순 웨이포인트: {0}개.", "{0}, {1}미터, {2}. {4}개 중 {3}번째.",
        "이 세계에 활성화된 웨이포인트가 없습니다.", "활성화된 웨이포인트를 읽을 수 없습니다.",
        "지도 보기를 읽을 수 없습니다.", "커서가 {0}에 있습니다.",
        "{0}은(는) 지도 보기 밖에 있습니다, {1}. 지도를 그쪽으로 옮긴 뒤 다시 선택하세요.", "목록을 닫았습니다.",
        "{0}은(는) 지도에서 너무 멉니다. 경유지: 커서가 {1}에 있습니다. 이동한 뒤 {0}을(를) 다시 선택하세요.",
        "{0}은(는) 지도 보기 밖에 있습니다. 축소한 뒤 다시 선택하세요.");

    public static readonly WaypointStrings ChineseSimplified = new(
        "按距离排列的传送点：{0}个。", "{0}，{1}米，{2}。第{3}个，共{4}个。",
        "这个世界里没有已激活的传送点。", "无法读取已激活的传送点。",
        "无法读取地图视图。", "光标已移到{0}。",
        "{0}在地图视图之外，{1}。请把地图移过去后再选一次。", "列表已关闭。",
        "{0}离得太远，地图上无法显示。中转：光标已移到{1}。传送后请再选一次{0}。",
        "{0}在地图视图之外。请缩小地图后再选一次。");

    public static readonly WaypointStrings ChineseTraditional = new(
        "依距離排列的傳送點：{0}個。", "{0}，{1}公尺，{2}。第{3}個，共{4}個。",
        "這個世界裡沒有已啟用的傳送點。", "無法讀取已啟用的傳送點。",
        "無法讀取地圖檢視。", "游標已移到{0}。",
        "{0}在地圖檢視之外，{1}。請把地圖移過去後再選一次。", "清單已關閉。",
        "{0}離得太遠，地圖上無法顯示。中轉：游標已移到{1}。傳送後請再選一次{0}。",
        "{0}在地圖檢視之外。請縮小地圖後再選一次。");

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
