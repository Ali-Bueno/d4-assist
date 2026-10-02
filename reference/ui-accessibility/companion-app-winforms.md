# Companion-app settings windows (WinForms)

For external companion apps (no in-game UI to hook) that need their own window, e.g. a settings dialog. First used by
Diablo IV Dungeon Assist (`src/D4Assist/Ui/`, 2026-09-30); verified with NVDA by the (blind) user and with UI Automation.

## Why WinForms

Its controls are the native Win32 common controls (check box, button, tab, trackbar, combo box, group box), which NVDA
and JAWS read with no extra work. .NET 10: `<UseWindowsForms>true</UseWindowsForms>`, `OutputType` `WinExe` to drop the
console window. Remove the implicit `System.Windows.Forms`/`System.Drawing` global usings if the project has its own
`Timer`, `Message` or `Point` names.

## Rules that held

- Give every trackbar and combo box an `AccessibleName` (the label text is not picked up); check boxes and buttons use
  their `Text`.
- Group related rows in a `GroupBox`: the screen reader says the group's name when focus enters it.
- A non-modal window opened from a tray icon or a global hotkey (`RegisterHotKey`) can take the foreground with
  `Activate()`; run the tray (`NotifyIcon`, `ContextMenuStrip`) on its own STA thread with `Application.Run`.
- `NotifyIcon` only opens its menu on a right click; Enter on the tray icon arrives as a left click. Open the same menu
  there (its private `ShowContextMenu` also brings the menu to the foreground), and select the first item on `Opened`.
- Custom slider keys: handle them in `OnKeyDown` and set `e.Handled`; setting `Value` raises the same UIA value event as a
  native move.

## Trap: a trackbar's value is read as a percentage

The value a screen reader reads (MSAA, and WinForms' UIA Value pattern, which delegates to the same oleacc proxy) is the
position as a percentage of the range the proxy queries with `TBM_GETRANGEMIN`/`TBM_GETRANGEMAX`: a 25-300 slider at
100 reads "27". Only a 0-100 range reads its own number. Fixes tried:
- A custom `AccessibleObject` with its own `Value`: the UIA Value pattern disappears (WinForms enables it only in its
  internal `TrackBarAccessibleObject`), so nothing is read at all.
- MSAA dynamic annotation (`IAccPropServices.SetHwndPropStr`, `PROPID_ACC_VALUE`): ignored, because WinForms creates the
  proxy in-process and the annotation wrapper applies only through `AccessibleObjectFromWindow`.
- **Works:** answer `TBM_GETRANGEMIN` with 0 and `TBM_GETRANGEMAX` with 100 in the trackbar's `WndProc`. WinForms keeps
  the range itself and never sends those messages, and the proxy does not clamp, so it reads the position as is (25, 100,
  300).

## Trap: a check box's new state is not announced

Space toggles a WinForms check box, but NVDA says nothing; the new state is only heard when focus comes back
(user report, 2026-10-02). WinForms on .NET 10 (checked in 10.0.12, `CheckBox.NotifyAccessibilityStateChanged`):
- UIA: it raises only a Name property change, never the Toggle pattern's `ToggleState` change. NVDA reads WinForms
  controls through UIA (`UiaHasServerSideProvider` is true) and announces a toggle only on that event.
- MSAA: `Control.AccessibilityNotifyClients` sends the `NotifyWinEvent` only when the AppContext switch
  `Switch.System.Windows.Forms.AccessibleObject.NoClientNotifications` is **true** (default false), so no
  `EVENT_OBJECT_STATECHANGE` either. This silences every `AccessibilityNotifyClients` call, not only the check box's.
- NVDA ignores MSAA events from a window it reads through UIA (its `IAccessibleHandler`), so the MSAA event alone is not enough.

**Works** (`Ui/AccessibleCheckBox`): answer `WM_GETOBJECT` with `UiaRootObjectId` (-25) with 0, so readers take the
check box's MSAA object (WinForms' own `IAccessible`: role check button, state checked), and call
`NotifyWinEvent(EVENT_OBJECT_STATECHANGE, hwnd, OBJID_CLIENT, CHILDID_SELF)` in `OnCheckedChanged`. Verified with a
probe (a WinEvent hook and a UIA client): the WinEvent arrives with the checked state, and UIA's MSAA proxy turns it into a
`ToggleState` change for UIA clients (Narrator). Without the `WM_GETOBJECT` answer, UIA clients still get no toggle event.
Not yet confirmed by ear with NVDA.
