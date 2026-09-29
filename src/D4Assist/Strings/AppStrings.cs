namespace D4Assist.Strings;

/// <summary>Every app-level spoken string for one language (CLAUDE.md §9/§10); add a phrase once per language instance.</summary>
public sealed record AppStrings(
    string AssistantActive, string WaitingForGame, string EnteringDungeon, string LeavingDungeon,
    string GuideOn, string GuideOff, string EnteringWorldFormat)
{
    public static readonly AppStrings Spanish = new(
        "Asistente de mazmorras activo", "Esperando a Diablo IV", "Entrando en mazmorra", "Saliendo de la mazmorra",
        "Guía activada.", "Guía desactivada.", "Entrando en {0}");

    public static readonly AppStrings English = new(
        "Dungeon assistant active", "Waiting for Diablo IV", "Entering dungeon", "Leaving dungeon",
        "Guide on.", "Guide off.", "Entering {0}");

    public static AppStrings For(Navigation.SpeechLanguage language) => language == Navigation.SpeechLanguage.English ? English : Spanish;

    public string EnteredWorld(string worldName) => string.Format(EnteringWorldFormat, worldName);
}
