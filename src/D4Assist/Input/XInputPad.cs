using System.Runtime.InteropServices;

namespace D4Assist.Input;

/// <summary>
/// Read-only observer of the four XInput slots, raising <see cref="DpadLeftPressed"/> as a short press of D-pad left comes back
/// up and <see cref="DpadLeftHeld"/> once it is held (PressHold).
/// Ported from the D2R mod's input_direction.cpp (dynamic load :108-130, per-slot poll :133-146, edge :99-104,
/// free on shutdown :336-345); it only calls XInputGetState, so the game's own pad state is untouched.
/// </summary>
public sealed class XInputPad : IGuideTogglePad, IDisposable
{
    // Windows XInput header: XINPUT_GAMEPAD_DPAD_LEFT.
    public const ushort DpadLeftMask = 0x0004;
    // Windows XInput header: XUSER_MAX_COUNT.
    public const int MaxUsers = 4;
    // One UI frame at 60 Hz; D2R polls once per UI tick (plugin.cpp:83) with no fixed rate.
    public const int PollIntervalMs = 16;
    private const int ErrorSuccess = 0;
    // Same order as D2R (input_direction.cpp:117).
    private static readonly string[] LibraryCandidates = ["xinput1_4.dll", "xinput9_1_0.dll"];

    private delegate int XInputGetStateFn(uint userIndex, out XInputState state);

    // XINPUT_STATE: dwPacketNumber, then XINPUT_GAMEPAD (wButtons, 2 triggers, 4 thumb axes).
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
    }

    private readonly Func<int, ushort?> _readButtons;
    private readonly Action<string>? _log;
    private nint _library;
    private readonly PressHold[] _buttons = [.. Enumerable.Range(0, MaxUsers).Select(_ => new PressHold())];
    private readonly Func<long> _nowMs = () => Environment.TickCount64;
    private readonly bool[] _connected = new bool[MaxUsers];
    private Timer? _timer;

    public event Action? DpadLeftPressed;
    public event Action? DpadLeftHeld;

    /// <summary>Production: loads XInput from the system; if it is unavailable the pad never fires.</summary>
    /// <param name="log">The library loaded and each slot's connect and disconnect (a few lines per session).</param>
    public XInputPad(Action<string>? log = null)
    {
        _log = log;
        _readButtons = _ => null;
        foreach (var name in LibraryCandidates)
        {
            if (!NativeLibrary.TryLoad(name, out var library)) continue;
            if (!NativeLibrary.TryGetExport(library, "XInputGetState", out var export)) { NativeLibrary.Free(library); continue; }
            var getState = Marshal.GetDelegateForFunctionPointer<XInputGetStateFn>(export);
            _library = library;
            _readButtons = slot => getState((uint)slot, out var state) == ErrorSuccess ? state.Buttons : null;
            log?.Invoke($"Pad: XInput loaded from {name}.");
            return;
        }
        log?.Invoke("Pad: XInput unavailable; Xbox pads can't toggle the guide.");
    }

    /// <summary>Tests: <paramref name="readButtons"/> returns a slot's button bits, or null when it is disconnected.</summary>
    /// <param name="nowMs">Milliseconds clock; null for the system's.</param>
    public XInputPad(Func<int, ushort?> readButtons, Action<string>? log = null, Func<long>? nowMs = null)
    {
        (_readButtons, _log) = (readButtons, log);
        if (nowMs is not null) _nowMs = nowMs;
    }

    public bool IsAvailable => _library != 0;

    public void Start() => _timer ??= new Timer(_ => PollOnce(), null, 0, PollIntervalMs);

    /// <summary>Samples every slot once and raises an event for each slot whose D-pad left was just pressed or held.</summary>
    public void PollOnce()
    {
        for (int slot = 0; slot < MaxUsers; slot++)
        {
            var buttons = _readButtons(slot);
            if (buttons.HasValue != _connected[slot])
            {
                _connected[slot] = buttons.HasValue;
                _log?.Invoke($"Pad: XInput slot {slot} {(buttons.HasValue ? "connected" : "disconnected")}.");
            }
            if (buttons is not { } b)
            {
                _buttons[slot].Reset(); // a disconnected slot clears, so a reconnect starts from a clean edge
                continue;
            }
            var edge = _buttons[slot].Sample((b & DpadLeftMask) != 0, _nowMs());
            if (edge == PressHold.Edge.Pressed) DpadLeftPressed?.Invoke();
            else if (edge == PressHold.Edge.Held) DpadLeftHeld?.Invoke();
        }
    }

    // A poll can take longer than this only when XInput itself hangs.
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);

    public void Dispose()
    {
        bool idle = true;
        if (_timer is { } timer)
        {
            _timer = null;
            // Freeing XInput under a poll still inside XInputGetState is an access violation: wait for it to return first.
            var pollsDone = new ManualResetEvent(false);
            idle = !timer.Dispose(pollsDone) || pollsDone.WaitOne(StopTimeout);
            if (idle) pollsDone.Dispose(); // else the timer still signals it later
        }
        // A poll that never returned keeps the library: the process exit unloads it.
        if (idle && _library != 0) { NativeLibrary.Free(_library); _library = 0; }
    }
}
