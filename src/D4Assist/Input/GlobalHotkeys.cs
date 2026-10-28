using System.Runtime.InteropServices;
using D4Assist.Game;

namespace D4Assist.Input;

/// <summary>
/// Ctrl+Alt+Shift hotkeys (Win32 RegisterHotKey) served by a private message-loop thread: the digits play the sound examples,
/// G toggles the guide like D-pad left and E switches its mode like holding it. A registered key goes to this app alone, so they are registered only while the game
/// window is in front (user, 2026-10-26: only the settings and exit keys work everywhere); the game never sees them.
/// </summary>
public sealed class GlobalHotkeys : IGuideTogglePad, IDisposable
{
    // winuser.h: MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_NOREPEAT; WM_HOTKEY, WM_QUIT, WM_APP; virtual keys '1' and 'G'.
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312, WmQuit = 0x0012, WmSetActive = 0x8001;
    private const uint FirstDigitKey = 0x31, GuideKey = 0x47, GuideModeKey = 0x45;
    private const int GuideHotkeyId = 100, GuideModeHotkeyId = 101; // past the digit ids 1-9

    // Checked at the player-poll rate, like the cue pause while the game is in the background.
    private const int ActivePollMs = GameSession.DefaultPlayerPollIntervalMs;

    private readonly int _digitCount;
    private readonly Func<bool> _active;
    private readonly ManualResetEventSlim _started = new();
    private readonly HashSet<int> _takenLogged = [];
    private Thread? _thread;
    private Timer? _poll;
    private uint _threadId;

    /// <summary>Raised on the hotkey thread with the digit pressed (1-based).</summary>
    public event Action<int>? DigitPressed;

    /// <summary>Raised on the hotkey thread for Ctrl+Alt+Shift+G: the keyboard's D-pad left.</summary>
    public event Action? GuideKeyPressed;

    event Action? IGuideTogglePad.DpadLeftPressed { add => GuideKeyPressed += value; remove => GuideKeyPressed -= value; }
    public event Action? GuideModeKeyPressed;
    event Action? IGuideTogglePad.DpadLeftHeld { add => GuideModeKeyPressed += value; remove => GuideModeKeyPressed -= value; }

    /// <param name="digitCount">Registers Ctrl+Alt+Shift+1 up to this digit (at most 9).</param>
    /// <param name="active">True while the keys belong to this app: the game window is in front (GameWindowFocus.IsGameForeground).</param>
    public GlobalHotkeys(int digitCount, Func<bool> active) => (_digitCount, _active) = (Math.Clamp(digitCount, 1, 9), active);

    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(Loop) { IsBackground = true, Name = "GlobalHotkeys" };
        _thread.Start();
        _started.Wait();
        _poll = new Timer(_ => PostThreadMessage(_threadId, WmSetActive, _active() ? 1 : 0, IntPtr.Zero), null, 0, ActivePollMs);
    }

    private void Loop()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // creates the thread's message queue before anyone posts to it
        _started.Set();
        bool active = false;
        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            switch (message.Message)
            {
                case WmSetActive when (message.WParam != 0) != active:
                    active = message.WParam != 0;
                    if (active) RegisterAll();
                    else UnregisterAll();
                    break;
                case WmHotkey when (int)message.WParam == GuideHotkeyId:
                    GuideKeyPressed?.Invoke();
                    break;
                case WmHotkey when (int)message.WParam == GuideModeHotkeyId:
                    GuideModeKeyPressed?.Invoke();
                    break;
                case WmHotkey:
                    DigitPressed?.Invoke((int)message.WParam);
                    break;
            }
        }
        UnregisterAll();
    }

    private void RegisterAll()
    {
        for (int digit = 1; digit <= _digitCount; digit++)
            Register(digit, FirstDigitKey + (uint)digit - 1, $"Ctrl+Alt+Shift+{digit} is already taken by another program; that sound example is unavailable.");
        Register(GuideHotkeyId, GuideKey, "Ctrl+Alt+Shift+G is already taken by another program; toggle the guide with D-pad left.");
        Register(GuideModeHotkeyId, GuideModeKey, "Ctrl+Alt+Shift+E is already taken by another program; switch the guide's mode by holding D-pad left.");
    }

    // Registered again each time the game comes to the front: a key another program holds is reported once.
    private void Register(int id, uint key, string taken)
    {
        if (!RegisterHotKey(IntPtr.Zero, id, ModControl | ModAlt | ModShift | ModNoRepeat, key) && _takenLogged.Add(id))
            Console.Error.WriteLine(taken);
    }

    private void UnregisterAll()
    {
        for (int digit = 1; digit <= _digitCount; digit++) UnregisterHotKey(IntPtr.Zero, digit);
        UnregisterHotKey(IntPtr.Zero, GuideHotkeyId);
        UnregisterHotKey(IntPtr.Zero, GuideModeHotkeyId);
    }

    public void Dispose()
    {
        _poll?.Dispose();
        if (_threadId != 0) PostThreadMessage(_threadId, WmQuit, 0, IntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Message;
        public nint WParam;
        public IntPtr LParam;
        public uint Time;
        public int PointX, PointY;
        public uint Private;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
