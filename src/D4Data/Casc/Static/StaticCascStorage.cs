using System.Collections.Concurrent;
using System.IO.Compression;
using System.Buffers.Binary;
using Microsoft.Win32.SafeHandles;
using WiseOwl.Casc;
using WiseOwl.Casc.Diablo4;
using WiseOwl.Casc.Encoding;

namespace D4Data.Casc.Static;

/// <summary>
/// Steam's static CASC: <c>Data/.build.config</c>, archives under <c>Data/&lt;chunk&gt;/</c> found from bits in each
/// encoding key, no index and no encoding table (research/steam-storage.md).
/// </summary>
internal sealed class StaticCascStorage : IDisposable
{
    public const string DataFolder = "Data";
    public const string ConfigFileName = ".build.config";
    private const string BuildInfoFileName = ".build.info";
    private const int KeyPrefixLength = 9; // the hash part of a key; the rest is the location
    private const int MaxLoggedFailures = 5;
    private const int BlteMagic = 0x45544C42; // "BLTE"
    private const int BlteSizeOffset = 4, BlteHeaderMinimum = 8, BlteTableStart = 12, BlteBlockEntrySize = 24;

    private readonly string _data;
    private readonly StaticBuildConfig _config;
    private readonly Dictionary<UInt128, StaticManifest> _manifestsByPrefix;
    private readonly ConcurrentDictionary<string, SafeFileHandle?> _archives = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string>? _log;
    private readonly ConcurrentDictionary<int, bool> _loggedLayouts = new();
    private int _loggedFailures;

    private StaticCascStorage(string root, StaticBuildConfig config, Action<string>? log)
    {
        _data = Path.Combine(root, DataFolder);
        _config = config;
        _log = log;
        _manifestsByPrefix = [];
        foreach (var manifest in config.Manifests) _manifestsByPrefix.TryAdd(Prefix(TvfsSpan.Of(manifest.Key, 0, null, 0)), manifest);
        BuildVersion = config.BuildName ?? ReadBuildVersion(root);
        Files = LoadFiles();
    }

    public string BuildVersion { get; }

    public TvfsFileTable Files { get; }

