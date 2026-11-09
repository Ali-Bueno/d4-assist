using Prismatoid;

namespace D4Assist.Speech;

/// <summary>
/// Routes speech through PRISM via the official Prismatoid .NET binding, which targets net10.0 and loads
/// fine in this standalone app (reference/screen-reader-integration/prism-prebuilt.md — the binding does
/// NOT load inside injected/BepInEx hosts, but this is not one).
/// </summary>
public sealed class PrismSpeechSink : ISpeechSink
{
    // PRISM's ids for the backends its Wine bridges serve (prism.h, v0.18.3).
    private const ulong OrcaId = 0x10AA1FC05A17F96C;
    private const ulong SpeechDispatcherId = 0xE3D6F895D949EBFE;
    private const ulong SpielId = 0x478B44F14AD3D89C;
    private static readonly ulong[] WineBridgeIds = [OrcaId, SpeechDispatcherId, SpielId];

    private readonly PrismContext _context;
    private readonly IPrismBackend _backend;

    private PrismSpeechSink(PrismContext context, IPrismBackend backend)
    {
        _context = context;
        _backend = backend;
    }

    /// <summary>The sink on PRISM's best backend (under Wine, the best one its bridges reach), or null with the reason when it
    /// finds no screen reader or voice.</summary>
    public static PrismSpeechSink? TryCreate(out string? failure)
    {
        bool wine = WineHost.Version is not null;
        if (wine) PrismLog.Open();
        PrismContext? context = null;
        try
        {
            failure = null;
            context = new PrismContext();
            var backend = wine ? AcquireBridge(context, out failure) : context.AcquireBestBackend();
            if (backend is null)
            {
                context.Dispose();
                return null;
            }
            return new PrismSpeechSink(context, backend);
        }
        catch (PrismException e)
        {
            context?.Dispose();
            failure = e.Message;
            return null;
        }
        finally
        {
            if (wine) PrismLog.Close();
        }
    }

    /// <summary>The backends PRISM's Wine bridges serve, highest priority first: the only ones that speak under Wine.</summary>
    public static IEnumerable<PrismBackendInfo> WineBridgesByPriority(IEnumerable<PrismBackendInfo> backends) =>
        backends.Where(b => WineBridgeIds.Contains(b.Id)).OrderByDescending(b => b.Priority);

    // Never PRISM's best under Wine. Proton's SAPI (protontts over eSpeak-NG, whose data Steam's runtime lacks) says it has
    // a voice and stays mute, and it ties with Speech Dispatcher at 97 below Orca's 100, so acquire_best picks it whenever
    // Orca's bridge can't load; NVDA fails PRISM's check there from v0.16.2 (d2r-access docs/proton.md, 2026-11-09).
    private static IPrismBackend? AcquireBridge(PrismContext context, out string? failure)
    {
        var backends = context.AvailableBackends;
        Console.WriteLine($"PRISM backends: {string.Join(", ", backends.Select(b => $"{b.Name} {b.Priority}"))}");
        var reasons = new List<string>();
        foreach (var bridge in WineBridgesByPriority(backends))
        {
            try
            {
                failure = null;
                return context.AcquireBackend(bridge.Id);
            }
            catch (PrismException e)
            {
                Console.WriteLine($"PRISM {bridge.Name} unavailable: {e.Message}");
                reasons.Add($"{bridge.Name}: {e.Message}");
            }
        }
        failure = reasons.Count == 0
            ? "this prism.dll has no Wine bridges"
            : $"no Wine bridge reached Linux's speech ({string.Join("; ", reasons)})";
        return null;
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
