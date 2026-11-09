using System.Runtime.InteropServices;

namespace D4Assist.Speech;

/// <summary>
/// Wine's own exports, for running under Linux (Proton): PRISM speaks there through its Wine bridges, whose Linux halves
/// Wine loads from <c>WINEDLLPATH</c> (reference/screen-reader-integration/prism-prebuilt.md). Nothing on Windows.
/// </summary>
public static class WineHost
{
    /// <summary>The folder beside the exe that holds PRISM's Linux bridge halves (<c>*.dll.so</c>).</summary>
    public const string BridgeFolderName = "wine";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetVersion();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private delegate IntPtr GetUnixFileName(string dosPath);

    /// <summary>Wine's version (ntdll's <c>wine_get_version</c>); null on Windows.</summary>
    public static string? Version { get; } =
        Export<GetVersion>("ntdll.dll", "wine_get_version") is { } get ? Marshal.PtrToStringUTF8(get()) : null;

    /// <summary>The Unix path of a Windows path (kernel32's <c>wine_get_unix_file_name</c>); null outside Wine.</summary>
    public static string? UnixPath(string windowsPath)
    {
        if (Export<GetUnixFileName>("kernel32.dll", "wine_get_unix_file_name") is not { } get) return null;
        var path = get(windowsPath);
        if (path == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(path); }
        finally { HeapFree(GetProcessHeap(), 0, path); } // Wine allocates it on the process heap
    }

    /// <summary>Under Wine, where the bridges are and what <c>WINEDLLPATH</c> the app was started with; null on Windows.</summary>
    public static string? Describe(string appFolder) =>
        Version is { } version
            ? $"Wine {version}; PRISM's Linux bridges in {BridgeFolder(appFolder)}; WINEDLLPATH {Environment.GetEnvironmentVariable("WINEDLLPATH") ?? "unset"}"
            : null;

    /// <summary>Under Wine, how to start the app so PRISM reaches Linux's speech; null on Windows.</summary>
    public static string? StartHint(string appFolder) =>
        Version is null ? null
            : $"Under Linux, start D4Assist.exe with Proton and WINEDLLPATH=\"{BridgeFolder(appFolder)}\" (protontricks-launch doesn't pass it on), with Orca or Cthulhu running. See README.txt.";

    private static string BridgeFolder(string appFolder)
    {
        var folder = Path.Combine(appFolder, BridgeFolderName);
        return UnixPath(folder) ?? folder;
    }

    private static T? Export<T>(string module, string name) where T : Delegate =>
        NativeLibrary.TryLoad(module, out var handle) && NativeLibrary.TryGetExport(handle, name, out var address)
            ? Marshal.GetDelegateForFunctionPointer<T>(address)
            : null;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetProcessHeap();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HeapFree(IntPtr heap, uint flags, IntPtr memory);
}
