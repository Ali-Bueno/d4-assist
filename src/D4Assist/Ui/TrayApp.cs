using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using D4Assist.Strings;

namespace D4Assist.Ui;

/// <summary>
/// The notification-area icon (settings, check for updates, restart, exit), the Ctrl+Alt+Shift+C settings hotkey and the
/// Ctrl+Alt+Shift+Q exit hotkey, on their own STA
/// thread and message loop. Enter or a click on the icon opens the same menu as the context-menu key.
/// </summary>
public sealed class TrayApp : IDisposable
{
    // winuser.h: MOD_ALT | MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, WM_HOTKEY; 'C' for "configuración"/"settings", 'Q' for quit.
    private const uint HotkeyModifiers = 0x1 | 0x2 | 0x4 | 0x4000;
    private const int WmHotkey = 0x0312, SettingsHotkeyId = 1, ExitHotkeyId = 2;
    private const Keys SettingsKey = Keys.C, ExitKey = Keys.Q;
    private const int IconSize = 32;

    private readonly SettingsStrings _text;
    private readonly Func<SettingsForm> _createSettings;
    private readonly ManualResetEventSlim _started = new();
    private Thread? _thread;
    private ApplicationContext? _context;
    private HotkeyWindow? _hotkeys;
    private NotifyIcon? _icon;
    private SettingsForm? _settings;

    /// <summary>Raised on the tray thread when the user picks Exit or presses Ctrl+Alt+Shift+Q.</summary>
    public event Action? ExitRequested;

    /// <summary>Raised on the tray thread when the user picks Restart, or asks the settings window to restart.</summary>
    public event Action? RestartRequested;

    /// <summary>Raised on the tray thread when the user picks Check for updates.</summary>
    public event Action? CheckUpdatesRequested;

    /// <param name="createSettings">Builds a settings window; called on the tray thread each time one opens.</param>
    public TrayApp(SettingsStrings text, Func<SettingsForm> createSettings)
    {
        _text = text;
        _createSettings = createSettings;
    }

    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(Run) { IsBackground = true, Name = "Tray" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _started.Wait();
    }

    private void Run()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.ThreadException += (_, e) => Console.Error.WriteLine($"Settings window error: {e.Exception}");

        _context = new ApplicationContext();
        var menu = new ContextMenuStrip();
        menu.Items.Add(_text.MenuSettings, null, (_, _) => ShowSettings());
        menu.Items.Add(_text.MenuCheckUpdates, null, (_, _) => CheckUpdatesRequested?.Invoke());
        menu.Items.Add(_text.MenuRestart, null, (_, _) => RestartRequested?.Invoke());
        menu.Items.Add(_text.MenuExit, null, (_, _) => ExitRequested?.Invoke());
        menu.Opened += (_, _) => menu.Items[0].Select(); // the screen reader lands on an item, not on an empty menu

        _icon = new NotifyIcon { Icon = CreateIcon(), Text = _text.AppName, ContextMenuStrip = menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowMenu(); };
        _hotkeys = new HotkeyWindow(ShowSettings, () => ExitRequested?.Invoke());

        _started.Set();
        Application.Run(_context);

        _hotkeys.Dispose();
        _icon.Visible = false; // otherwise the icon lingers until the mouse passes over it
        _icon.Dispose();
    }

    private void ShowSettings()
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.Activate();
            return;
        }
        _settings = _createSettings();
        _settings.RestartRequested += () => RestartRequested?.Invoke();
        _settings.FormClosed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    // NotifyIcon only opens its menu on a right click; its private ShowContextMenu also brings the menu to the foreground.
    private void ShowMenu()
    {
        var show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        if (show is not null) show.Invoke(_icon, null);
        else _icon?.ContextMenuStrip?.Show(Cursor.Position);
    }

    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(IconSize, IconSize);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font(FontFamily.GenericSansSerif, IconSize / 2.5f, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            graphics.Clear(Color.Transparent);
            graphics.FillEllipse(Brushes.DarkRed, 0, 0, IconSize - 1, IconSize - 1);
            var centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString("D4", font, Brushes.White, new RectangleF(0, 0, IconSize, IconSize), centered);
        }
        IntPtr handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    /// <summary>Closes the menu and any settings window and ends the tray thread.</summary>
    public void Dispose()
    {
        if (_context is null || _hotkeys is null || _thread is not { IsAlive: true }) return;
        try { _hotkeys.Invoke(() => { _settings?.Close(); _context.ExitThread(); }); }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException) { } // the loop already ended
        _thread?.Join(TimeSpan.FromSeconds(2));
    }

    /// <summary>A message-only window that receives the settings and exit hotkeys and runs work on the tray thread.</summary>
    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private readonly Action _onSettings, _onExit;
        private readonly SynchronizationContext _loop = new WindowsFormsSynchronizationContext();

        public HotkeyWindow(Action onSettings, Action onExit)
        {
            _onSettings = onSettings;
            _onExit = onExit;
            CreateHandle(new CreateParams());
            if (!RegisterHotKey(Handle, SettingsHotkeyId, HotkeyModifiers, (uint)SettingsKey))
                Console.Error.WriteLine("Ctrl+Alt+Shift+C is already taken by another program; open the settings from the notification area.");
            if (!RegisterHotKey(Handle, ExitHotkeyId, HotkeyModifiers, (uint)ExitKey))
                Console.Error.WriteLine("Ctrl+Alt+Shift+Q is already taken by another program; exit from the notification area.");
        }

        public void Invoke(Action work) => _loop.Send(_ => work(), null);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam == SettingsHotkeyId) _onSettings();
            else if (m.Msg == WmHotkey && m.WParam == ExitHotkeyId) _onExit();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle == IntPtr.Zero) return;
            UnregisterHotKey(Handle, SettingsHotkeyId);
            UnregisterHotKey(Handle, ExitHotkeyId);
            DestroyHandle();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);
}
