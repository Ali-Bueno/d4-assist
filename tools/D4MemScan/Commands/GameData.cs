using D4Data.Casc;
using D4Data.Schema;

namespace D4MemScan.Commands;

/// <summary>Opens the offline CASC store + type schema shared by the `world` and `scenes` commands.</summary>
internal static class GameData
{
    /// <summary>Default Diablo IV install location, per the task's live-session setup; override with --install.</summary>
    public const string DefaultInstallPath = @"D:\games\battlenet\Diablo IV";

    /// <summary>The main module's file name on disk, relative to the install dir; matches ProcessLocator.ProcessName.</summary>
    public const string MainModuleFileName = "Diablo IV.exe";

    public static (SnoStore Store, TypeSchema Schema, string InstallPath) Open(ArgReader args)
    {
        string install = args.Get("--install") ?? DefaultInstallPath;
        string defs = args.Require("--defs");
        var schema = TypeSchema.Load(defs);
        var store = new SnoStore(install);
        return (store, schema, install);
    }
}
