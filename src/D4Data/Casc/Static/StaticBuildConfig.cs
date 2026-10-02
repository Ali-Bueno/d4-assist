using System.Globalization;

namespace D4Data.Casc.Static;

/// <summary>How many bits of an encoding key's location word give the chunk folder, the archive and the offset, and the offset's unit.</summary>
internal readonly record struct StaticKeyLayout(int ChunkBits, int ArchiveBits, int OffsetBits, long Unit);

/// <summary>A TVFS manifest the build config names: its full encoding key, encoded size and encoding spec ("z" = raw zlib).</summary>
internal sealed record StaticManifest(string Name, byte[] Key, long EncodedSize, string? ESpec);

/// <summary>
/// Steam's <c>Data/.build.config</c> (<c>key = v1 v2 …</c> lines): the key layouts and the TVFS manifests
/// (research/steam-storage.md §2).
/// </summary>
internal sealed class StaticBuildConfig
{
    public const string RootManifestName = "vfs-root";
    private const string LayoutIndexBitsKey = "key-layout-index-bits";
    private const string LayoutPrefix = "key-layout-";
    private const string ManifestPrefix = "vfs-";
    private const string SizeSuffix = "-size", ESpecSuffix = "-espec";
    private const int EncodedSizeField = 1; // "<content size> <encoded size>"
    private const int EncodingKeyField = 1; // "<content key> <encoding key>"

    private StaticBuildConfig(IReadOnlyDictionary<string, string[]> entries)
    {
        Entries = entries;
        IndexBits = int.Parse(Single(LayoutIndexBitsKey), CultureInfo.InvariantCulture);
        var layouts = new Dictionary<int, StaticKeyLayout>();
        foreach (var (key, values) in entries)
        {
            if (!key.StartsWith(LayoutPrefix, StringComparison.Ordinal) || key == LayoutIndexBitsKey) continue;
            if (!int.TryParse(key.AsSpan(LayoutPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int index)) continue;
            if (values.Length < 3) throw new InvalidDataException($"{key} needs chunk, archive and offset bits, not \"{string.Join(' ', values)}\".");
            long unit = values.Length > 3 ? long.Parse(values[3], CultureInfo.InvariantCulture) : 1;
            layouts[index] = new StaticKeyLayout(Int(values[0]), Int(values[1]), Int(values[2]), Math.Max(1, unit));
        }
        Layouts = layouts;
        Manifests = entries.Keys
            .Where(IsManifestKey)
            .Select(Manifest)
            .OrderBy(m => m.Name == RootManifestName ? 0 : 1).ThenBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
        if (Manifests.Count == 0 || Manifests[0].Name != RootManifestName)
            throw new InvalidDataException($"No {RootManifestName} in the build config.");
    }

    public IReadOnlyDictionary<string, string[]> Entries { get; }

    /// <summary>Bits at the top of the location word that pick the key layout.</summary>
    public int IndexBits { get; }

    public IReadOnlyDictionary<int, StaticKeyLayout> Layouts { get; }

    /// <summary>vfs-root first, then every vfs-N.</summary>
    public IReadOnlyList<StaticManifest> Manifests { get; }

    public StaticManifest Root => Manifests[0];

    /// <summary>The build-name value (TACTLib reads the version from it); null when absent.</summary>
    public string? BuildName => Entries.TryGetValue("build-name", out var values) && values.Length > 0 ? string.Join(' ', values) : null;

    /// <summary>True for the static layout: key layouts and no encoding table.</summary>
    public static bool IsStatic(IReadOnlyDictionary<string, string[]> entries) =>
        entries.ContainsKey(LayoutIndexBitsKey) && !entries.ContainsKey("encoding");

    public static StaticBuildConfig Parse(string text)
    {
        var entries = ParseEntries(text);
        if (!IsStatic(entries)) throw new InvalidDataException($"Not a static build config: no {LayoutIndexBitsKey}, or it has an encoding table.");
        return new StaticBuildConfig(entries);
    }

    public static Dictionary<string, string[]> ParseEntries(string text)
    {
        var entries = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int equals = line.IndexOf('=');
            if (equals <= 0) continue;
            entries[line[..equals].Trim()] = line[(equals + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        }
        return entries;
    }

    // vfs-root or vfs-<n>, as rustydemon and TACTLib accept; other vfs-… keys are left alone.
    private static bool IsManifestKey(string key) =>
        key == RootManifestName
        || (key.StartsWith(ManifestPrefix, StringComparison.Ordinal) && key.Length > ManifestPrefix.Length
            && key.AsSpan(ManifestPrefix.Length).IndexOfAnyExceptInRange('0', '9') < 0);

    private StaticManifest Manifest(string name)
    {
        var keys = Entries[name];
        if (keys.Length <= EncodingKeyField) throw new InvalidDataException($"{name} needs a content key and an encoding key.");
        long size = Entries.TryGetValue(name + SizeSuffix, out var sizes) && sizes.Length > EncodedSizeField
            ? long.Parse(sizes[EncodedSizeField], CultureInfo.InvariantCulture) : 0;
        string? espec = Entries.TryGetValue(name + ESpecSuffix, out var specs) && specs.Length > 0 ? specs[0] : null;
        return new StaticManifest(name, Convert.FromHexString(keys[EncodingKeyField]), size, espec);
    }

    private string Single(string key) =>
        Entries.TryGetValue(key, out var values) && values.Length > 0 ? values[0] : throw new InvalidDataException($"No {key} in the build config.");

    private static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);
}
