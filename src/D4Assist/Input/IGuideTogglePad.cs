namespace D4Assist.Input;

/// <summary>Raises once per D-pad left press on any controller (edge-triggered, never while held).</summary>
public interface IGuideTogglePad
{
    event Action? DpadLeftPressed;
}
