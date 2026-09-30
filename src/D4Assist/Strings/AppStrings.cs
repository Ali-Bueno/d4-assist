using D4Assist.Game;

namespace D4Assist.Strings;

/// <summary>Every app-level spoken string for one language (CLAUDE.md §9/§10); add a phrase once per language instance.</summary>
public sealed record AppStrings(
    string AssistantActive, string WaitingForGame, string EnteringDungeon, string LeavingDungeon,
    string GuideOn, string GuideOff, string EnteringWorldFormat, IReadOnlyList<string> SoundExamples,
    string ShelterGuide, string ShelterReachedFormat, string GuideToPortal, string GuideToUsedPortal)
{
    public static readonly AppStrings Spanish = new(
        "Asistente de mazmorras activo", "Esperando a Diablo IV", "Entrando en mazmorra", "Saliendo de la mazmorra",
        "Guía activada.", "Guía desactivada.", "Entrando en {0}",
        ["Salida", "Paso abierto", "Pared", "Puerta", "Salida sin explorar", "Sonido de misión", "Pitido de la guía", "Llegada"],
        "Guía de refugios.", "Refugio {0} de {1}.",
        "Todo explorado. Te llevo a un portal.", "Todo explorado. Te llevo al portal por el que llegaste.");

    public static readonly AppStrings English = new(
        "Dungeon assistant active", "Waiting for Diablo IV", "Entering dungeon", "Leaving dungeon",
        "Guide on.", "Guide off.", "Entering {0}",
        ["Exit", "Open floor", "Wall", "Door", "Unexplored exit", "Quest sound", "Guide ping", "Arrival"],
        "Shelter guide.", "Shelter {0} of {1}.",
        "All explored. Leading to a portal.", "All explored. Leading back to the portal you came through.");

    public static AppStrings For(Navigation.SpeechLanguage language) => language == Navigation.SpeechLanguage.English ? English : Spanish;

    public string EnteredWorld(string worldName) => string.Format(EnteringWorldFormat, worldName);

    /// <summary>Spoken when a quest route starts leading.</summary>
    public string RouteStarted(RouteKind kind) =>
        kind == RouteKind.Shelters ? ShelterGuide : throw new ArgumentOutOfRangeException(nameof(kind));

    /// <summary>Spoken on reaching point <paramref name="number"/> of <paramref name="count"/> (1-based).</summary>
    public string RoutePointReached(RouteKind kind, int number, int count) =>
        kind == RouteKind.Shelters ? string.Format(ShelterReachedFormat, number, count) : throw new ArgumentOutOfRangeException(nameof(kind));
}
