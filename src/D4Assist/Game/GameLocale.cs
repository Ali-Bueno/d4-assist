using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>Reads the game's text locale from a FenrisDebug.txt line and maps it to a speech language.</summary>
public static class GameLocale
{
    private const string TextLocaleMarker = "Current Text Locale: ";

    /// <summary>The locale after "Current Text Locale:" (e.g. "enUS"), or null when the line isn't the locale line.</summary>
    public static string? ExtractTextLocale(string line)
    {
        int i = line.IndexOf(TextLocaleMarker, StringComparison.Ordinal);
        if (i < 0) return null;
        var rest = line[(i + TextLocaleMarker.Length)..];
        int end = rest.IndexOfAny([' ', '\t', '\r', '\n']);
        var locale = (end < 0 ? rest : rest[..end]).Trim();
        return locale.Length == 0 ? null : locale;
    }

    /// <summary>The Battle.net launcher's file in the install folder holding the game language (e.g. "esES").</summary>
    public const string LauncherFileName = "Launcher.db";

    // A locale code: two lowercase letters, two uppercase letters (enUS, esES, esMX).
    private const int LocaleLength = 4;

    /// <summary>The language Battle.net will start the game in, from <c>&lt;install&gt;\Launcher.db</c>; null when absent or not a locale.
    /// It is current before the game runs, unlike the last log line (research/runtime-signals.md §2).</summary>
    public static string? ReadLauncherLocale(string installPath)
    {
        try
        {
            var text = File.ReadAllText(Path.Combine(installPath, LauncherFileName)).Trim();
            return IsLocale(text) ? text : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    private static bool IsLocale(string text) =>
        text.Length == LocaleLength && char.IsAsciiLetterLower(text[0]) && char.IsAsciiLetterLower(text[1])
        && char.IsAsciiLetterUpper(text[2]) && char.IsAsciiLetterUpper(text[3]);

    /// <summary>The speech language of a game locale (SpeechLanguages.FromLocale): esES/esMX Spanish, frFR French...; English
    /// for a locale the game doesn't have.</summary>
    public static SpeechLanguage ToLanguage(string locale) => SpeechLanguages.FromLocale(locale);
}
