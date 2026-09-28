using Prismatoid;

namespace D4Assist.Speech;

/// <summary>
/// Routes speech through PRISM via the official Prismatoid .NET binding, which targets net10.0 and loads
/// fine in this standalone app (reference/screen-reader-integration/prism-prebuilt.md — the binding does
/// NOT load inside injected/BepInEx hosts, but this is not one).
/// </summary>
public sealed class PrismSpeechSink : ISpeechSink
{
    private readonly PrismContext _context = new();
    private readonly IPrismBackend _backend;

    public PrismSpeechSink()
    {
        _backend = _context.AcquireBestBackend();
    }

    public string BackendName => _backend.Name;

    public void Speak(string text, bool interrupt) => _backend.Speak(text, interrupt);

    public void Dispose()
    {
        _backend.Dispose();
        _context.Dispose();
    }
}
