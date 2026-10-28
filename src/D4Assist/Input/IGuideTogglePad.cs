namespace D4Assist.Input;

/// <summary>D-pad left on any controller: once per short press, on its release (never while held), and once per hold of
/// <see cref="PressHold.HoldMs"/> (instead of the press).</summary>
public interface IGuideTogglePad
{
    event Action? DpadLeftPressed;

    /// <summary>D-pad left held down (the guide's mode, ExplorationGuideBridge); a source with no hold never raises it.</summary>
    event Action? DpadLeftHeld { add { } remove { } }
}
