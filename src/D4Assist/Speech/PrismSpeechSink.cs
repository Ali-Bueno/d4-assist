using Prismatoid;

namespace D4Assist.Speech;

/// <summary>
/// Routes speech through PRISM via the official Prismatoid .NET binding, which targets net10.0 and loads
/// fine in this standalone app (reference/screen-reader-integration/prism-prebuilt.md — the binding does
/// NOT load inside injected/BepInEx hosts, but this is not one).
/// </summary>
public sealed class PrismSpeechSink : ISpeechSink
{
    private readonly PrismContext _context;
    private readonly IPrismBackend _backend;

    private PrismSpeechSink(PrismContext context, IPrismBackend backend)
    {
        _context = context;
        _backend = backend;
    }

    /// <summary>The sink on PRISM's best backend, or null with PRISM's reason when it finds no screen reader or voice
    /// (under Wine: no bridge reached Linux's speech).</summary>
    public static PrismSpeechSink? TryCreate(out string? failure)
    {
        PrismContext? context = null;
        try
        {
            context = new PrismContext();
            failure = null;
            return new PrismSpeechSink(context, context.AcquireBestBackend());
        }
        catch (PrismException e)
        {
            context?.Dispose();
            failure = e.Message;
            return null;
        }
    }

    public string BackendName => _backend.Name;

    public void Speak(string text, bool interrupt) => _backend.Speak(text, interrupt);

    // PRISM's output speaks and brailles at once (NVDA, JAWS); a backend with braille but no output gets both calls, one
    // without braille (SAPI) only speaks.
    public void Output(string text, bool interrupt)
    {
        var features = _backend.Features;
        if (features.HasFlag(PrismBackendFeature.SupportsOutput)) _backend.Output(text, interrupt);
        else
        {
            _backend.Speak(text, interrupt);
            if (features.HasFlag(PrismBackendFeature.SupportsBraille)) _backend.Braille(text);
        }
    }

    public void Dispose()
    {
        _backend.Dispose();
        _context.Dispose();
    }
}
