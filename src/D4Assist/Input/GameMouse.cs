using System.Diagnostics;
using System.Runtime.InteropServices;
using D4Memory.Safety;

namespace D4Assist.Input;

/// <summary>The mouse cursor over the game window: its client size, and a move to a client pixel. Never clicks.</summary>
public interface IGameMouse
{
    /// <summary>The game window's client size in pixels; null while there is no game window.</summary>
    (int Width, int Height)? ClientSize();

    /// <summary>Moves the cursor to a client pixel; false when the game window is gone or not in the foreground.</summary>
    bool MoveTo(int clientX, int clientY);
}

/// <summary>
/// <see cref="IGameMouse"/> with SendInput: the game ignores SetCursorPos, and takes a SendInput move only while its window is in
/// the foreground (research/client-memory.md §4, world map view). The first input the app ever sends to the game: the user
/// allowed moving the mouse for the waypoint list (2026-10-18), nothing else.
/// </summary>
public sealed class GameMouse : IGameMouse
{
    // winuser.h: INPUT_MOUSE; MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK; SM_[XY]VIRTUALSCREEN, SM_C[XY]VIRTUALSCREEN.
    private const uint InputMouse = 0, MoveAbsolute = 0x0001 | 0x8000 | 0x4000;
    private const int SmXVirtual = 76, SmYVirtual = 77, SmCxVirtual = 78, SmCyVirtual = 79;
    private const int AbsoluteRange = 65535;

    public (int Width, int Height)? ClientSize() =>
        GameWindow() is { } window && GetClientRect(window, out var rect) && rect.Right > 0 && rect.Bottom > 0 ? (rect.Right, rect.Bottom) : null;

    public bool MoveTo(int clientX, int clientY)
    {
        if (GameWindow() is not { } window || GetForegroundWindow() != window) return false;
        var point = new Point { X = clientX, Y = clientY };
        if (!ClientToScreen(window, ref point)) return false;
        int left = GetSystemMetrics(SmXVirtual), top = GetSystemMetrics(SmYVirtual);
        int width = GetSystemMetrics(SmCxVirtual), height = GetSystemMetrics(SmCyVirtual);
        if (width <= 1 || height <= 1) return false;
        var input = new Input
        {
            Type = InputMouse,
            Mouse = new MouseInput
            {
                X = (int)Math.Round((point.X - left) * (double)AbsoluteRange / (width - 1)),
                Y = (int)Math.Round((point.Y - top) * (double)AbsoluteRange / (height - 1)),
                Flags = MoveAbsolute,
            },
        };
        return SendInput(1, [input], Marshal.SizeOf<Input>()) == 1;
    }

    private static IntPtr? GameWindow()
    {
        foreach (var process in Process.GetProcessesByName(ProcessLocator.ProcessName))
            using (process)
                if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
        return null;
    }

    private struct Rect { public int Left, Top, Right, Bottom; }

    private struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public IntPtr ExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    private struct Input { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public MouseInput Mouse; }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr window, ref Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
