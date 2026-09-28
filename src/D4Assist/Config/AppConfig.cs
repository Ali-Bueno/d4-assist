using System.Text.Json;
using System.Text.Json.Serialization;
using D4Assist.Audio;

namespace D4Assist.Config;

/// <summary>
/// d4assist.json, next to the exe (see README for the full list and defaults). A missing file, or a
/// missing individual field, falls back to the defaults below — only DefinitionsPath has no sane default
/// (it depends on where the user put the CASC definitions dump) and must be set explicitly.
/// </summary>
public sealed class AppConfig
{
    public const string FileName = "d4assist.json";

    // Matches D4MemScan's own default install location (Commands/GameData.DefaultInstallPath).
    public const string DefaultInstallPath = @"D:\games\battlenet\Diablo IV";

    public string InstallPath { get; set; } = DefaultInstallPath;
    public string DefinitionsPath { get; set; } = "";
    public bool SpeechEnabled { get; set; } = true;
    public bool AutoAnnouncementsEnabled { get; set; } = true;
    public bool InterruptOnContextChange { get; set; } = true;

    /// <summary>Master volume for the D2R-ported radar cues (D4Assist.Audio), 0 (silent) to 1 (full).</summary>
    public double RadarVolume { get; set; } = 1.0;

    /// <summary>Play the reactive radar (D4Assist.Radar) while in a dungeon.</summary>
    public bool RadarEnabled { get; set; } = true;

    /// <summary>"Cardinal" (the stick's up/right/down/left, D2R's default) or "Diagonal" (along the engine axes).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<RadarFrame>))]
    public RadarFrame RadarFrame { get; set; } = RadarFrame.Cardinal;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Defaults only when the file doesn't exist; a present-but-malformed file is a hard error.</summary>
    public static AppConfig Load(string path)
    {
        if (!File.Exists(path)) return new AppConfig();
        return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"{path} did not contain a valid config.");
    }
}
