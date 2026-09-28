namespace D4Assist.Strings;

/// <summary>
/// Every spoken string in one place (CLAUDE.md §9/§10): centralized, translatable, no literal text
/// scattered through gameplay/service code. All output is Spanish per the 2026-09-28 UX decision (STATUS.md).
/// </summary>
public static class SpanishStrings
{
    public const string AssistantActive = "Asistente de mazmorras activo";
    public const string WaitingForGame = "Esperando a Diablo IV";
    public const string EnteringDungeon = "Entrando en mazmorra";
    public const string LeavingDungeon = "Saliendo de la mazmorra";

    public static string EnteredWorld(string worldName) => $"Entrando en {worldName}";
}
