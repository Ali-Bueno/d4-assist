using D4Data.Readers;
using D4MemScan.Scan;

namespace D4MemScan.Commands;

/// <summary>`world`: resolves the last world the client entered (from FenrisDebug.txt) to a World SNO — offline only.</summary>
internal static class WorldCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var (store, schema, install) = GameData.Open(reader);
        using (store)
        {
            var logPath = Path.Combine(install, "FenrisDebug.txt");
            var worldName = WorldLogReader.LastWorldNameFromFile(logPath);
            int group = new WorldReader(store, schema).Group;
            int sno = store.Resolve(group, worldName);

            Console.WriteLine($"World: {worldName} -> SNO {sno} (group {group})");
            return 0;
        }
    }
}
