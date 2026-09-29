namespace D4Assist.Input;

/// <summary>
/// One D-pad left source from several pads. A press repeated by another source within <see cref="SamePressWindowMs"/>
/// is dropped: a Sony pad mirrored as a virtual Xbox pad (Steam Input, DS4Windows) would otherwise toggle twice.
/// </summary>
public sealed class CombinedPad : IGuideTogglePad
{
    public const int SamePressWindowMs = 200;

    private readonly Func<long> _nowMs;
    private readonly object _gate = new();
    private long? _lastPressMs;

    public event Action? DpadLeftPressed;

    /// <param name="nowMs">Milliseconds clock; tests pass their own.</param>
    public CombinedPad(IEnumerable<IGuideTogglePad> pads, Func<long>? nowMs = null)
    {
        _nowMs = nowMs ?? (() => Environment.TickCount64);
        foreach (var pad in pads) pad.DpadLeftPressed += OnPressed;
    }

    private void OnPressed()
    {
        lock (_gate)
        {
            long now = _nowMs();
            if (now - _lastPressMs < SamePressWindowMs) return;
            _lastPressMs = now;
        }
        DpadLeftPressed?.Invoke();
    }
}
