using D4Assist.Game;

namespace D4Assist.Strings;

/// <summary>Every app-level spoken string for one language (CLAUDE.md §9/§10); add a phrase once per language instance.</summary>
public sealed partial record AppStrings(
    string AssistantActive, string WaitingForGame, string EnteringDungeon, string LeavingDungeon,
    string GuideOn, string GuideOff, string EnteringWorldFormat, IReadOnlyList<string> SoundExamples,
    string ShelterGuide, string ShelterReachedFormat, string GuideToPortal, string GuideToUsedPortal,
    string AssistantClosed, string Restarting, string SkillsStolen, string SkillsRecovered,
    IReadOnlyDictionary<ReadCheck, string> ReadFailures, string ReadWarningFormat, string Stopped,
    string UpdateAvailableFormat, string UpdateDownloading, string UpdateRestarting, string UpdateFailed,
    string UpdateFolderNotWritable, string UpToDate, string UpdateCheckFailed, string UpdateOnlyDownloaded,
    string UpdateProgressFormat, string UpdatedToFormat, string GuideDungeonsOnly, string ShelterRouteEnd, string DebugMode,
    string GuideToBossRoom, string TrailGuide, string TrailEnd)
{
    public static readonly AppStrings Spanish = new(
        "Diablo 4 Assist activo", "Esperando a Diablo IV", "Entrando en mazmorra", "Saliendo de la mazmorra",
        "Guía activada.", "Guía desactivada.", "Entrando en {0}",
        ["Salida", "Paso abierto", "Pared", "Puerta", "Salida sin explorar", "Sonido de misión", "Pitido de la guía", "Llegada"],
        "Guía de refugios.", "Refugio {0} de {1}.",
        "Todo explorado. Te llevo a un portal.", "Todo explorado. Te llevo al portal por el que llegaste.",
        "Diablo 4 Assist cerrado", "Reiniciando el asistente",
        "Mefisto te ha quitado las habilidades. Recoge los orbes.", "Habilidades recuperadas.",
        new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "no puedo leer la posición del personaje, el asistente no funciona",
            [ReadCheck.Addresses] = "no encuentro los datos de esta versión del juego, sin salas, radar ni sonidos de objetos",
            [ReadCheck.Rooms] = "no puedo leer las salas, sin anuncios de salas ni radar",
            [ReadCheck.Actors] = "no puedo leer los objetos, sin sonidos de portales, objetivos ni personajes",
            [ReadCheck.Attributes] = "no puedo leer el estado de los objetos, pueden sonar objetos ya usados y no se siguen las misiones",
            [ReadCheck.Menus] = "no encuentro los menús del juego, los sonidos no se callarán en ellos",
        },
        "Aviso: {0}. Manda el registro.",
        "El asistente se ha detenido por un error. Manda el registro.",
        "Diablo 4 Assist {0} está disponible. ¿Actualizar ahora?",
        "Descargando la actualización.",
        "Actualizado. Reiniciando.",
        "La actualización falló; tu versión no ha cambiado.",
        "La actualización falló: no se puede escribir en la carpeta de la aplicación. Tu versión no ha cambiado.",
        "Tienes la última versión.",
        "No se pudo comprobar si hay actualizaciones.",
        "Las actualizaciones solo funcionan en la versión descargada.",
        "{0} %",
        "Actualizado a la versión {0}.",
        "La guía solo funciona en mazmorras.",
        "Fin de la guía de refugios.",
        "Modo de depuración activo.",
        "Todo explorado. Te llevo a la sala del jefe.",
        "Guía del rastro de pétalos.", "Fin del rastro.");

    public static readonly AppStrings English = new(
        "Diablo 4 Assist active", "Waiting for Diablo IV", "Entering dungeon", "Leaving dungeon",
        "Guide on.", "Guide off.", "Entering {0}",
        ["Exit", "Open floor", "Wall", "Door", "Unexplored exit", "Quest sound", "Guide ping", "Arrival"],
        "Shelter guide.", "Shelter {0} of {1}.",
        "All explored. Leading to a portal.", "All explored. Leading back to the portal you came through.",
        "Diablo 4 Assist closed", "Restarting the assistant",
        "Mephisto took your skills. Pick up the orbs.", "Skills recovered.",
        new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "can't read the character's position, the assistant can't work",
            [ReadCheck.Addresses] = "can't find this game version's data, no rooms, radar or object sounds",
            [ReadCheck.Rooms] = "can't read the rooms, no room announcements or radar",
            [ReadCheck.Actors] = "can't read the objects, no portal, objective or character sounds",
            [ReadCheck.Attributes] = "can't read the objects' state, used objects may keep sounding and quests aren't followed",
            [ReadCheck.Menus] = "can't find the game menus, sounds won't pause in them",
        },
        "Warning: {0}. Please send the log.",
        "The assistant stopped because of an error. Please send the log.",
        "Diablo 4 Assist {0} is available. Update now?",
        "Downloading the update.",
        "Updated. Restarting.",
        "The update failed; your version is unchanged.",
        "The update failed: the app folder is not writable. Your version is unchanged.",
        "You have the latest version.",
        "Couldn't check for updates.",
        "Updates only work in the downloaded build.",
        "{0}%",
        "Updated to version {0}.",
        "The guide only works in dungeons.",
        "Shelter guide finished.",
        "Debug mode on.",
        "All explored. Leading you to the boss room.",
        "Petal trail guide.", "End of the trail.");

    public static AppStrings For(Navigation.SpeechLanguage language) => language switch
    {
        Navigation.SpeechLanguage.English => English,
        Navigation.SpeechLanguage.French => French,
        Navigation.SpeechLanguage.German => German,
        Navigation.SpeechLanguage.Italian => Italian,
        Navigation.SpeechLanguage.Polish => Polish,
        Navigation.SpeechLanguage.Portuguese => Portuguese,
        Navigation.SpeechLanguage.Russian => Russian,
        Navigation.SpeechLanguage.Turkish => Turkish,
        Navigation.SpeechLanguage.Japanese => Japanese,
        Navigation.SpeechLanguage.Korean => Korean,
        Navigation.SpeechLanguage.ChineseSimplified => ChineseSimplified,
        Navigation.SpeechLanguage.ChineseTraditional => ChineseTraditional,
        _ => Spanish,
    };

    public string EnteredWorld(string worldName) => string.Format(EnteringWorldFormat, worldName);

    /// <summary>Spoken once per run when a memory read keeps failing (ReadHealth).</summary>
    public string ReadWarning(ReadCheck check) => string.Format(ReadWarningFormat, ReadFailures[check]);

    /// <summary>Spoken when a quest route starts leading.</summary>
    public string RouteStarted(RouteKind kind) => kind switch
    {
        RouteKind.Shelters => ShelterGuide,
        RouteKind.Trail => TrailGuide,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Spoken on reaching point <paramref name="number"/> of <paramref name="count"/> (1-based); a trail speaks only
    /// its last point (null otherwise: a phrase every 10 units would talk over the game).</summary>
    public string? RoutePointReached(RouteKind kind, int number, int count) => kind switch
    {
        RouteKind.Shelters => string.Format(ShelterReachedFormat, number, count),
        RouteKind.Trail => number == count ? TrailEnd : null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Spoken on reaching the place a quest route ends at, past its last point.</summary>
    public string RouteEndReached(RouteKind kind) =>
        kind == RouteKind.Shelters ? ShelterRouteEnd : throw new ArgumentOutOfRangeException(nameof(kind));
}
