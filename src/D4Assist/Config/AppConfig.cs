using System.Text.Json;
using System.Text.Json.Serialization;
using D4Assist.Audio;
using D4Assist.Navigation;

namespace D4Assist.Config;

/// <summary>
/// d4assist.json, next to the exe (see README for the full list and defaults). A missing file, or a
/// missing individual field, falls back to the defaults below, which need no machine-specific paths.
/// </summary>
public sealed class AppConfig
{
    public const string FileName = "d4assist.json";
    public const string AutoLanguage = "auto";
    private const string SpanishCode = "es";
    private const string EnglishCode = "en";


    /// <summary>Empty = find the Diablo IV install through Battle.net (SnoStore.TryLocateInstall).</summary>
    public string InstallPath { get; set; } = "";

    /// <summary>Empty = the definitions.json embedded in the exe (data/definitions.json at build time). A relative path is
    /// resolved against the exe's folder.</summary>
    public string DefinitionsPath { get; set; } = "";
    public bool SpeechEnabled { get; set; } = true;
    public bool AutoAnnouncementsEnabled { get; set; } = true;
    public bool InterruptOnContextChange { get; set; } = true;

    /// <summary>"auto" follows the game's text language (FenrisDebug.txt); "es" or "en" forces one.</summary>
    public string Language { get; set; } = AutoLanguage;

    /// <summary>Closed doors sound as doors in the radar (two knocks, like D2R) instead of open floor.</summary>
    public bool RadarClosedDoorsEnabled { get; set; } = true;

    /// <summary>Barrier walls a dungeon raises until its objective is done block the radar and the guide's routes while they stand.</summary>
    public bool LiveBarriersEnabled { get; set; } = true;

    /// <summary>Every sound cue's switch, volume and repeat speed, plus the master volume (the settings window's sound tabs).</summary>
    public SoundSettings Sound { get; set; } = new();

    /// <summary>Silence every sound cue while a game cutscene plays (from its start in FenrisDebug.txt until the world changes or you walk).</summary>
    public bool PauseCuesInCutscenes { get; set; } = true;

    /// <summary>Silence every sound cue while the game window is not in the foreground (like D2R); the sound examples still play.</summary>
    public bool PauseCuesInBackground { get; set; } = true;

    /// <summary>Silence every sound cue while the dialogue menu is open and while the conversation you chose in it plays.</summary>
    public bool PauseCuesInConversations { get; set; } = true;

    /// <summary>Silence every sound cue while a game menu (inventory, skills, map, pause) is open.</summary>
    public bool PauseCuesInMenus { get; set; } = true;

    /// <summary>CoreTOC names of the actors that get a beacon loop; unknown names are logged and ignored.</summary>
    public string[] BeaconActorNames { get; set; } = ["X1_Undercity_WarpPad"];

    /// <summary>Exact (case-sensitive) CoreTOC names of extra non-DRLG worlds where only the radar and actor loops run (no
    /// announcements, no guide), e.g. the open world. Story areas outside the open world get this without being listed.</summary>
    public string[] RadarOnlyWorlds { get; set; } = [];

    /// <summary>Read the player and nearby actors outside dungeons too (overworld, towns, story areas), for the beacons and
    /// objective loops there, plus the radar in story areas outside the open world; no announcements outside dungeons.
    /// User-approved 2026-09-29.</summary>
    public bool OutsideDungeonsEnabled { get; set; } = true;

    /// <summary>Lead through known campaign routes with the guide ping, e.g. from shelter to shelter in the desert sandstorm
    /// quest with Meshif (Game/QuestRoute.cs); D-pad left mutes it while it leads. Needs OutsideDungeonsEnabled.</summary>
    public bool QuestRoutesEnabled { get; set; } = true;

    /// <summary>Play the automatic exploration ping that leads to the nearest unexplored part of the dungeon.</summary>
    public bool ExplorationGuideEnabled { get; set; } = true;

    /// <summary>Guide default in dungeons other than the Undercity and the Pit (those default on); D-pad left toggles it in-game.</summary>
    public bool ExplorationGuideInDungeons { get; set; } = false;

    /// <summary>"Cardinal" (the stick's up/right/down/left, D2R's default) or "Diagonal" (along the engine axes).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RadarFrame>))]
    public RadarFrame RadarFrame { get; set; } = RadarFrame.Cardinal;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keeps install paths readable
    };

    /// <summary>The configured install folder when it exists, else the located one; null when neither is found.</summary>
    public string? ResolveInstallPath(Func<string, bool> directoryExists, Func<string?> locate) =>
        !string.IsNullOrWhiteSpace(InstallPath) && directoryExists(InstallPath) ? InstallPath : locate();

    /// <summary>DefinitionsPath, made absolute against <paramref name="baseDirectory"/> when relative; null (use the
    /// embedded copy) when empty.</summary>
    public string? ResolveDefinitionsPath(string baseDirectory) =>
        string.IsNullOrWhiteSpace(DefinitionsPath) ? null
        : Path.IsPathRooted(DefinitionsPath) ? DefinitionsPath : Path.Combine(baseDirectory, DefinitionsPath);

    /// <summary>Defaults only when the file doesn't exist; a present-but-malformed file is a hard error.</summary>
    public static AppConfig Load(string path)
    {
        if (!File.Exists(path)) return new AppConfig();
        var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"{path} did not contain a valid config.");
        config.ForcedLanguage(); // validate now so a bad value fails at startup
        config.Sound ??= new SoundSettings();
        return config;
    }

    /// <summary>Writes the whole config (every sound entry listed), replacing the file only once the new one is complete.</summary>
    public void Save(string path)
    {
        Sound.Complete();
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, ToJson());
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>An independent copy, e.g. the settings window's working copy.</summary>
    public AppConfig Clone() => JsonSerializer.Deserialize<AppConfig>(ToJson(), JsonOptions)!;

    /// <summary>True when every option but the sound section matches: those other options take effect on restart.</summary>
    public bool SameStartupOptions(AppConfig other) => WithoutSound().ToJson() == other.WithoutSound().ToJson();

    private AppConfig WithoutSound()
    {
        var copy = Clone();
        copy.Sound = new SoundSettings();
        return copy;
    }

    private string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>The language forced by <see cref="Language"/>, or null for "auto".</summary>
    public SpeechLanguage? ForcedLanguage() => Language.Trim().ToLowerInvariant() switch
    {
        AutoLanguage => null,
        SpanishCode => SpeechLanguage.Spanish,
        EnglishCode => SpeechLanguage.English,
        _ => throw new InvalidDataException($"Language must be \"{AutoLanguage}\", \"{SpanishCode}\" or \"{EnglishCode}\", not \"{Language}\"."),
    };
}
