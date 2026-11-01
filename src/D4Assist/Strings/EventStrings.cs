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
/// <param name="MinutesLeftFormat">{0}: the minutes an entry has left.</param>
public sealed record EventStrings(
    string MenuOpened, string Helltide, string Whispers, string Events, string WhispersOpenedFormat, string EventsOpenedFormat,
    string NoWhispers, string NoEvents, string Unreadable, string OneFavor, string FavorsFormat, string MinutesLeftFormat)
{
    public static readonly EventStrings Spanish = new(
        "Menú de eventos:", "Marea infernal", "Susurros", "Eventos", "{0} susurros por distancia.", "{0} eventos por distancia.",
        "No hay susurros activos.", "Ahora no hay eventos. Aparecen poco antes de empezar.",
        "No se puede leer qué está activo.", "1 favor siniestro", "{0} favores siniestros", "quedan {0} minutos");

    public static readonly EventStrings English = new(
        "Events menu:", "Helltide", "Whispers", "Events", "{0} whispers by distance.", "{0} events by distance.",
        "No whispers are up.", "No events right now. They show shortly before they start.",
        "Can't read what is up.", "1 Grim Favor", "{0} Grim Favors", "{0} minutes left");

    public static readonly EventStrings German = new(
        "Ereignismenü:", "Höllenflut", "Flüstern", "Ereignisse", "{0} Flüstern nach Entfernung.", "{0} Ereignisse nach Entfernung.",
        "Gerade kein Flüstern aktiv.", "Gerade keine Ereignisse. Sie erscheinen kurz vor ihrem Start.",
        "Es kann nicht gelesen werden, was aktiv ist.", "1 grausige Gabe", "{0} grausige Gaben", "noch {0} Minuten");

    public static readonly EventStrings French = new(
        "Menu des événements :", "Marée infernale", "Murmures", "Événements", "{0} murmures par distance.", "{0} événements par distance.",
        "Aucun murmure actif.", "Aucun événement pour l'instant. Ils apparaissent peu avant de commencer.",
        "Impossible de lire ce qui est actif.", "1 faveur sinistre", "{0} faveurs sinistres", "encore {0} minutes");

    public static readonly EventStrings Italian = new(
        "Menu eventi:", "Marea Infernale", "Sussurri", "Eventi", "{0} sussurri per distanza.", "{0} eventi per distanza.",
        "Nessun sussurro attivo.", "Nessun evento al momento. Compaiono poco prima di iniziare.",
        "Impossibile leggere cosa è attivo.", "1 favore sinistro", "{0} favori sinistri", "ancora {0} minuti");

    public static readonly EventStrings Polish = new(
        "Menu wydarzeń:", "Piekielny Przypływ", "Szepty", "Wydarzenia", "Szepty według odległości: {0}.", "Wydarzenia według odległości: {0}.",
        "Brak aktywnych szeptów.", "Na razie brak wydarzeń. Pojawiają się krótko przed rozpoczęciem.",
        "Nie można odczytać, co jest aktywne.", "1 ponura łaska", "Ponure łaski: {0}", "pozostało minut: {0}");

    public static readonly EventStrings Portuguese = new(
        "Menu de eventos:", "Maré Infernal", "Sussurros", "Eventos", "{0} sussurros por distância.", "{0} eventos por distância.",
        "Nenhum sussurro ativo.", "Nenhum evento agora. Eles aparecem pouco antes de começar.",
        "Não é possível ler o que está ativo.", "1 favor sinistro", "{0} favores sinistros", "faltam {0} minutos");

    public static readonly EventStrings Russian = new(
        "Меню событий:", "Адский прилив", "Шёпоты", "События", "Шёпоты по расстоянию: {0}.", "События по расстоянию: {0}.",
        "Активных шёпотов нет.", "Сейчас событий нет. Они появляются незадолго до начала.",
        "Не удаётся прочитать, что активно.", "1 мрачная милость", "Мрачные милости: {0}", "осталось минут: {0}");

    public static readonly EventStrings Turkish = new(
        "Etkinlik menüsü:", "Cehennem Dalgası", "Fısıltılar", "Etkinlikler", "Mesafeye göre {0} fısıltı.", "Mesafeye göre {0} etkinlik.",
        "Etkin fısıltı yok.", "Şu an etkinlik yok. Başlamadan kısa süre önce görünürler.",
        "Neyin etkin olduğu okunamıyor.", "1 Karanlık Lütuf", "{0} Karanlık Lütuf", "{0} dakika kaldı");

    public static readonly EventStrings Japanese = new(
        "イベントメニュー:", "ヘルタイド", "囁き", "イベント", "囁き {0} 件、距離順。", "イベント {0} 件、距離順。",
        "有効な囁きはありません。", "現在イベントはありません。開始の少し前に表示されます。",
        "有効なものを読み取れません。", "不気味な恩恵 1", "不気味な恩恵 {0}", "残り {0} 分");

    public static readonly EventStrings Korean = new(
        "이벤트 메뉴:", "지옥물결", "속삭임", "이벤트", "거리순 속삭임 {0}개.", "거리순 이벤트 {0}개.",
        "활성 속삭임이 없습니다.", "지금은 이벤트가 없습니다. 시작 직전에 나타납니다.",
        "활성 항목을 읽을 수 없습니다.", "음산한 은총 1개", "음산한 은총 {0}개", "{0}분 남음");

    public static readonly EventStrings ChineseSimplified = new(
        "事件菜单:", "地狱狂潮", "低语", "事件", "按距离排列的低语 {0} 个。", "按距离排列的事件 {0} 个。",
        "没有进行中的低语。", "目前没有事件。它们会在开始前不久出现。",
        "无法读取进行中的内容。", "冷酷恩惠 1 个", "冷酷恩惠 {0} 个", "还剩 {0} 分钟");

    public static readonly EventStrings ChineseTraditional = new(
        "事件選單:", "地獄狂潮", "低語", "事件", "按距離排列的低語 {0} 個。", "按距離排列的事件 {0} 個。",
        "沒有進行中的低語。", "目前沒有事件。它們會在開始前不久出現。",
        "無法讀取進行中的內容。", "冷酷恩惠 1 個", "冷酷恩惠 {0} 個", "還剩 {0} 分鐘");

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
