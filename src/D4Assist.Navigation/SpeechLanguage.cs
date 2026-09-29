namespace D4Assist.Navigation;

/// <summary>The two languages Diablo IV text and this assistant's speech exist in.</summary>
public enum SpeechLanguage
{
    Spanish,
    English,
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
