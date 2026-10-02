using System.Runtime.Versioning;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace D4Data.Casc;

/// <summary>Finds Diablo IV installed through Steam: Steam's uninstall entry, then the app manifest in every Steam library.</summary>
public static class SteamInstall
{
    /// <summary>Diablo IV's Steam app id (store.steampowered.com/app/2344520): names its uninstall key and app manifest.</summary>
    public const int AppId = 2344520;

    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App ";
    private const string SteamKey = @"SOFTWARE\Valve\Steam";
    private const string SteamApps = "steamapps";

    /// <summary>The first Steam location of the game that <paramref name="isInstall"/> accepts; null when Steam has none.</summary>
    [SupportedOSPlatform("windows")]
    public static string? Locate(Func<string, bool> isInstall) => Candidates().FirstOrDefault(isInstall);

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> Candidates()
    {
        if (ReadRegistry(RegistryHive.LocalMachine, UninstallKey + AppId, "InstallLocation") is { } location) yield return location;
        var steamFolders = new[]
        {
            ReadRegistry(RegistryHive.CurrentUser, SteamKey, "SteamPath"),
            ReadRegistry(RegistryHive.LocalMachine, SteamKey, "InstallPath"),
        };
        foreach (var steam in steamFolders.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (var install in LibraryInstalls(steam, ReadText))
                yield return install;
    }

    /// <summary>Where each Steam library of <paramref name="steamFolder"/> holds the game, read from its app manifest.</summary>
    public static IEnumerable<string> LibraryInstalls(string steamFolder, Func<string, string?> readText)
    {
        var libraryList = readText(Path.Combine(steamFolder, SteamApps, "libraryfolders.vdf")) ?? "";
        var libraries = ValuesOf(libraryList, "path").Prepend(steamFolder).Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var library in libraries)
        {
            var manifest = readText(Path.Combine(library, SteamApps, $"appmanifest_{AppId}.acf"));
            if (manifest is not null && ValuesOf(manifest, "installdir").FirstOrDefault() is { Length: > 0 } folder)
                yield return Path.Combine(library, SteamApps, "common", folder);
        }
    }

    /// <summary>Every string value written for <paramref name="key"/> in Steam's KeyValues text (libraryfolders.vdf, *.acf).</summary>
    public static IEnumerable<string> ValuesOf(string keyValues, string key)
    {
        string? pendingKey = null;
        foreach (var token in Tokens(keyValues))
        {
            if (token is null) pendingKey = null; // a brace: the key before it named a block
            else if (pendingKey is null) pendingKey = token;
            else
            {
                if (pendingKey.Equals(key, StringComparison.OrdinalIgnoreCase)) yield return token;
                pendingKey = null;
            }
        }
    }

    // Strings in order, with null for each brace; quoted strings unescape \\ and \".
    private static IEnumerable<string?> Tokens(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) i++;
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '/') i = SkipLine(text, i);
            else if (c is '{' or '}') { i++; yield return null; }
            else if (c == '"')
            {
                var value = new StringBuilder();
                for (i++; i < text.Length && text[i] != '"'; i++)
                    value.Append(text[i] == '\\' && i + 1 < text.Length ? text[++i] : text[i]);
                i++;
                yield return value.ToString();
            }
            else
            {
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('"' or '{' or '}')) i++;
                yield return text[start..i];
            }
        }
    }

    private static int SkipLine(string text, int i)
    {
        int end = text.IndexOf('\n', i);
        return end < 0 ? text.Length : end + 1;
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadRegistry(RegistryHive hive, string subKey, string name)
    {
        // Steam is a 32-bit program: its HKLM keys live in the 32-bit view (WOW6432Node).
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(subKey);
                if (key?.GetValue(name) is string { Length: > 0 } value) return Path.GetFullPath(value);
            }
            catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException or ArgumentException) { }
        }
        return null;
    }

    private static string? ReadText(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
}
