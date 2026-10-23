using D4Assist.Config;
using D4Assist.Navigation;

namespace D4Assist.Strings;

/// <summary>The notification-area menu and settings window text for one language (CLAUDE.md §9/§10).</summary>
public sealed partial record SettingsStrings
{
    public required string AppName { get; init; }
    public required string MenuSettings { get; init; }
    public required string MenuRestart { get; init; }
    public required string MenuCheckUpdates { get; init; }
    public required string MenuExit { get; init; }
    public required string AlreadyRunning { get; init; }
    public required string WindowTitle { get; init; }
    public required string Ok { get; init; }
    public required string Cancel { get; init; }
    public required string Apply { get; init; }
    public required string Defaults { get; init; }
    public required string RestartQuestion { get; init; }
    public required string SaveFailedFormat { get; init; }
    /// <summary>The update question: {0} the new version, {1} the current one, {2} the release notes.</summary>
    public required string UpdateHeadingFormat { get; init; }
    public required string UpdateTextFormat { get; init; }
    public required string UpdateInstall { get; init; }
    public required string UpdateLater { get; init; }
    public required string UpdateSkip { get; init; }

    public required string SoundTab { get; init; }
    public required string MasterVolume { get; init; }
    public required string Preview { get; init; }
    public required string VolumeColumn { get; init; }
    public required string SpeedColumn { get; init; }
    public required string SpeedDescription { get; init; }
    public required string GroupEnabledFormat { get; init; }
    public required string GroupVolumeFormat { get; init; }
    public required string GroupSpeedFormat { get; init; }
    public required string CueVolumeFormat { get; init; }
    public required string CueSpeedFormat { get; init; }
    public required string RadarDirections { get; init; }
    public required string GuideAndCharactersTab { get; init; }
    /// <summary>The choice of how many of the nearest characters to talk to tap at once.</summary>
    public required string NpcVoices { get; init; }
    public required IReadOnlyDictionary<SoundGroup, string> Groups { get; init; }
    public required IReadOnlyDictionary<SoundCue, string> Cues { get; init; }

    public required string GeneralTab { get; init; }
    public required string GeneralBox { get; init; }
    public required string PausesBox { get; init; }
    public required string Language { get; init; }
    /// <summary>The first language choice: follow the game; the languages follow by their own names.</summary>
    public required string LanguageAuto { get; init; }
    public required string RadarFrame { get; init; }
    public required IReadOnlyList<string> RadarFrameChoices { get; init; }
    public required string SpeechEnabled { get; init; }
    public required string AutoAnnouncements { get; init; }
    public required string InterruptOnContextChange { get; init; }
    public required string RadarClosedDoors { get; init; }
    public required string LiveBarriers { get; init; }
    public required string WaypointList { get; init; }
    public required string ExplorationGuide { get; init; }
    public required string ExplorationGuideInDungeons { get; init; }
    public required string CompassDirections { get; init; }
    public required string CheckForUpdates { get; init; }
    public required string QuestRoutes { get; init; }
    public required string OutsideDungeons { get; init; }
    public required string PauseInCutscenes { get; init; }
    public required string PauseInBackground { get; init; }
    public required string PauseInConversations { get; init; }
    public required string PauseInMenus { get; init; }
    public required string InstallFolder { get; init; }
    /// <summary>Shown as the folder while InstallPath is empty (found at each start).</summary>
    public required string InstallFolderAuto { get; init; }
    public required string Browse { get; init; }
    public required string InstallPickerTitle { get; init; }
    /// <summary>{0} is the folder the player picked.</summary>
    public required string WrongInstallFolderFormat { get; init; }
    /// <summary>At startup, before the folder picker opens.</summary>
    public required string InstallNotFound { get; init; }

