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
    // Twice in the shortest tap (about 60 ms): the pad only toggles the guide, and each poll wakes a thread.
    public const int PollIntervalMs = 32;
    // Asking an empty slot is the slow path of XInputGetState: Microsoft's advice is to look for new pads every few seconds.
    public const long EmptySlotCheckMs = 2000;
    private const int ErrorSuccess = 0;
    // Same order as D2R (input_direction.cpp:117).
    private static readonly string[] LibraryCandidates = ["xinput1_4.dll", "xinput9_1_0.dll"];

    // The undocumented XInputGetCapabilitiesEx of xinput1_4 (export ordinal 108, as SDL reads it): the pad's USB vendor and product.
    private const int CapabilitiesExOrdinal = 108;

    private delegate int XInputGetStateFn(uint userIndex, out XInputState state);
    private delegate int XInputGetCapabilitiesExFn(uint one, uint userIndex, uint flags, out XInputCapabilitiesEx capabilities);

    // XINPUT_STATE: dwPacketNumber, then XINPUT_GAMEPAD (wButtons, 2 triggers, 4 thumb axes).
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
    }

    // XINPUT_CAPABILITIES (type, subtype, flags, XINPUT_GAMEPAD, XINPUT_VIBRATION), then vendor, product, version and two unknowns.
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputCapabilitiesEx
    {
        public byte Type, SubType;
        public ushort Flags;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short ThumbLX, ThumbLY, ThumbRX, ThumbRY;
        public ushort LeftMotor, RightMotor;
        public ushort VendorId, ProductId, VersionNumber, Unknown1;
        public uint Unknown2;
    }

    /// <summary>One slot's sample: the buttons, and the packet number XInput moves on with any change of the pad's state.</summary>
    private readonly record struct Reading(ushort Buttons, uint Packet);

    private readonly Func<int, Reading?> _read;
    private readonly Func<int, string?> _describe = _ => null;
    private readonly Action<string>? _log;
    private nint _library;
    private readonly PressHold[] _buttons = [.. Enumerable.Range(0, MaxUsers).Select(_ => new PressHold())];
    private readonly Func<long> _nowMs = () => Environment.TickCount64;
    private long _nextEmptyCheck;
    private readonly bool[] _connected = new bool[MaxUsers];
    // Logged once per connection: a pad that connects but never reports a button (seen on a ROG Xbox Ally) is told from one that
    // works, and one whose sticks move from one that sends nothing at all.
    private readonly bool[] _buttonsSeen = new bool[MaxUsers];
    private readonly bool[] _inputSeen = new bool[MaxUsers];
    private readonly uint[] _firstPacket = new uint[MaxUsers];
    private Timer? _timer;

    public event Action? DpadLeftPressed;
    public event Action? DpadLeftHeld;

    /// <summary>Production: loads XInput from the system; if it is unavailable the pad never fires.</summary>
    /// <param name="log">The library loaded and each slot's connect and disconnect (a few lines per session).</param>
    public XInputPad(Action<string>? log = null)
    {
        _log = log;
        _read = _ => null;
        foreach (var name in LibraryCandidates)
        {
            if (!NativeLibrary.TryLoad(name, out var library)) continue;
            if (!NativeLibrary.TryGetExport(library, "XInputGetState", out var export)) { NativeLibrary.Free(library); continue; }
            var getState = Marshal.GetDelegateForFunctionPointer<XInputGetStateFn>(export);
            _library = library;
            _read = slot => getState((uint)slot, out var state) == ErrorSuccess ? new Reading(state.Buttons, state.PacketNumber) : null;
            var capabilities = GetProcAddress(library, CapabilitiesExOrdinal);
            if (capabilities != 0)
            {
                var getCapabilities = Marshal.GetDelegateForFunctionPointer<XInputGetCapabilitiesExFn>(capabilities);
                _describe = slot => getCapabilities(1, (uint)slot, 0, out var c) == ErrorSuccess
                    ? $"type {c.Type}, subtype {c.SubType}, flags 0x{c.Flags:X4}, vendor 0x{c.VendorId:X4}, product 0x{c.ProductId:X4}" : null;
            }
            log?.Invoke($"Pad: XInput loaded from {name}.");
            return;
        }
        log?.Invoke("Pad: XInput unavailable; Xbox pads can't toggle the guide.");
    }

    /// <summary>Tests: <paramref name="readButtons"/> returns a slot's button bits, or null when it is disconnected; its state
    /// changes with the buttons only.</summary>
    /// <param name="nowMs">Milliseconds clock; null for the system's.</param>
    public XInputPad(Func<int, ushort?> readButtons, Action<string>? log = null, Func<long>? nowMs = null)
    {
        _read = slot => readButtons(slot) is { } b ? new Reading(b, b) : null;
        _log = log;
        if (nowMs is not null) _nowMs = nowMs;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nint GetProcAddress(nint module, nint ordinal);

    public bool IsAvailable => _library != 0;

    // A timer tick runs while the last one still polls when a slot is slow to answer (seen at connect): PressHold and the per-slot
    // state take one poll at a time.
    public void Start() => _timer ??= new Timer(_ => PollAlone(), null, 0, PollIntervalMs);

    private int _polling;

    private void PollAlone()
    {
        if (Interlocked.Exchange(ref _polling, 1) == 1) return;
        try { PollOnce(); }
        finally { Interlocked.Exchange(ref _polling, 0); }
    }

    /// <summary>Samples every slot once and raises an event for each slot whose D-pad left was just pressed or held.</summary>
    public void PollOnce()
    {
        long now = _nowMs();
        bool checkEmpty = now >= _nextEmptyCheck;
        if (checkEmpty) _nextEmptyCheck = now + EmptySlotCheckMs;
        for (int slot = 0; slot < MaxUsers; slot++)
        {
            if (!_connected[slot] && !checkEmpty) continue;
            var reading = _read(slot);
            if (reading.HasValue != _connected[slot])
            {
                _connected[slot] = reading.HasValue;
                (_buttonsSeen[slot], _inputSeen[slot]) = (false, false);
                string? device = null;
                if (reading is { } first) (_firstPacket[slot], device) = (first.Packet, _describe(slot));
                _log?.Invoke($"Pad: XInput slot {slot} {(reading.HasValue ? "connected" : "disconnected")}{(device is null ? "" : $" ({device})")}.");
            }
            if (reading is not { } r)
            {
                _buttons[slot].Reset(); // a disconnected slot clears, so a reconnect starts from a clean edge
                continue;
            }
            ushort b = r.Buttons;
            if (r.Packet != _firstPacket[slot] && !_inputSeen[slot])
            {
                _inputSeen[slot] = true;
                _log?.Invoke($"Pad: XInput slot {slot} first input.");
            }
            if (b != 0 && !_buttonsSeen[slot])
            {
                _buttonsSeen[slot] = true;
                _log?.Invoke($"Pad: XInput slot {slot} first buttons 0x{b:X4}.");
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
