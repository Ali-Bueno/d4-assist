using System.Runtime.InteropServices;

namespace D4Assist.Input;

/// <summary>The keys of the map's lists: Shift+F1 opens the waypoint list, Shift+F2 the events menu and Shift+F3 the treasure
/// goblins' while the map shows, the arrows, Enter and Escape work the open list.</summary>
public interface IMapListKeys
{
    /// <summary>Takes Shift+F1, Shift+F2 and Shift+F3 from the game while true; the game keeps them otherwise.</summary>
    void SetOpenKey(bool active);

    /// <summary>Takes up, down, Enter and Escape from the game while true (only while the list is open).</summary>
    void SetListKeys(bool active);

    event Action? OpenPressed;
    event Action? OpenEventsPressed;
    event Action? OpenGoblinsPressed;
    event Action? UpPressed;
    event Action? DownPressed;
    event Action? EnterPressed;
    event Action? EscapePressed;

    /// <summary>Once a run, an opening key another program holds (tester 2026-11-01: Shift+F2): the key, the one tried instead
    /// (Ctrl added), and whether that one is this app's now.</summary>
    event Action<string, string, bool>? OpenKeyTaken;
}

/// <summary>
/// <see cref="IMapListKeys"/> with Win32 RegisterHotKey on a private message-loop thread (as <see cref="GlobalHotkeys"/>).
/// A registered key goes to this app alone, so each is registered only while it means something here: Shift+F1 to Shift+F3
/// while the map is open and something listens to it, the list keys while a list is. An opening key another program holds is
/// taken with Ctrl added.
/// </summary>
public sealed class MapListKeys : IMapListKeys, IDisposable
{
    // winuser.h: MOD_CONTROL, MOD_SHIFT, MOD_NOREPEAT; WM_HOTKEY, WM_QUIT, WM_APP; VK_F1 to VK_F3, VK_UP, VK_DOWN, VK_RETURN, VK_ESCAPE.
    private const uint ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;
    private const uint WmHotkey = 0x0312, WmQuit = 0x0012, WmSetOpenKey = 0x8001, WmSetListKeys = 0x8002;
    private const uint VkF1 = 0x70, VkF2 = 0x71, VkF3 = 0x72, VkUp = 0x26, VkDown = 0x28, VkReturn = 0x0D, VkEscape = 0x1B;
    private const int OpenId = 200, UpId = 201, DownId = 202, EnterId = 203, EscapeId = 204, OpenEventsId = 205, OpenGoblinsId = 206;

    private static readonly (int Id, uint Key)[] ListKeys = [(UpId, VkUp), (DownId, VkDown), (EnterId, VkReturn), (EscapeId, VkEscape)];

    private readonly Action<string>? _log;
    private readonly ManualResetEventSlim _started = new();
    private Thread? _thread;
    private uint _threadId;

    public MapListKeys(Action<string>? log = null) => _log = log;

    public event Action? OpenPressed;
    public event Action? OpenEventsPressed;
    public event Action? OpenGoblinsPressed;
    public event Action? UpPressed;
    public event Action? DownPressed;
    public event Action? EnterPressed;
    public event Action? EscapePressed;
    public event Action<string, string, bool>? OpenKeyTaken;

    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(Loop) { IsBackground = true, Name = "MapListKeys" };
        _thread.Start();
        _started.Wait();
    }

    public void SetOpenKey(bool active) => Post(WmSetOpenKey, active);

    public void SetListKeys(bool active) => Post(WmSetListKeys, active);

    private void Post(uint message, bool active)
    {
        if (_threadId != 0) PostThreadMessage(_threadId, message, active ? 1 : 0, IntPtr.Zero);
    }

    private void Loop()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // creates the thread's message queue before anyone posts to it
        _started.Set();
        bool openKey = false, listKeys = false;
        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            switch (message.Message)
            {
                case WmSetOpenKey when (message.WParam != 0) != openKey:
                    openKey = message.WParam != 0;
                    if (openKey)
                    {
                        if (OpenPressed is not null) RegisterOpen(OpenId, VkF1, "Shift+F1");
                        if (OpenEventsPressed is not null) RegisterOpen(OpenEventsId, VkF2, "Shift+F2");
                        if (OpenGoblinsPressed is not null) RegisterOpen(OpenGoblinsId, VkF3, "Shift+F3");
                    }
                    else
                    {
                        UnregisterHotKey(IntPtr.Zero, OpenId);
                        UnregisterHotKey(IntPtr.Zero, OpenEventsId);
                        UnregisterHotKey(IntPtr.Zero, OpenGoblinsId);
                    }
                    break;
                case WmSetListKeys when (message.WParam != 0) != listKeys:
                    listKeys = message.WParam != 0;
                    foreach (var (id, key) in ListKeys)
                        if (listKeys) Register(id, 0, key, $"key 0x{key:X2}");
                        else UnregisterHotKey(IntPtr.Zero, id);
                    break;
                case WmHotkey:
                    Raise((int)message.WParam);
                    break;
            }
        }
        UnregisterHotKey(IntPtr.Zero, OpenId);
        UnregisterHotKey(IntPtr.Zero, OpenEventsId);
        UnregisterHotKey(IntPtr.Zero, OpenGoblinsId);
        foreach (var (id, _) in ListKeys) UnregisterHotKey(IntPtr.Zero, id);
    }

    // Keys found taken: said once a run, not at every opening of the map (tester 2026-11-01: Shift+F2, 14 times).
    private readonly HashSet<string> _taken = [];

    private void Register(int id, uint modifiers, uint key, string name)
    {
        if (!RegisterHotKey(IntPtr.Zero, id, modifiers, key) && _taken.Add(name)) _log?.Invoke($"WAYPOINTS {name} is taken by another program.");
    }

    // Unregistered by id whichever of the two was taken.
    private void RegisterOpen(int id, uint key, string name)
    {
        if (RegisterHotKey(IntPtr.Zero, id, ModShift | ModNoRepeat, key)) return;
        string instead = "Ctrl+" + name;
        bool taken = RegisterHotKey(IntPtr.Zero, id, ModControl | ModShift | ModNoRepeat, key);
        if (!_taken.Add(name)) return;
        _log?.Invoke($"WAYPOINTS {name} is taken by another program; {instead} {(taken ? "instead" : "is taken too")}.");
        OpenKeyTaken?.Invoke(name, instead, taken);
    }

    private void Raise(int id)
    {
        var handler = id switch
        {
            OpenId => OpenPressed,
            OpenEventsId => OpenEventsPressed,
            OpenGoblinsId => OpenGoblinsPressed,
            UpId => UpPressed,
            DownId => DownPressed,
            EnterId => EnterPressed,
            EscapeId => EscapePressed,
            _ => null,
        };
        // A handler reads the game; one that throws must not end the app on this thread.
        try { handler?.Invoke(); }
        catch (Exception e) { _log?.Invoke($"WAYPOINTS key {id} failed: {e}"); }
    }

    public void Dispose()
    {
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
