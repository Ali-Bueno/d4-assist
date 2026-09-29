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

    /// <summary>Master volume for the D2R-ported radar cues (D4Assist.Audio), 0 (silent) to 1 (full, already boosted over D2R — RadarCuePlayer.LoudnessBoost).</summary>
    public double RadarVolume { get; set; } = 1.0;

    /// <summary>Play the reactive radar (D4Assist.Radar) while in a dungeon.</summary>
    public bool RadarEnabled { get; set; } = true;

    /// <summary>Play looping positional beacons (D4Assist.Audio.SpatialLoopPlayer), e.g. at the next-floor portal.</summary>
    public bool BeaconSoundsEnabled { get; set; } = true;

    /// <summary>Volume of the beacon loops, 0 (silent) to 1 (full).</summary>
    public double BeaconVolume { get; set; } = 1.0;

    /// <summary>Play looping quest sounds at dungeon objectives: prisoners, switches, carryables and their receptacles, quest chests.</summary>
    public bool ObjectiveSoundsEnabled { get; set; } = true;

    /// <summary>Volume of the objective loops, 0 (silent) to 1 (full).</summary>
    public double ObjectiveVolume { get; set; } = 1.0;

    /// <summary>CoreTOC names of the actors that get a beacon loop; unknown names are logged and ignored.</summary>
    public string[] BeaconActorNames { get; set; } = ["X1_Undercity_WarpPad"];

    /// <summary>Exact (case-sensitive) CoreTOC names of non-DRLG worlds where only the radar and actor loops run (no announcements, no guide).
    /// Explicit, user-approved exception to "read memory only in a dungeon"; opt-in by this list. Default: the Mercenary den.</summary>
    // Empty by default: the one candidate, the Mercenary den, ships no detailed walkable grid (README "RadarOnlyWorlds").
    public string[] RadarOnlyWorlds { get; set; } = [];

    /// <summary>Read the player and nearby actors outside dungeons too (overworld, towns, story areas), for the beacons and
    /// objective loops there; no radar or announcements outside dungeons. User-approved 2026-09-29.</summary>
    public bool OutsideDungeonsEnabled { get; set; } = true;

    /// <summary>Lead through known campaign routes with the guide ping, e.g. from shelter to shelter in the desert sandstorm
    /// quest with Meshif (Game/QuestRoute.cs); D-pad left mutes it while it leads. Needs OutsideDungeonsEnabled.</summary>
    public bool QuestRoutesEnabled { get; set; } = true;

    /// <summary>Play the automatic exploration ping that leads to the nearest unexplored part of the dungeon.</summary>
    public bool ExplorationGuideEnabled { get; set; } = true;

    /// <summary>Guide default in dungeons other than the Undercity and the Pit (those default on); D-pad left toggles it in-game.</summary>
    public bool ExplorationGuideInDungeons { get; set; } = false;

    /// <summary>Volume of the exploration ping, 0 (silent) to 1 (full).</summary>
    public double ExplorationGuideVolume { get; set; } = 1.0;

    /// <summary>"Cardinal" (the stick's up/right/down/left, D2R's default) or "Diagonal" (along the engine axes).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RadarFrame>))]
    public RadarFrame RadarFrame { get; set; } = RadarFrame.Cardinal;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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
        return config;
    }

    /// <summary>The language forced by <see cref="Language"/>, or null for "auto".</summary>
    public SpeechLanguage? ForcedLanguage() => Language.Trim().ToLowerInvariant() switch
    {
        AutoLanguage => null,
        SpanishCode => SpeechLanguage.Spanish,
        EnglishCode => SpeechLanguage.English,
        _ => throw new InvalidDataException($"Language must be \"{AutoLanguage}\", \"{SpanishCode}\" or \"{EnglishCode}\", not \"{Language}\"."),
    };
}
