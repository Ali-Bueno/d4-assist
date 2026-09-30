using System.Windows.Forms;

namespace D4Assist.Ui;

/// <summary>The settings sliders' keys (user request 2026-09-30): up/down 1, right/left 5, page up/down 10, Home to the top, End to the bottom.</summary>
public static class SliderKeys
{
    public const int LineStep = 1, SideStep = 5, PageStep = 10;

    /// <summary>The value a key moves the slider to, clamped to the range; null when the key isn't a slider key.</summary>
    public static int? Next(Keys key, int value, int minimum, int maximum)
    {
        int? target = key switch
        {
            Keys.Up => value + LineStep,
            Keys.Down => value - LineStep,
            Keys.Right => value + SideStep,
            Keys.Left => value - SideStep,
            Keys.PageUp => value + PageStep,
            Keys.PageDown => value - PageStep,
            Keys.Home => maximum,
            Keys.End => minimum,
            _ => null,
        };
        return target is { } t ? Math.Clamp(t, minimum, maximum) : null;
    }
}

/// <summary>A standard trackbar that moves by <see cref="SliderKeys"/> instead of the native steps and reads its own number.</summary>
public sealed class CueSlider : TrackBar
{
    // commctrl.h: TBM_GETRANGEMIN, TBM_GETRANGEMAX (WM_USER + 1, + 2).
    private const int GetRangeMinMessage = 0x0401, GetRangeMaxMessage = 0x0402;
    // The MSAA trackbar proxy (which WinForms' UIA value also comes from) reads the position as a percentage of the range
    // it asks for; WinForms keeps the range itself and never asks, so answering 0-100 makes it read the position as is.
    private const int ReportedMinimum = 0, ReportedMaximum = 100;

    public CueSlider(int minimum, int maximum, int tickFrequency)
    {
        (Minimum, Maximum, TickFrequency) = (minimum, maximum, tickFrequency);
        (SmallChange, LargeChange) = (SliderKeys.LineStep, SliderKeys.PageStep);
        AutoSize = true;
        QueryAccessibilityHelp += (_, e) => e.HelpString ??= AccessibleDescription; // the description reaches UIA only as help
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == GetRangeMinMessage) m.Result = ReportedMinimum;
        else if (m.Msg == GetRangeMaxMessage) m.Result = ReportedMaximum;
        else base.WndProc(ref m);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Modifiers == Keys.None && SliderKeys.Next(e.KeyCode, Value, Minimum, Maximum) is { } next)
        {
            Value = next; // raises ValueChanged and the UIA value event, like a native move
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1); // for MSAA clients, and at the ends where the value stays
            e.Handled = e.SuppressKeyPress = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
