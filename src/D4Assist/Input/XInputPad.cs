using System.Runtime.InteropServices;

namespace D4Assist.Input;

/// <summary>
/// Read-only observer of the four XInput slots, raising <see cref="DpadLeftPressed"/> on the press edge.
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
    private readonly nint _library;
    private readonly bool[] _held = new bool[MaxUsers];
    private Timer? _timer;

    public event Action? DpadLeftPressed;

    /// <summary>Production: loads XInput from the system; if it is unavailable the pad never fires.</summary>
    public XInputPad()
    {
        _readButtons = _ => null;
        foreach (var name in LibraryCandidates)
        {
            if (!NativeLibrary.TryLoad(name, out var library)) continue;
            if (!NativeLibrary.TryGetExport(library, "XInputGetState", out var export)) { NativeLibrary.Free(library); continue; }
            var getState = Marshal.GetDelegateForFunctionPointer<XInputGetStateFn>(export);
            _library = library;
            _readButtons = slot => getState((uint)slot, out var state) == ErrorSuccess ? state.Buttons : null;
            return;
        }
    }

    /// <summary>Tests: <paramref name="readButtons"/> returns a slot's button bits, or null when it is disconnected.</summary>
    public XInputPad(Func<int, ushort?> readButtons) => _readButtons = readButtons;

    public bool IsAvailable => _library != 0;

    public void Start() => _timer ??= new Timer(_ => PollOnce(), null, 0, PollIntervalMs);

    /// <summary>Samples every slot once and raises the event for each slot whose D-pad left just went down.</summary>
    public void PollOnce()
    {
        for (int slot = 0; slot < MaxUsers; slot++)
        {
            var buttons = _readButtons(slot);
            bool down = buttons is { } b && (b & DpadLeftMask) != 0;
            bool edge = down && !_held[slot];
            _held[slot] = down; // a disconnected slot clears, so a reconnect starts from a clean edge
            if (edge) DpadLeftPressed?.Invoke();
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
        if (_library != 0) NativeLibrary.Free(_library);
    }
}
