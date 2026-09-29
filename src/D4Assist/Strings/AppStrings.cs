namespace D4Assist.Strings;

/// <summary>Every app-level spoken string for one language (CLAUDE.md §9/§10); add a phrase once per language instance.</summary>
public sealed record AppStrings(
    string AssistantActive, string WaitingForGame, string EnteringDungeon, string LeavingDungeon,
    string GuideOn, string GuideOff, string EnteringWorldFormat, IReadOnlyList<string> SoundExamples)
{
    public static readonly AppStrings Spanish = new(
        "Asistente de mazmorras activo", "Esperando a Diablo IV", "Entrando en mazmorra", "Saliendo de la mazmorra",
        "Guía activada.", "Guía desactivada.", "Entrando en {0}",
        ["Salida", "Paso abierto", "Pared", "Puerta", "Salida sin explorar", "Sonido de misión", "Pitido de la guía", "Llegada"]);

    public static readonly AppStrings English = new(
        "Dungeon assistant active", "Waiting for Diablo IV", "Entering dungeon", "Leaving dungeon",
        "Guide on.", "Guide off.", "Entering {0}",
        ["Exit", "Open floor", "Wall", "Door", "Unexplored exit", "Quest sound", "Guide ping", "Arrival"]);

    public static AppStrings For(Navigation.SpeechLanguage language) => language == Navigation.SpeechLanguage.English ? English : Spanish;

    public string EnteredWorld(string worldName) => string.Format(EnteringWorldFormat, worldName);
}
