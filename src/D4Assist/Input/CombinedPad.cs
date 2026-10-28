namespace D4Assist.Input;

/// <summary>
/// One D-pad left source from several pads. A press repeated by another source within <see cref="SamePressWindowMs"/>
/// is dropped, and so is a repeated hold: a Sony pad mirrored as a virtual Xbox pad (Steam Input, DS4Windows) would otherwise
/// toggle twice.
/// </summary>
public sealed class CombinedPad : IGuideTogglePad
{
    public const int SamePressWindowMs = 200;

    private readonly Func<long> _nowMs;
    private readonly object _gate = new();
    private long? _lastPressMs, _lastHeldMs;

    public event Action? DpadLeftPressed;
    public event Action? DpadLeftHeld;

    /// <param name="nowMs">Milliseconds clock; tests pass their own.</param>
    public CombinedPad(IEnumerable<IGuideTogglePad> pads, Func<long>? nowMs = null)
    {
        _nowMs = nowMs ?? (() => Environment.TickCount64);
        foreach (var pad in pads)
        {
            pad.DpadLeftPressed += () => { if (First(ref _lastPressMs)) DpadLeftPressed?.Invoke(); };
            pad.DpadLeftHeld += () => { if (First(ref _lastHeldMs)) DpadLeftHeld?.Invoke(); };
        }
    }

    // False for the same press (or hold) again from another source.
    private bool First(ref long? lastMs)
    {
        lock (_gate)
        {
            long now = _nowMs();
            if (now - lastMs < SamePressWindowMs) return false;
            lastMs = now;
            return true;
        }
    }
}
