using System.Diagnostics;

namespace D4Memory.Safety;

/// <summary>Finds the running Diablo IV process by name; never launches, attaches a debugger, or waits for it.</summary>
public static class ProcessLocator
{
    public const string ProcessName = "Diablo IV";

    public static Process Find() =>
        Process.GetProcessesByName(ProcessName).FirstOrDefault()
            ?? throw new InvalidOperationException($"Process '{ProcessName}' not found. Is Diablo IV running?");
}
