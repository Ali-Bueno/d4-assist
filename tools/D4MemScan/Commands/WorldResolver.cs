using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;
using D4MemScan.Scan;

namespace D4MemScan.Commands;

/// <summary>Resolves the last world the client entered (FenrisDebug.txt) to a World SNO — offline only.</summary>
/// <remarks>Shared by `scenes` (best-effort, for the report header) and `player` (required: an anchor field of the record).</remarks>
internal static class WorldResolver
{
    public static (int Sno, string Name) Resolve(SnoStore store, TypeSchema schema, string install)
    {
        string name = WorldLogReader.LastWorldNameFromFile(Path.Combine(install, "FenrisDebug.txt"));
        int group = new WorldReader(store, schema).Group;
        return (store.Resolve(group, name), name);
    }

    /// <summary>Same as <see cref="Resolve"/>, but returns null instead of throwing when the log/world data isn't available.</summary>
    public static (int Sno, string Name)? TryResolve(SnoStore store, TypeSchema schema, string install)
    {
        try { return Resolve(store, schema, install); }
        catch (IOException) { return null; }
        catch (ArgumentException) { return null; }
    }
}