    /// <summary>True when <paramref name="root"/> holds a static build config (key layouts, no encoding table).</summary>
    public static bool IsStaticInstall(string root)
    {
        try
        {
            var path = ConfigPath(root);
            return File.Exists(path) && StaticBuildConfig.IsStatic(StaticBuildConfig.ParseEntries(File.ReadAllText(path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Opens the storage and its file table; each step it passes is logged, so a failure on a player's machine says where.</summary>
    public static StaticCascStorage Open(string root, Action<string>? log = null)
    {
        var config = StaticBuildConfig.Parse(File.ReadAllText(ConfigPath(root)));
        log?.Invoke($"Steam storage: {config.Manifests.Count} TVFS manifests, {config.IndexBits} layout index bits, layouts "
            + string.Join(", ", config.Layouts.OrderBy(l => l.Key).Select(l =>
                $"{l.Key}={l.Value.ChunkBits}/{l.Value.ArchiveBits}/{l.Value.OffsetBits}x{l.Value.Unit}")) + ".");
        return new StaticCascStorage(root, config, log);
    }

    /// <summary>The decoded file at a TVFS path; null when it isn't there or can't be read (the first failures are logged).</summary>
    public byte[]? ReadPath(string path)
    {
        if (!Files.TryResolve(path, out var spans)) return null;
        try
        {
            if (spans.Length == 1) return Read(spans[0]);
            var whole = new MemoryStream();
            foreach (var span in spans) whole.Write(Read(span));
            return whole.ToArray();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or CascFormatException or UnauthorizedAccessException)
        {
            if (Interlocked.Increment(ref _loggedFailures) <= MaxLoggedFailures) _log?.Invoke($"Steam storage: can't read {path}: {e.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var handle in _archives.Values) handle?.Dispose();
    }

    private static string ConfigPath(string root) => Path.Combine(root, DataFolder, ConfigFileName);

    private TvfsFileTable LoadFiles()
    {
        var root = ReadManifest(_config.Root);
        var files = TvfsFileTable.Parse(root, SubManifest, skipped: Skipped);
        if (!HasSnoFiles(files))
        {
            _log?.Invoke($"Steam storage: no SNO files among {files.Count} paths (e.g. {string.Join(", ", files.SamplePaths)}); "
                + "trying paths without separators.");
            files = TvfsFileTable.Parse(root, SubManifest, ignoreSeparators: true, skipped: Skipped);
            if (!HasSnoFiles(files))
                throw new InvalidDataException($"No {TvfsSnoArchive.CoreTocPath} or SNO files in the Steam file table ({files.Count} paths, "
                    + $"e.g. {string.Join(", ", files.SamplePaths)}).");
        }
        _log?.Invoke($"Steam storage: {files.Count} files, build {BuildVersion}{(files.SeparatorsIgnored ? ", paths without separators" : "")}.");
        return files;
    }

    // CoreTOC and the meta files it lists: D2R's Steam manifests drop separators inside folders, which only the second shows.
    private bool HasSnoFiles(TvfsFileTable files)
    {
        const int sampled = 64;
        if (!files.TryResolve(TvfsSnoArchive.CoreTocPath, out var spans)) return false;
        var toc = CoreToc.Parse(spans.Length == 1 ? Read(spans[0]) : spans.SelectMany(Read).ToArray());
        return toc.Entries.Take(sampled).Any(e => files.TryResolve(Diablo4Storage.SnoPath(e.Id, SnoFolder.Meta), out _));
    }

    // A nested TVFS the build config names: its size and spec come from the config, else from the parent manifest. One that
    // can't be read (e.g. a language not installed) is left out instead of failing the whole storage.
    private byte[]? SubManifest(TvfsSpan span)
    {
        if (!_manifestsByPrefix.TryGetValue(Prefix(span), out var manifest)) return null;
        try
        {
            var bytes = ReadManifest(manifest with
            {
                EncodedSize = manifest.EncodedSize > 0 ? manifest.EncodedSize : span.EncodedSize,
                ESpec = manifest.ESpec ?? span.ESpec,
            });
            if (TvfsFileTable.IsManifest(bytes)) return bytes;
            Skipped($"{manifest.Name} is not a TVFS manifest");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or CascFormatException or UnauthorizedAccessException)
        {
            Skipped($"{manifest.Name}: {e.Message}");
        }
        return null;
    }

    private void Skipped(string reason) => _log?.Invoke($"Steam storage: skipped a nested manifest, {reason}.");

    private byte[] ReadManifest(StaticManifest manifest) =>
        Read(TvfsSpan.Of(manifest.Key, manifest.EncodedSize, manifest.ESpec, 0));

    private byte[] Read(TvfsSpan span)
    {
        if (span.KeyLength < StaticLocation.KeyLength)
            throw new InvalidDataException($"keys have {span.KeyLength} bytes; a static storage locates data with {StaticLocation.KeyLength}.");
        var location = StaticLocation.Decode(span.Key(), _config.IndexBits, _config.Layouts);
        var (handle, name) = OpenArchive(location, span.EncodedSize);
        long offset = location.WholeFile ? 0 : location.Offset;
        if (span.EncodedSize <= 0 && !location.WholeFile && IsZlibAt(handle, offset)) return InflateAt(Path.Combine(_data, name), offset);
        long size = span.EncodedSize > 0 ? span.EncodedSize : location.WholeFile ? RandomAccess.GetLength(handle) : EncodedSizeAt(handle, offset, name);
        var encoded = new byte[size];
        int read = RandomAccess.Read(handle, encoded, offset);
        if (read != size) throw new InvalidDataException($"{name} ends {size - read} bytes short of the file at 0x{offset:X}.");
        return Decode(encoded, span.ESpec);
    }

    // A whole file must also have the encoded size: the two naming schemes overlap (decimal 16 vs hex 0x16).
    private (SafeFileHandle Handle, string Name) OpenArchive(StaticLocation location, long encodedSize)
    {
        foreach (var name in location.ArchiveNames())
        {
            if (_archives.GetOrAdd(name, TryOpen) is not { } handle) continue;
            if (location.WholeFile && encodedSize > 0 && RandomAccess.GetLength(handle) != encodedSize) continue;
            if (_loggedLayouts.TryAdd(location.LayoutIndex, true)) _log?.Invoke($"Steam storage: layout {location.LayoutIndex} file found as {name}.");
            return (handle, name);
        }
        throw new FileNotFoundException($"no archive file for layout {location.LayoutIndex}, chunk {location.Chunk}, archive {location.Archive} "
            + $"(tried {string.Join(", ", location.ArchiveNames())}).");
    }

    private SafeFileHandle? TryOpen(string name)
    {
        var path = Path.Combine(_data, name);
        return File.Exists(path) ? File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete) : null;
    }

    private static bool IsZlibAt(SafeFileHandle handle, long offset)
    {
        var start = new byte[2];
        return RandomAccess.Read(handle, start, offset) == start.Length && IsZlibHeader(start);
    }

    // A zlib stream of unknown size ends itself.
    private static byte[] InflateAt(string path, long offset)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        file.Position = offset;
        using var inflater = new ZLibStream(file, CompressionMode.Decompress);
        var decoded = new MemoryStream();
        inflater.CopyTo(decoded);
        return decoded.ToArray();
    }

    // Only when neither the manifest nor the build config gives the size: a BLTE header's block table sums it.
    private static long EncodedSizeAt(SafeFileHandle handle, long offset, string name)
    {
        var start = new byte[BlteTableStart];
        RandomAccess.Read(handle, start, offset);
        int headerSize = BinaryPrimitives.ReadInt32BigEndian(start.AsSpan(BlteSizeOffset));
        if (BinaryPrimitives.ReadInt32LittleEndian(start) != BlteMagic || headerSize <= BlteHeaderMinimum)
            throw new InvalidDataException($"no size for the file at 0x{offset:X} in {name}, and it has no BLTE block table.");
        var header = new byte[headerSize];
        RandomAccess.Read(handle, header, offset);
        long total = headerSize;
        for (int at = BlteTableStart; at + BlteBlockEntrySize <= headerSize; at += BlteBlockEntrySize)
            total += BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(at));
        return total;
    }

    /// <summary>BLTE (by its magic, whatever the spec says), raw zlib (a "z" spec, or a zlib header when there is no spec) or plain bytes.</summary>
    public static byte[] Decode(byte[] encoded, string? espec)
    {
        if (encoded.Length >= 4 && BinaryPrimitives.ReadInt32LittleEndian(encoded) == BlteMagic) return Blte.Decode(encoded);
        if (espec is not null ? espec.StartsWith('z') : IsZlibHeader(encoded))
        {
            using var inflater = new ZLibStream(new MemoryStream(encoded), CompressionMode.Decompress);
            var decoded = new MemoryStream();
            inflater.CopyTo(decoded);
            return decoded.ToArray();
        }
        return encoded;
    }

    // RFC 1950: deflate method 8, and the two header bytes are a multiple of 31.
    private static bool IsZlibHeader(byte[] b) => b.Length >= 2 && (b[0] & 0x0F) == 8 && ((b[0] << 8) | b[1]) % 31 == 0;

    private static UInt128 Prefix(TvfsSpan span) => span.KeyValue >> ((StaticLocation.KeyLength - KeyPrefixLength) * 8);

    // Without a build-name: Steam's root .build.info lacks the usual columns, but its Version column, when present, names the build.
    private static string ReadBuildVersion(string root)
    {
        const string unknown = "unknown";
        try
        {
            var lines = File.ReadAllLines(Path.Combine(root, BuildInfoFileName)).Where(l => l.Length > 0 && l[0] != '#').ToList();
            if (lines.Count < 2) return unknown;
            char separator = lines[0].Contains('|') ? '|' : '\t';
            int column = Array.FindIndex(lines[0].Split(separator), h => h.StartsWith("Version", StringComparison.OrdinalIgnoreCase));
            var values = lines[1].Split(separator);
            return column >= 0 && column < values.Length && values[column].Length > 0 ? values[column] : unknown;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return unknown; }
    }
}
