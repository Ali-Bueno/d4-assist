namespace D4Assist.Input;

/// <summary>
/// Tells a short press of one button from a hold, one sample per poll: a press reads as such once the button comes back up before
/// <see cref="HoldMs"/>, a hold as soon as it has stayed down that long (its release then reads as nothing). Not thread-safe:
/// one poll loop feeds it.
/// </summary>
public sealed class PressHold
{
    /// <summary>How long the button stays down to read as a hold (user, 2026-10-28: about a second).</summary>
    public const long HoldMs = 1000;

    public enum Edge { None, Pressed, Held }

    private long? _downSinceMs;
    private bool _held;

    public Edge Sample(bool down, long nowMs)
    {
        if (down)
        {
            _downSinceMs ??= nowMs;
            if (_held || nowMs - _downSinceMs < HoldMs) return Edge.None;
            _held = true;
            return Edge.Held;
        }
        bool pressed = _downSinceMs is not null && !_held;
        Reset();
        return pressed ? Edge.Pressed : Edge.None;
    }

    /// <summary>Forgets a press under way without reading it (the pad was disconnected).</summary>
    public void Reset()
    {
        _downSinceMs = null;
        _held = false;
    }
}
