using System.Runtime.InteropServices;

namespace D4Assist.Speech;

/// <summary>
/// PRISM's own log in ours while a backend is chosen under Wine: at debug it says why a Wine bridge did not load (upstream
/// logs a failed bridge load there and is silent at info). Afterwards only its warnings and errors, so nothing logs per
/// utterance. Calls prism.dll directly: Prismatoid 0.3.0 has no log API.
/// </summary>
internal static class PrismLog
{
    // prism.h, v0.18.3.
    private enum Level { Trace, Debug, Info, Warn, Error, None }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void Callback(IntPtr userdata, Level level, IntPtr source, IntPtr message);

    [StructLayout(LayoutKind.Sequential)]
    private struct Handler
    {
        public IntPtr Function;
        public IntPtr Userdata;
    }

    // Static, so it outlives every call PRISM makes to it: the handler stays set after Close.
    private static readonly Callback Forward = (_, level, source, message) =>
        Console.WriteLine($"PRISM {level.ToString().ToLowerInvariant()} {Marshal.PtrToStringUTF8(source) ?? "?"}: {Marshal.PtrToStringUTF8(message)}");

    [DllImport("prism", EntryPoint = "prism_set_log_handler")]
    private static extern Handler SetHandler(Handler handler);

    [DllImport("prism", EntryPoint = "prism_set_log_level")]
    private static extern Level SetLevel(Level level);

    public static void Open() => Try(() =>
    {
        SetHandler(new Handler { Function = Marshal.GetFunctionPointerForDelegate(Forward) });
        SetLevel(Level.Debug);
    });

    public static void Close() => Try(() => SetLevel(Level.Warn));

    // A prism.dll older than the one shipped has no log API; speech goes on without its log.
    private static void Try(Action call)
    {
        try { call(); }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { Console.WriteLine($"PRISM log not available: {e.Message}"); }
    }
}
