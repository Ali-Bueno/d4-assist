using System.Diagnostics;

namespace D4Memory.Safety;

/// <summary>Finds the running Diablo IV process by name; never launches, attaches a debugger, or waits for it.</summary>
public static class ProcessLocator
{
    public const string ProcessName = "Diablo IV";

    public static Process Find() =>
        Process.GetProcessesByName(ProcessName).FirstOrDefault()
            ?? throw new InvalidOperationException($"Process '{ProcessName}' not found. Is Diablo IV running?");

    /// <summary>The folder of the running game's exe, whichever store installed it; null when it isn't running or can't be queried.</summary>
    public static string? ExecutableDirectory() => ExecutablePath() is { } exe ? Path.GetDirectoryName(exe) : null;

    /// <summary>The running game's exe file; null when it isn't running or can't be queried.</summary>
    public static string? ExecutablePath()
    {
        try
        {
            using var process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            return process?.MainModule?.FileName;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>When the running game process started (UTC): tells one game run from the next. Null when it isn't running or can't be queried.</summary>
    public static DateTime? StartedUtc()
    {
        try
        {
            using var process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            return process?.StartTime.ToUniversalTime();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
