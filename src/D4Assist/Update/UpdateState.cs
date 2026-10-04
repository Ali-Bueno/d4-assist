using System.Text.Json;

namespace D4Assist.Update;

/// <summary>The one thing the updater remembers between runs: the version the player chose to skip.</summary>
public sealed record UpdateState(string? SkippedVersion)
{
    /// <summary>The state file's name; app-managed next to the exe and never part of the release zip.</summary>
    public const string FileName = "d4assist-update.json";

    private static readonly UpdateState None = new((string?)null);
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Whether a check should ask about <paramref name="available"/>: only a newer version, and an automatic check not one the player skipped.</summary>
    public static bool ShouldAsk(Version available, Version current, Version? skipped, bool manual) =>
        available > current && (manual || skipped is null || available > skipped);

    /// <summary>The skipped version, or null when none is saved or the file can't be read.</summary>
    public Version? Skipped => UpdateVersion.TryParse(SkippedVersion, out var version) ? version : null;

    /// <summary>The saved state; nothing skipped when the file is missing or corrupt.</summary>
    public static UpdateState Load(string path)
    {
        try { return JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(path)) ?? None; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return None; }
    }

    /// <summary>Writes the state; throws on I/O errors, the caller logs them.</summary>
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
}