    public static readonly SettingsStrings Spanish = new()
    {
        AppName = "Diablo 4 Assist",
        MenuSettings = "Configuración...",
        MenuRestart = "Reiniciar asistente",
        MenuCheckUpdates = "Buscar actualizaciones",
        MenuExit = "Salir",
        AlreadyRunning = "Diablo 4 Assist ya está en marcha. Está en la bandeja del sistema; Control+Alt+Shift+C abre su configuración.",
        WindowTitle = "Configuración de Diablo 4 Assist",
        Ok = "Aceptar",
        Cancel = "Cancelar",
        Apply = "Aplicar",
        Defaults = "Restaurar valores predeterminados",
        RestartQuestion = "Los cambios de la pestaña General se aplican al reiniciar el asistente. ¿Reiniciarlo ahora?",
        SaveFailedFormat = "No se pudo guardar la configuración: {0}",
        UpdateHeadingFormat = "Diablo 4 Assist {0} está disponible (tienes la {1}).",
        UpdateTextFormat = "¿Actualizar ahora? El asistente se reiniciará.\n\nNovedades:\n{0}",
        UpdateInstall = "Actualizar ahora",
        UpdateLater = "Recordármelo más tarde",
        UpdateSkip = "Omitir esta versión",

        SoundTab = "Sonido general",
        MasterVolume = "Volumen maestro",
        Preview = "Previsualizar el sonido al cambiar volumen o velocidad",
        VolumeColumn = "Volumen",
        SpeedColumn = "Velocidad",
        SpeedDescription = "Porcentaje: 100 es la velocidad normal, 200 el doble de rápido, 50 la mitad.",
        GroupEnabledFormat = "{0}: sonidos activados",
        GroupVolumeFormat = "{0}: volumen general",
        GroupSpeedFormat = "{0}: velocidad general",
        CueVolumeFormat = "{0}: volumen",
        CueSpeedFormat = "{0}: velocidad",
        RadarDirections = "Direcciones del radar",
        GuideAndCharactersTab = "Guía y personajes",
        NpcVoices = "Personajes que suenan a la vez",
        Groups = new Dictionary<SoundGroup, string>
        {
            [SoundGroup.Radar] = "Radar",
            [SoundGroup.Objects] = "Objetos",
            [SoundGroup.Quest] = "Misión y portales",
            [SoundGroup.Guide] = "Guía",
            [SoundGroup.Characters] = "Personajes",
            [SoundGroup.Danger] = "Peligro",
        },
        Cues = new Dictionary<SoundCue, string>
        {
            [SoundCue.RadarExit] = "Salida",
            [SoundCue.RadarOpen] = "Suelo abierto",
            [SoundCue.RadarWall] = "Pared",
            [SoundCue.RadarDoor] = "Puerta cerrada",
            [SoundCue.RadarFront] = "Delante",
            [SoundCue.RadarRight] = "Derecha",
            [SoundCue.RadarBack] = "Detrás",
            [SoundCue.RadarLeft] = "Izquierda",
            [SoundCue.Door] = "Puertas",
            [SoundCue.Chest] = "Cofres",
            [SoundCue.Corpse] = "Cadáveres",
            [SoundCue.Readable] = "Escritos y libros",
            [SoundCue.PointOfInterest] = "Puntos para mirar",
            [SoundCue.HealingWell] = "Pozos de curación",
            [SoundCue.Portal] = "Portales",
            [SoundCue.Waypoint] = "Puntos de ruta sin activar",
            [SoundCue.QuestObjective] = "Objetivos de misión",
            [SoundCue.Prisoner] = "Prisioneros",
            [SoundCue.Enticement] = "Braseros espirituales de la Ciudad Subterránea",
            [SoundCue.SafeZone] = "Barreras protectoras en combates contra jefes",
            [SoundCue.StolenSkill] = "Orbes de las habilidades que roba Mefisto",
            [SoundCue.AkaratLight] = "Luces de Akarat contra el Heraldo del Odio",
            [SoundCue.WardBreaker] = "Vasijas de los sellos contra el Guardián Horádrico",
            [SoundCue.GuidePing] = "Pitido de la guía",
            [SoundCue.GuideArrival] = "Llegada",
            [SoundCue.Npc] = "Personajes con los que hablar",
            [SoundCue.Boss] = "Jefes",
            [SoundCue.Elite] = "Élites y únicos",
            [SoundCue.Telegraph] = "Aviso de ataques de jefe",
        },

        GeneralTab = "General",
        GeneralBox = "Opciones (se aplican al reiniciar el asistente)",
        PausesBox = "Silenciar los sonidos (se aplica al reiniciar el asistente)",
        Language = "Idioma",
        LanguageAuto = "Automático, el del juego",
        RadarFrame = "Orientación del radar",
        RadarFrameChoices = ["Cardinal: arriba, derecha, abajo e izquierda del stick", "Diagonal: los ejes del juego"],
        SpeechEnabled = "Voz por el lector de pantalla",
        AutoAnnouncements = "Anunciar salas y mazmorras automáticamente",
        InterruptOnContextChange = "Interrumpir la voz al entrar o salir de una mazmorra",
        RadarClosedDoors = "Puertas cerradas en el radar",
        LiveBarriers = "Barreras de las mazmorras en el radar y la guía",
        WaypointList = "Lista de puntos de ruta en el mapa (Mayús+F1)",
        ExplorationGuide = "Guía de exploración (cruceta izquierda la activa y desactiva)",
        ExplorationGuideInDungeons = "Guía activada al entrar en mazmorras normales (en la Ciudad Subterránea y el Pozo siempre empieza activada)",
        CompassDirections = "Decir las direcciones como puntos cardinales (norte, este…) en vez de arriba, derecha…",
        CheckForUpdates = "Buscar actualizaciones al iniciar",
        QuestRoutes = "Rutas de misión (refugios de la tormenta de arena)",
        OutsideDungeons = "Funcionar también fuera de las mazmorras",
        PauseInCutscenes = "En las cinemáticas",
        PauseInBackground = "Cuando el juego no está en primer plano",
        PauseInConversations = "En las conversaciones",
        PauseInMenus = "En los menús del juego",
        InstallFolder = "Carpeta de Diablo IV",
        InstallFolderAuto = "Automática: Battle.net, Steam, el juego abierto o una búsqueda en los discos",
        Browse = "Examinar...",
        InstallPickerTitle = "Elige la carpeta de Diablo IV",
        WrongInstallFolderFormat = "No hay ningún Diablo IV en {0}. Elige la carpeta que tiene Diablo IV.exe.",
        InstallNotFound = "Diablo 4 Assist no encontró Diablo IV. En la siguiente ventana, elige la carpeta de Diablo IV, la que tiene Diablo IV.exe. Se guarda en d4assist.json, así que solo hay que hacerlo una vez.",
    };

