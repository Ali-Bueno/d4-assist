using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the events menu on the world map speaks (Shift+F2), per language: its entries, the whispers' list and the events' list (legions and
/// world bosses). The helltide's list speaks as HelltideStrings, an entry's distance and the cursor jump as the waypoint list (WaypointStrings).</summary>
/// <param name="MenuOpened">Said before the first entry when the menu opens.</param>
/// <param name="Helltide">The menu's helltide entry.</param>
/// <param name="Whispers">The menu's whispers entry.</param>
/// <param name="Events">The menu's entry for legions and world bosses.</param>
/// <param name="WhispersOpenedFormat">{0}: how many whispers the list holds.</param>
/// <param name="EventsOpenedFormat">{0}: how many events the list holds.</param>
/// <param name="NoWhispers">None is up.</param>
/// <param name="NoEvents">None is listed: they show only from shortly before they start.</param>
/// <param name="Unreadable">The world states that say what is up can't be read.</param>
/// <param name="OneFavor">An entry paying one grim favor.</param>
/// <param name="FavorsFormat">{0}: how many grim favors an entry pays, more than one.</param>
/// <param name="MinutesLeftFormat">{0}: the minutes an entry has left, more than one.</param>
/// <param name="OneMinuteLeft">An entry in its last minute.</param>
/// <param name="StartsInFormat">{0}: the minutes until an event starts, more than one.</param>
/// <param name="StartsInOneMinute">An event starting within the minute.</param>
/// <param name="EventsReading">No event listed yet while the map's first read since it opened still runs.</param>
/// <param name="KeyTakenFormat">An opening key another program holds: {0} that key, {1} the one to use instead.</param>
/// <param name="KeysTakenFormat">Both held: {0} the key, {1} the one tried instead; that list can't be opened.</param>
public sealed record EventStrings(
    string MenuOpened, string Helltide, string Whispers, string Events, string WhispersOpenedFormat, string EventsOpenedFormat,
    string NoWhispers, string NoEvents, string Unreadable, string OneFavor, string FavorsFormat, string MinutesLeftFormat, string OneMinuteLeft,
    string StartsInFormat, string StartsInOneMinute, string EventsReading, string KeyTakenFormat, string KeysTakenFormat)
{
    public static readonly EventStrings Spanish = new(
        "Menú de eventos:", "Marea infernal", "Susurros", "Eventos", "{0} susurros por distancia.", "{0} eventos por distancia.",
        "No hay susurros activos.", "Ahora no hay eventos. Aparecen poco antes de empezar.",
        "No se puede leer qué está activo.", "1 favor siniestro", "{0} favores siniestros", "quedan {0} minutos", "queda 1 minuto", "empieza en {0} minutos", "empieza en 1 minuto",
        "Todavía leyendo los eventos del mapa; prueba de nuevo en unos segundos.", "Otro programa usa {0}; usa {1}.",
        "Otros programas usan {0} y {1}; esa lista no se puede abrir.");

    public static readonly EventStrings English = new(
        "Events menu:", "Helltide", "Whispers", "Events", "{0} whispers by distance.", "{0} events by distance.",
        "No whispers are up.", "No events right now. They show shortly before they start.",
        "Can't read what is up.", "1 Grim Favor", "{0} Grim Favors", "{0} minutes left", "1 minute left", "starts in {0} minutes", "starts in 1 minute",
        "Still reading the map's events; try again in a few seconds.", "Another program holds {0}; use {1}.",
        "Other programs hold {0} and {1}; that list can't be opened.");

    public static readonly EventStrings German = new(
        "Ereignismenü:", "Höllenflut", "Flüstern", "Ereignisse", "{0} Flüstern nach Entfernung.", "{0} Ereignisse nach Entfernung.",
        "Gerade kein Flüstern aktiv.", "Gerade keine Ereignisse. Sie erscheinen kurz vor ihrem Start.",
        "Es kann nicht gelesen werden, was aktiv ist.", "1 grausige Gabe", "{0} grausige Gaben", "noch {0} Minuten", "noch 1 Minute", "startet in {0} Minuten", "startet in 1 Minute",
        "Die Ereignisse der Karte werden noch gelesen; versuche es in ein paar Sekunden erneut.", "Ein anderes Programm belegt {0}; nimm {1}.",
        "Andere Programme belegen {0} und {1}; diese Liste lässt sich nicht öffnen.");

    public static readonly EventStrings French = new(
        "Menu des événements :", "Marée infernale", "Murmures", "Événements", "{0} murmures par distance.", "{0} événements par distance.",
        "Aucun murmure actif.", "Aucun événement pour l'instant. Ils apparaissent peu avant de commencer.",
        "Impossible de lire ce qui est actif.", "1 faveur sinistre", "{0} faveurs sinistres", "encore {0} minutes", "encore 1 minute", "commence dans {0} minutes", "commence dans 1 minute",
        "Lecture des événements de la carte en cours ; réessayez dans quelques secondes.", "Un autre programme utilise {0} ; utilisez {1}.",
        "D'autres programmes utilisent {0} et {1} ; cette liste ne peut pas s'ouvrir.");

    public static readonly EventStrings Italian = new(
        "Menu eventi:", "Marea Infernale", "Sussurri", "Eventi", "{0} sussurri per distanza.", "{0} eventi per distanza.",
        "Nessun sussurro attivo.", "Nessun evento al momento. Compaiono poco prima di iniziare.",
        "Impossibile leggere cosa è attivo.", "1 favore sinistro", "{0} favori sinistri", "ancora {0} minuti", "ancora 1 minuto", "inizia tra {0} minuti", "inizia tra 1 minuto",
        "Lettura degli eventi della mappa in corso; riprova tra qualche secondo.", "Un altro programma usa {0}; usa {1}.",
        "Altri programmi usano {0} e {1}; quella lista non si può aprire.");

    public static readonly EventStrings Polish = new(
        "Menu wydarzeń:", "Piekielny Przypływ", "Szepty", "Wydarzenia", "Szepty według odległości: {0}.", "Wydarzenia według odległości: {0}.",
        "Brak aktywnych szeptów.", "Na razie brak wydarzeń. Pojawiają się krótko przed rozpoczęciem.",
        "Nie można odczytać, co jest aktywne.", "1 ponura łaska", "Ponure łaski: {0}", "pozostało minut: {0}", "pozostała 1 minuta", "początek za {0} min", "początek za 1 min",
        "Trwa odczyt wydarzeń z mapy; spróbuj ponownie za kilka sekund.", "Inny program zajmuje {0}; użyj {1}.",
        "Inne programy zajmują {0} i {1}; tej listy nie da się otworzyć.");

    public static readonly EventStrings Portuguese = new(
        "Menu de eventos:", "Maré Infernal", "Sussurros", "Eventos", "{0} sussurros por distância.", "{0} eventos por distância.",
        "Nenhum sussurro ativo.", "Nenhum evento agora. Eles aparecem pouco antes de começar.",
        "Não é possível ler o que está ativo.", "1 favor sinistro", "{0} favores sinistros", "faltam {0} minutos", "falta 1 minuto", "começa em {0} minutos", "começa em 1 minuto",
        "Ainda lendo os eventos do mapa; tente de novo em alguns segundos.", "Outro programa usa {0}; use {1}.",
        "Outros programas usam {0} e {1}; essa lista não pode ser aberta.");

    public static readonly EventStrings Russian = new(
        "Меню событий:", "Адский прилив", "Шёпоты", "События", "Шёпоты по расстоянию: {0}.", "События по расстоянию: {0}.",
        "Активных шёпотов нет.", "Сейчас событий нет. Они появляются незадолго до начала.",
        "Не удаётся прочитать, что активно.", "1 мрачная милость", "Мрачные милости: {0}", "осталось минут: {0}", "осталась 1 минута", "начало через {0} мин.", "начало через 1 мин.",
        "События карты ещё читаются; попробуйте снова через несколько секунд.", "{0} занято другой программой; используйте {1}.",
        "{0} и {1} заняты другими программами; этот список не открыть.");

    public static readonly EventStrings Turkish = new(
        "Etkinlik menüsü:", "Cehennem Dalgası", "Fısıltılar", "Etkinlikler", "Mesafeye göre {0} fısıltı.", "Mesafeye göre {0} etkinlik.",
        "Etkin fısıltı yok.", "Şu an etkinlik yok. Başlamadan kısa süre önce görünürler.",
        "Neyin etkin olduğu okunamıyor.", "1 Karanlık Lütuf", "{0} Karanlık Lütuf", "{0} dakika kaldı", "1 dakika kaldı", "{0} dakika sonra başlıyor", "1 dakika sonra başlıyor",
        "Haritanın etkinlikleri hâlâ okunuyor; birkaç saniye sonra tekrar deneyin.", "{0} başka bir program tarafından kullanılıyor; {1} kullanın.",
        "{0} ve {1} başka programlar tarafından kullanılıyor; bu liste açılamıyor.");

    public static readonly EventStrings Japanese = new(
        "イベントメニュー:", "ヘルタイド", "囁き", "イベント", "囁き {0} 件、距離順。", "イベント {0} 件、距離順。",
        "有効な囁きはありません。", "現在イベントはありません。開始の少し前に表示されます。",
        "有効なものを読み取れません。", "不気味な恩恵 1", "不気味な恩恵 {0}", "残り {0} 分", "残り 1 分", "{0} 分後に開始", "1 分後に開始",
        "マップのイベントを読み込み中です。数秒後にもう一度お試しください。", "{0} は別のプログラムが使用中です。{1} を使ってください。",
        "{0} と {1} は別のプログラムが使用中のため、このリストは開けません。");

    public static readonly EventStrings Korean = new(
        "이벤트 메뉴:", "지옥물결", "속삭임", "이벤트", "거리순 속삭임 {0}개.", "거리순 이벤트 {0}개.",
        "활성 속삭임이 없습니다.", "지금은 이벤트가 없습니다. 시작 직전에 나타납니다.",
        "활성 항목을 읽을 수 없습니다.", "음산한 은총 1개", "음산한 은총 {0}개", "{0}분 남음", "1분 남음", "{0}분 후 시작", "1분 후 시작",
        "지도의 이벤트를 읽는 중입니다. 몇 초 후에 다시 시도하세요.", "{0}은(는) 다른 프로그램이 사용 중입니다. {1}을(를) 사용하세요.",
        "{0}와(과) {1}을(를) 다른 프로그램이 사용 중이라 이 목록을 열 수 없습니다.");

    public static readonly EventStrings ChineseSimplified = new(
        "事件菜单:", "地狱狂潮", "低语", "事件", "按距离排列的低语 {0} 个。", "按距离排列的事件 {0} 个。",
        "没有进行中的低语。", "目前没有事件。它们会在开始前不久出现。",
        "无法读取进行中的内容。", "冷酷恩惠 1 个", "冷酷恩惠 {0} 个", "还剩 {0} 分钟", "还剩 1 分钟", "{0} 分钟后开始", "1 分钟后开始",
        "仍在读取地图上的事件，请几秒后再试。", "{0} 已被其他程序占用，请使用 {1}。",
        "{0} 和 {1} 已被其他程序占用，无法打开该列表。");

    public static readonly EventStrings ChineseTraditional = new(
        "事件選單:", "地獄狂潮", "低語", "事件", "按距離排列的低語 {0} 個。", "按距離排列的事件 {0} 個。",
        "沒有進行中的低語。", "目前沒有事件。它們會在開始前不久出現。",
        "無法讀取進行中的內容。", "冷酷恩惠 1 個", "冷酷恩惠 {0} 個", "還剩 {0} 分鐘", "還剩 1 分鐘", "{0} 分鐘後開始", "1 分鐘後開始",
        "仍在讀取地圖上的事件，請幾秒後再試。", "{0} 已被其他程式佔用，請使用 {1}。",
        "{0} 和 {1} 已被其他程式佔用，無法開啟該清單。");

    public static EventStrings For(SpeechLanguage language) => language switch
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
