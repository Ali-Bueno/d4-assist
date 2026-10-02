using System.Runtime.InteropServices;

namespace D4Assist.Input;

/// <summary>
/// System-wide Ctrl+Alt+Shift hotkeys (Win32 RegisterHotKey) served by a private message-loop thread: the digits play the
/// sound examples, G toggles the guide like D-pad left. Windows delivers them to this app only; the game never sees them.
/// </summary>
public sealed class GlobalHotkeys : IGuideTogglePad, IDisposable
{
    // winuser.h: MOD_ALT, MOD_CONTROL, MOD_SHIFT, MOD_NOREPEAT; WM_HOTKEY, WM_QUIT; virtual keys '1' and 'G'.
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312, WmQuit = 0x0012;
    private const uint FirstDigitKey = 0x31, GuideKey = 0x47;
    private const int GuideHotkeyId = 100; // past the digit ids 1-9

    private readonly int _digitCount;
    private readonly ManualResetEventSlim _started = new();
    private Thread? _thread;
    private uint _threadId;

    /// <summary>Raised on the hotkey thread with the digit pressed (1-based).</summary>
    public event Action<int>? DigitPressed;

    /// <summary>Raised on the hotkey thread for Ctrl+Alt+Shift+G: the keyboard's D-pad left.</summary>
    public event Action? GuideKeyPressed;

    event Action? IGuideTogglePad.DpadLeftPressed { add => GuideKeyPressed += value; remove => GuideKeyPressed -= value; }

    /// <param name="digitCount">Registers Ctrl+Alt+Shift+1 up to this digit (at most 9).</param>
    public GlobalHotkeys(int digitCount) => _digitCount = Math.Clamp(digitCount, 1, 9);

    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(Loop) { IsBackground = true, Name = "GlobalHotkeys" };
        _thread.Start();
        _started.Wait();
    }

    private void Loop()
    {
        _threadId = GetCurrentThreadId();
        var registered = new List<int>();
        for (int digit = 1; digit <= _digitCount; digit++)
        {
            if (RegisterHotKey(IntPtr.Zero, digit, ModControl | ModAlt | ModShift | ModNoRepeat, FirstDigitKey + (uint)digit - 1))
                registered.Add(digit);
            else
                Console.Error.WriteLine($"Ctrl+Alt+Shift+{digit} is already taken by another program; that sound example is unavailable.");
        }
        if (RegisterHotKey(IntPtr.Zero, GuideHotkeyId, ModControl | ModAlt | ModShift | ModNoRepeat, GuideKey))
            registered.Add(GuideHotkeyId);
        else
            Console.Error.WriteLine("Ctrl+Alt+Shift+G is already taken by another program; toggle the guide with D-pad left.");
        _started.Set();

        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.Message != WmHotkey) continue;
            if ((int)message.WParam == GuideHotkeyId) GuideKeyPressed?.Invoke();
            else DigitPressed?.Invoke((int)message.WParam);
        }

        foreach (var id in registered) UnregisterHotKey(IntPtr.Zero, id);
    }

    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Message;
        public IntPtr WParam;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
