using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>What the events menu on the world map speaks (Shift+F2), per language: its two entries and the whispers' list. The helltide's
/// list speaks as HelltideStrings, a whisper's distance and the cursor jump as the waypoint list (WaypointStrings).</summary>
/// <param name="MenuOpened">Said before the first entry when the menu opens.</param>
/// <param name="Helltide">The menu's helltide entry.</param>
/// <param name="Whispers">The menu's whispers entry.</param>
/// <param name="WhispersOpenedFormat">{0}: how many whispers the list holds.</param>
/// <param name="NoWhispers">None is up, or none could be told apart.</param>
/// <param name="Unreadable">The world states that say which are up can't be read.</param>
/// <param name="OneFavor">A whisper paying one grim favor.</param>
/// <param name="FavorsFormat">{0}: how many grim favors a whisper pays, more than one.</param>
/// <param name="MinutesLeftFormat">{0}: the minutes a whisper has left.</param>
public sealed record EventStrings(
    string MenuOpened, string Helltide, string Whispers, string WhispersOpenedFormat, string NoWhispers, string Unreadable,
    string OneFavor, string FavorsFormat, string MinutesLeftFormat)
{
    public static readonly EventStrings Spanish = new(
        "Eventos:", "Marea infernal", "Susurros", "{0} susurros por distancia.", "No hay susurros activos.",
        "No se puede leer qué susurros están activos.", "1 favor siniestro", "{0} favores siniestros", "quedan {0} minutos");

    public static readonly EventStrings English = new(
        "Events:", "Helltide", "Whispers", "{0} whispers by distance.", "No whispers are up.",
        "Can't read which whispers are up.", "1 Grim Favor", "{0} Grim Favors", "{0} minutes left");

    public static readonly EventStrings German = new(
        "Ereignisse:", "Höllenflut", "Flüster-Ereignisse", "{0} Flüster-Ereignisse nach Entfernung.", "Gerade keine Flüster-Ereignisse.",
        "Die aktiven Flüster-Ereignisse können nicht gelesen werden.", "1 grausige Gabe", "{0} grausige Gaben", "noch {0} Minuten");

    public static readonly EventStrings French = new(
        "Événements :", "Marée infernale", "Murmures", "{0} murmures par distance.", "Aucun murmure actif.",
        "Impossible de lire quels murmures sont actifs.", "1 faveur sinistre", "{0} faveurs sinistres", "encore {0} minutes");

    public static readonly EventStrings Italian = new(
        "Eventi:", "Marea Infernale", "Sussurri", "{0} sussurri per distanza.", "Nessun sussurro attivo.",
        "Impossibile leggere quali sussurri sono attivi.", "1 favore sinistro", "{0} favori sinistri", "ancora {0} minuti");

    public static readonly EventStrings Polish = new(
        "Wydarzenia:", "Piekielny Przypływ", "Szepty", "Szepty według odległości: {0}.", "Brak aktywnych szeptów.",
        "Nie można odczytać aktywnych szeptów.", "1 ponura łaska", "Ponure łaski: {0}", "pozostało minut: {0}");

    public static readonly EventStrings Portuguese = new(
        "Eventos:", "Maré Infernal", "Sussurros", "{0} sussurros por distância.", "Nenhum sussurro ativo.",
        "Não é possível ler quais sussurros estão ativos.", "1 favor sinistro", "{0} favores sinistros", "faltam {0} minutos");

    public static readonly EventStrings Russian = new(
        "События:", "Адский прилив", "Шёпоты", "Шёпоты по расстоянию: {0}.", "Активных шёпотов нет.",
        "Не удаётся прочитать активные шёпоты.", "1 мрачная милость", "Мрачные милости: {0}", "осталось минут: {0}");

    public static readonly EventStrings Turkish = new(
        "Etkinlikler:", "Cehennem Dalgası", "Fısıltılar", "Mesafeye göre {0} fısıltı.", "Etkin fısıltı yok.",
        "Etkin fısıltılar okunamıyor.", "1 Karanlık Lütuf", "{0} Karanlık Lütuf", "{0} dakika kaldı");

    public static readonly EventStrings Japanese = new(
        "イベント:", "ヘルタイド", "囁き", "囁き {0} 件、距離順。", "有効な囁きはありません。",
        "有効な囁きを読み取れません。", "不気味な恩恵 1", "不気味な恩恵 {0}", "残り {0} 分");

    public static readonly EventStrings Korean = new(
        "이벤트:", "지옥물결", "속삭임", "거리순 속삭임 {0}개.", "활성 속삭임이 없습니다.",
        "활성 속삭임을 읽을 수 없습니다.", "음산한 은총 1개", "음산한 은총 {0}개", "{0}분 남음");

    public static readonly EventStrings ChineseSimplified = new(
        "事件:", "地狱狂潮", "低语", "按距离排列的低语 {0} 个。", "没有进行中的低语。",
        "无法读取进行中的低语。", "冷酷恩惠 1 个", "冷酷恩惠 {0} 个", "还剩 {0} 分钟");

    public static readonly EventStrings ChineseTraditional = new(
        "事件:", "地獄狂潮", "低語", "按距離排列的低語 {0} 個。", "沒有進行中的低語。",
        "無法讀取進行中的低語。", "冷酷恩惠 1 個", "冷酷恩惠 {0} 個", "還剩 {0} 分鐘");

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
