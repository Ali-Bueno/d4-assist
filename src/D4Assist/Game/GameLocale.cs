using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>Reads the game's text locale from a FenrisDebug.txt line and maps it to a speech language.</summary>
public static class GameLocale
{
    private const string TextLocaleMarker = "Current Text Locale: ";
    private const string SpanishLocalePrefix = "es";

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

    /// <summary>esES/esMX are Spanish; every other locale is English (only two languages exist).</summary>
    public static SpeechLanguage ToLanguage(string locale) =>
        locale.StartsWith(SpanishLocalePrefix, StringComparison.OrdinalIgnoreCase) ? SpeechLanguage.Spanish : SpeechLanguage.English;
}
