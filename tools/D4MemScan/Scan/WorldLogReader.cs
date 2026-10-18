namespace D4MemScan.Scan;

/// <summary>Parses the last "Client entered world" line from the game's FenrisDebug.txt (offline; no process access).</summary>
public static class WorldLogReader
{
    private const string Marker = "[Game] Client entered world | world: ";

    public static string? LastWorldName(IEnumerable<string> lines)
    {
        string? found = null;
        foreach (var line in lines)
        {
            var name = ExtractName(line);
            if (name is not null) found = name;
        }
        return found;
    }

    public static string LastWorldNameFromFile(string path) =>
        LastWorldName(File.ReadLines(path)) ?? throw new InvalidDataException($"No 'Client entered world' line found in {path}.");

    private static string? ExtractName(string line)
    {
        int i = line.IndexOf(Marker, StringComparison.Ordinal);
        if (i < 0) return null;
        var rest = line[(i + Marker.Length)..];
        int end = rest.IndexOf(" |", StringComparison.Ordinal); // a world name can hold a space
        return (end < 0 ? rest : rest[..end]).Trim();
    }
}
