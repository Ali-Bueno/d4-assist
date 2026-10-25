namespace D4Assist.Speech;

/// <summary>Prints instead of speaking; used behind `--no-speech` to debug without a screen reader running.</summary>
public sealed class ConsoleSpeechSink : ISpeechSink
{
    public string BackendName => "console (--no-speech)";

    public void Speak(string text, bool interrupt) => Console.WriteLine($"[speech{(interrupt ? "!" : "")}] {text}");

    public void Output(string text, bool interrupt) => Console.WriteLine($"[speech+braille{(interrupt ? "!" : "")}] {text}");

    public void Dispose() { }
}
