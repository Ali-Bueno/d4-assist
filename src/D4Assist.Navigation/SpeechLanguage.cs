namespace D4Assist.Navigation;

/// <summary>The languages Diablo IV's text exists in, and so this assistant's speech.</summary>
public enum SpeechLanguage
{
    Spanish,
    English,
    French,
    German,
    Italian,
    Polish,
    Portuguese,
    Russian,
    Turkish,
    Japanese,
    Korean,
    ChineseSimplified,
    ChineseTraditional,
}

/// <summary>Each speech language's config code, the game locales that select it and its name in itself.</summary>
public static class SpeechLanguages
{
    private sealed record Info(SpeechLanguage Language, string Code, string[] Locales, string NativeName);

    // Diablo IV's text locales (Battle.net): esES and esMX are both Spanish; enUS is the fallback for anything unknown.
    private static readonly Info[] All =
    [
        new(SpeechLanguage.Spanish, "es", ["esES", "esMX"], "Español"),
        new(SpeechLanguage.English, "en", ["enUS", "enGB"], "English"),
        new(SpeechLanguage.French, "fr", ["frFR"], "Français"),
        new(SpeechLanguage.German, "de", ["deDE"], "Deutsch"),
        new(SpeechLanguage.Italian, "it", ["itIT"], "Italiano"),
        new(SpeechLanguage.Polish, "pl", ["plPL"], "Polski"),
        new(SpeechLanguage.Portuguese, "pt", ["ptBR"], "Português (Brasil)"),
        new(SpeechLanguage.Russian, "ru", ["ruRU"], "Русский"),
        new(SpeechLanguage.Turkish, "tr", ["trTR"], "Türkçe"),
        new(SpeechLanguage.Japanese, "ja", ["jaJP"], "日本語"),
        new(SpeechLanguage.Korean, "ko", ["koKR"], "한국어"),
        new(SpeechLanguage.ChineseSimplified, "zh-cn", ["zhCN"], "简体中文"),
        new(SpeechLanguage.ChineseTraditional, "zh-tw", ["zhTW"], "繁體中文"),
    ];

    /// <summary>Every language, in the settings window's order.</summary>
    public static IReadOnlyList<SpeechLanguage> Supported { get; } = [.. All.Select(i => i.Language)];

    /// <summary>The value of d4assist.json's "Language" that forces this language.</summary>
    public static string Code(SpeechLanguage language) => Of(language).Code;

    /// <summary>The language's name in itself, for the settings window.</summary>
    public static string NativeName(SpeechLanguage language) => Of(language).NativeName;

    /// <summary>The language a config code names (any case), or null when none does.</summary>
    public static SpeechLanguage? FromCode(string code) =>
        All.FirstOrDefault(i => string.Equals(i.Code, code.Trim(), StringComparison.OrdinalIgnoreCase))?.Language;

    /// <summary>The language of a game text locale (e.g. "frFR"): an exact locale first, then its first two letters; English
    /// for anything else.</summary>
    public static SpeechLanguage FromLocale(string locale)
    {
        var exact = All.FirstOrDefault(i => i.Locales.Contains(locale, StringComparer.OrdinalIgnoreCase));
        if (exact is not null) return exact.Language;
        var prefix = locale.Length >= 2 ? locale[..2] : locale;
        return All.FirstOrDefault(i => i.Locales.Any(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))?.Language
            ?? SpeechLanguage.English;
    }

    private static Info Of(SpeechLanguage language) => All.First(i => i.Language == language);
}

/// <summary>The language currently spoken; switched at runtime so running announcers follow the game without a restart.</summary>
public sealed class LanguageSetting(SpeechLanguage initial = SpeechLanguage.Spanish)
{
    private volatile SpeechLanguage _current = initial;

    public SpeechLanguage Current => _current;

    /// <summary>Raised with the new language, only when it actually changed.</summary>
    public event Action<SpeechLanguage>? Changed;

    public void Set(SpeechLanguage language)
    {
        if (_current == language) return;
        _current = language;
        Changed?.Invoke(language);
    }
}
