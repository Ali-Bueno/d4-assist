using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace D4Assist.Ui;

/// <summary>A standard check box whose screen reader says "checked" or "not checked" when Space toggles it.</summary>
public sealed class AccessibleCheckBox : CheckBox
{
    // winuser.h: WM_GETOBJECT, EVENT_OBJECT_STATECHANGE, OBJID_CLIENT, CHILDID_SELF; uiautomationcoreapi.h: UiaRootObjectId.
    private const int GetObjectMessage = 0x003D, UiaRootObjectId = -25, ClientObjectId = -4, SelfChildId = 0;
    private const uint StateChangeEvent = 0x800A;

    // WinForms raises neither a UIA toggle event nor an MSAA state event, so readers are sent to the MSAA object and this
    // box raises the event itself (reference/ui-accessibility/companion-app-winforms.md).
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == GetObjectMessage && (int)m.LParam == UiaRootObjectId) m.Result = 0;
        else base.WndProc(ref m);
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        if (IsHandleCreated) NotifyWinEvent(StateChangeEvent, Handle, ClientObjectId, SelfChildId);
    }

    [DllImport("user32.dll")]
    private static extern void NotifyWinEvent(uint winEvent, IntPtr window, int objectId, int childId);
}
