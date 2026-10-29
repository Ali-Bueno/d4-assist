namespace D4Assist.Speech;

/// <summary>
/// The single sink all accessibility output routes through (CLAUDE.md §9): gameplay code never talks to
/// a screen-reader backend directly, so swapping the backend or mocking it in a test touches only this.
/// </summary>
public interface ISpeechSink : IDisposable
{
    /// <summary>The backend name to log at startup (e.g. the PRISM driver, or "console" for the debug sink).</summary>
    string BackendName { get; }

    /// <summary><paramref name="interrupt"/> true on a context change (new dungeon/menu); false for incremental info.</summary>
    void Speak(string text, bool interrupt);

    /// <summary>Speaks <paramref name="text"/> and shows it on the braille display, where the backend has one (the waypoint
    /// list, user request 2026-10-25); a sink without braille only speaks.</summary>
    void Output(string text, bool interrupt) => Speak(text, interrupt);
}
