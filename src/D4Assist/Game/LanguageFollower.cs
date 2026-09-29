using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>Keeps a <see cref="LanguageSetting"/> in step with the game's text locale, unless the config forces a language.</summary>
public sealed class LanguageFollower
{
    private readonly LanguageSetting _setting;
    private readonly SpeechLanguage? _forced;
    private readonly Action<string>? _log;

    /// <param name="forced">Config-forced language; null follows the game.</param>
    /// <param name="log">Debug sink for the chosen language; null when not debugging.</param>
    public LanguageFollower(LanguageSetting setting, SpeechLanguage? forced, Action<string>? log = null)
    {
        _setting = setting;
        _forced = forced;
        _log = log;
        if (forced is { } language) setting.Set(language);
        setting.Changed += lang => _log?.Invoke($"Language: {lang}");
        _log?.Invoke($"Language: {setting.Current} ({(forced is null ? "following the game" : "forced by config")})");
    }

    /// <summary>Applies a game text locale (e.g. "enUS"); ignored when a language is forced.</summary>
    public void OnTextLocale(string locale)
    {
        if (_forced is null) _setting.Set(GameLocale.ToLanguage(locale));
    }
}
