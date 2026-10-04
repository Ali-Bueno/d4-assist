using System.Text.Json;

namespace D4Assist.Update;

/// <summary>The newest GitHub release, as far as the updater needs it; <see cref="Parse"/> reads the releases/latest JSON.</summary>
public sealed record ReleaseInfo(Version Version, string Notes, string ZipUrl, string? Sha256)
{
    /// <summary>The release asset that holds the portable build; it is always named this.</summary>
    public const string ZipAssetName = "D4Assist.zip";

    private const string Sha256Prefix = "sha256:";

    /// <summary>The release in <paramref name="json"/>, or null when it has no readable version tag or no zip asset.</summary>
    public static ReleaseInfo? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!UpdateVersion.TryParse(Text(root, "tag_name"), out var version)) return null;
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (Text(asset, "name") != ZipAssetName || Text(asset, "browser_download_url") is not { Length: > 0 } url) continue;
            var digest = Text(asset, "digest");
            var sha = digest.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase) ? digest[Sha256Prefix.Length..].Trim() : null;
            return new ReleaseInfo(version, Text(root, "body"), url, string.IsNullOrEmpty(sha) ? null : sha);
        }
        return null;
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}
