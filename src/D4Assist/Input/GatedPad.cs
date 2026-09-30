namespace D4Assist.Input;

/// <summary>Passes D-pad left presses on only while <c>blocked</c> is false: inside a game menu the D-pad belongs to the game.</summary>
public sealed class GatedPad : IGuideTogglePad
{
    public event Action? DpadLeftPressed;

    /// <param name="onBlocked">Called for each press swallowed; null for none.</param>
    public GatedPad(IGuideTogglePad inner, Func<bool> blocked, Action? onBlocked = null)
    {
        inner.DpadLeftPressed += () =>
        {
            if (blocked()) onBlocked?.Invoke();
            else DpadLeftPressed?.Invoke();
        };
    }
}