    public static readonly SettingsStrings English = new()
    {
        AppName = "Diablo 4 Assist",
        MenuSettings = "Settings...",
        MenuRestart = "Restart assistant",
        MenuCheckUpdates = "Check for updates",
        MenuExit = "Exit",
        AlreadyRunning = "Diablo 4 Assist is already running. It is in the notification area; Control+Alt+Shift+C opens its settings.",
        WindowTitle = "Diablo 4 Assist settings",
        Ok = "OK",
        Cancel = "Cancel",
        Apply = "Apply",
        Defaults = "Restore defaults",
        RestartQuestion = "Changes on the General tab take effect when the assistant restarts. Restart it now?",
        SaveFailedFormat = "The settings could not be saved: {0}",
        UpdateHeadingFormat = "Diablo 4 Assist {0} is available (you have {1}).",
        UpdateTextFormat = "Update now? The assistant will restart.\n\nWhat's new:\n{0}",
        UpdateInstall = "Update now",
        UpdateLater = "Remind me later",
        UpdateSkip = "Skip this version",

        SoundTab = "Sound",
        MasterVolume = "Master volume",
        Preview = "Preview the sound while changing volume or speed",
        VolumeColumn = "Volume",
        SpeedColumn = "Speed",
        SpeedDescription = "Percent: 100 is normal speed, 200 twice as fast, 50 half as fast.",
        GroupEnabledFormat = "{0}: sounds on",
        GroupVolumeFormat = "{0}: overall volume",
        GroupSpeedFormat = "{0}: overall speed",
        CueVolumeFormat = "{0}: volume",
        CueSpeedFormat = "{0}: speed",
        RadarDirections = "Radar directions",
        GuideAndCharactersTab = "Guide and characters",
        NpcVoices = "Characters to talk to heard at once",
        Groups = new Dictionary<SoundGroup, string>
        {
            [SoundGroup.Radar] = "Radar",
            [SoundGroup.Objects] = "Objects",
            [SoundGroup.Quest] = "Quest and portals",
            [SoundGroup.Guide] = "Guide",
            [SoundGroup.Characters] = "Characters",
            [SoundGroup.Danger] = "Danger",
        },
        Cues = new Dictionary<SoundCue, string>
        {
            [SoundCue.RadarExit] = "Exit",
            [SoundCue.RadarOpen] = "Open floor",
            [SoundCue.RadarWall] = "Wall",
            [SoundCue.RadarDoor] = "Closed door",
            [SoundCue.RadarFront] = "Front",
            [SoundCue.RadarRight] = "Right",
            [SoundCue.RadarBack] = "Back",
            [SoundCue.RadarLeft] = "Left",
            [SoundCue.Door] = "Doors",
            [SoundCue.Chest] = "Chests",
            [SoundCue.Corpse] = "Corpses",
            [SoundCue.Readable] = "Notes and books",
            [SoundCue.PointOfInterest] = "Spots to look at",
            [SoundCue.HealingWell] = "Healing wells",
            [SoundCue.Portal] = "Portals",
            [SoundCue.Waypoint] = "Waypoints not activated yet",
            [SoundCue.QuestObjective] = "Quest objectives",
            [SoundCue.Prisoner] = "Prisoners",
            [SoundCue.Enticement] = "Undercity spirit braziers",
            [SoundCue.SafeZone] = "Protective barriers in boss fights",
            [SoundCue.StolenSkill] = "Skill orbs stolen by Mephisto",
            [SoundCue.AkaratLight] = "Akarat's lights against the Harbinger of Hatred",
            [SoundCue.WardBreaker] = "Sigil vessels against the Horadric Guardian",
            [SoundCue.GuidePing] = "Guide ping",
            [SoundCue.GuideArrival] = "Arrival",
            [SoundCue.Npc] = "Characters to talk to",
            [SoundCue.Boss] = "Bosses",
            [SoundCue.Elite] = "Elites and uniques",
            [SoundCue.Telegraph] = "Boss attack warnings",
        },

        GeneralTab = "General",
        GeneralBox = "Options (take effect when the assistant restarts)",
        PausesBox = "Silence the sounds (takes effect when the assistant restarts)",
        Language = "Language",
        LanguageAuto = "Automatic, the game's",
        RadarFrame = "Radar orientation",
        RadarFrameChoices = ["Cardinal: the stick's up, right, down and left", "Diagonal: the game's axes"],
        SpeechEnabled = "Speech through the screen reader",
        AutoAnnouncements = "Announce rooms and dungeons automatically",
        InterruptOnContextChange = "Interrupt speech when entering or leaving a dungeon",
        RadarClosedDoors = "Closed doors in the radar",
        LiveBarriers = "Dungeon barriers in the radar and the guide",
        WaypointList = "Waypoint list on the map (Shift+F1)",
        ExplorationGuide = "Exploration guide (D-pad left turns it on and off)",
        ExplorationGuideInDungeons = "Guide on when entering ordinary dungeons (in the Undercity and the Pit it always starts on)",
        CompassDirections = "Say directions as compass points (north, east…) instead of up, right…",
        CheckForUpdates = "Check for updates at start",
        QuestRoutes = "Quest routes (sandstorm shelters)",
        OutsideDungeons = "Also work outside dungeons",
        PauseInCutscenes = "In cutscenes",
        PauseInBackground = "While the game is not in the foreground",
        PauseInConversations = "In conversations",
        PauseInMenus = "In game menus",
        InstallFolder = "Diablo IV folder",
        InstallFolderAuto = "Automatic: Battle.net, Steam, the running game or a search of your drives",
        Browse = "Browse...",
        InstallPickerTitle = "Choose the Diablo IV folder",
        WrongInstallFolderFormat = "There is no Diablo IV in {0}. Choose the folder that has Diablo IV.exe in it.",
        InstallNotFound = "Diablo 4 Assist couldn't find Diablo IV. In the next window, choose the Diablo IV folder, the one with Diablo IV.exe in it. It is saved in d4assist.json, so you only do this once.",
    };

    public static SettingsStrings For(SpeechLanguage language) => language switch
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
