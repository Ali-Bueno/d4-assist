using System.Diagnostics;
using System.Runtime.InteropServices;
using D4Assist.Game;
using D4Memory.Safety;

namespace D4Assist.Input;

/// <summary>Mutes the cues while the game window is not in the foreground, like d2r-access (src/radar_ui.cpp, Inactive).</summary>
public sealed class GameWindowFocus : IDisposable
{
    // Checked at the player-poll rate, so cues stop as fast as they would follow a move.
    private const int PollIntervalMs = GameSession.DefaultPlayerPollIntervalMs;

    private readonly Func<bool> _isGameForeground;
    private readonly CueGate _gate;
    private readonly Timer? _timer;

    /// <param name="isGameForeground">Default: the foreground window belongs to the Diablo IV process.</param>
    /// <param name="pollIntervalMs">Timeout.Infinite leaves polling to the caller (tests call <see cref="PollOnce"/>).</param>
    public GameWindowFocus(CueGate gate, Func<bool>? isGameForeground = null, int pollIntervalMs = PollIntervalMs)
    {
        _gate = gate;
        _isGameForeground = isGameForeground ?? IsGameForeground;
        if (pollIntervalMs != Timeout.Infinite) _timer = new Timer(_ => PollOnce(), null, 0, pollIntervalMs);
    }

    public void PollOnce() => _gate.Set(CueMute.GameInBackground, !_isGameForeground());

    public static bool IsGameForeground()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint processId);
        if (processId == 0) return false;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName == ProcessLocator.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return false; } // process gone meanwhile
    }

    public void Dispose() => _timer?.Dispose();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
