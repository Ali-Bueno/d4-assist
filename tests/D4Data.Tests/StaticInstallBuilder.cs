using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace D4Data.Tests;

/// <summary>
/// Writes a Steam-style static storage (research/steam-storage.md) to a temporary folder: archives named and located as
/// TACTLib's Fenris handler does, TVFS manifests, and the build config. Only for tests; the names are spelled out here
/// rather than taken from the reader, so a reader mistake can't hide behind the same mistake.
/// </summary>
internal sealed class StaticInstallBuilder : IDisposable
{
    public const int MetaLayout = 0, TypedWholeFileLayout = 1, ChildLayout = 2, PayloadLayout = 3;
    private const int IndexBits = 2, PayloadUnit = 4096;
    private static readonly (int Chunk, int Archive, int Offset)[] Bits = [(8, 16, 30), (8, 32, 0), (8, 32, 0), (8, 16, 20)];
    private readonly Dictionary<string, MemoryStream> _archives = [];
    private readonly List<string> _config = ["# test build config", $"key-layout-index-bits = {IndexBits}"];
    private int _nextWholeFile = 1;

    public StaticInstallBuilder()
    {
        Root = Path.Combine(Path.GetTempPath(), "d4-static-" + Guid.NewGuid().ToString("N"));
        for (int i = 0; i < Bits.Length; i++)
            _config.Add($"key-layout-{i} = {Bits[i].Chunk} {Bits[i].Archive} {Bits[i].Offset} {(i == PayloadLayout ? PayloadUnit : 0)}");
    }

    public string Root { get; }

    /// <summary>Whole files named as rustydemon guesses (hex), with a decoy of another size under TACTLib's decimal name.</summary>
    public bool HexWholeFileNames { get; init; }

    /// <summary>Stores <paramref name="encoded"/> and returns its 16-byte key: a hash prefix plus the location bits.</summary>
    public byte[] Put(int layout, int chunk, long archive, byte[] encoded)
    {
        string folder = chunk.ToString("D3");
        long offset = 0;
        if (Bits[layout].Offset == 0)
        {
            if (layout == TypedWholeFileLayout) archive = ((long)_nextWholeFile++ << 16) | 0; // type 0: the payload
            else archive = _nextWholeFile++;
            string name = layout == TypedWholeFileLayout ? $"{archive >> 16}-payload.dat" : $"{archive}-child.dat";
            if (HexWholeFileNames && layout == ChildLayout)
            {
                Archive(Path.Combine(folder, name)).Write(new byte[encoded.Length + 1]);
                name = $"0x{archive:x4}-child.dat";
            }
            Archive(Path.Combine(folder, name)).Write(encoded);
        }
        else
        {
            var file = Archive(Path.Combine(folder, layout == PayloadLayout ? $"0x{archive:x4}-payload.dat" : $"0x{archive:x4}-meta.dat"));
            if (layout == PayloadLayout) file.SetLength((file.Length + PayloadUnit - 1) / PayloadUnit * PayloadUnit);
            offset = file.Length;
            file.Position = offset;
            file.Write(encoded);
            if (layout == PayloadLayout) offset /= PayloadUnit;
        }
        return Key(encoded, layout, chunk, archive, offset);
    }

    /// <summary>A manifest the build config names (vfs-root or vfs-N), stored as raw zlib like Steam's.</summary>
    public byte[] PutManifest(string name, byte[] tvfs, bool withSizeAndSpec = true)
    {
        var encoded = Zlib(tvfs);
        var key = Put(MetaLayout, 0, 1, encoded);
        _config.Add($"{name} = {Convert.ToHexString(MD5.HashData(tvfs)).ToLowerInvariant()} {Convert.ToHexString(key).ToLowerInvariant()}");
        if (withSizeAndSpec)
        {
            _config.Add($"{name}-size = {tvfs.Length} {encoded.Length}");
            _config.Add($"{name}-espec = z");
        }
        return key;
    }

    public void AddConfigLine(string line) => _config.Add(line);

    /// <summary>A key whose location is an archive nobody wrote (a language that isn't installed).</summary>
    public static byte[] MissingKey() => Key([0xAB], MetaLayout, 0, 0xFFFF, 0);

    public void Write()
    {
        var data = Path.Combine(Root, "Data");
        foreach (var (name, content) in _archives)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(data, name))!);
            File.WriteAllBytes(Path.Combine(data, name), content.ToArray());
        }
        File.WriteAllText(Path.Combine(data, ".build.config"), string.Join("\n", _config) + "\n");
        // Steam's root .build.info lacks the Battle.net columns; the reader must not mistake the folder for Battle.net.
        File.WriteAllText(Path.Combine(Root, ".build.info"), "Branch!STRING:0|Version!STRING:0\nus|9.9.9.12345\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); } catch (IOException) { }
    }

    public static byte[] Zlib(byte[] content)
    {
        var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true)) zlib.Write(content);
        return output.ToArray();
    }

    /// <summary>BLTE with no chunk table, stored verbatim.</summary>
    public static byte[] BlteRaw(byte[] content) => [.. "BLTE"u8, 0, 0, 0, 0, (byte)'N', .. content];

    /// <summary>BLTE with a one-chunk table, zlib-compressed.</summary>
    public static byte[] BlteZlib(byte[] content)
    {
        byte[] chunk = [(byte)'Z', .. Zlib(content)];
        const int headerSize = 12 + 24;
        var blob = new byte[headerSize + chunk.Length];
        "BLTE"u8.CopyTo(blob);
        BinaryPrimitives.WriteInt32BigEndian(blob.AsSpan(4), headerSize);
        blob[8] = 0x0F;
        blob[11] = 1;
        BinaryPrimitives.WriteInt32BigEndian(blob.AsSpan(12), chunk.Length);
        BinaryPrimitives.WriteInt32BigEndian(blob.AsSpan(16), content.Length);
        MD5.HashData(chunk).CopyTo(blob, 20);
        chunk.CopyTo(blob, headerSize);
        return blob;
    }

    private MemoryStream Archive(string name) => _archives.TryGetValue(name, out var file) ? file : _archives[name] = new MemoryStream();

    private static byte[] Key(byte[] encoded, int layout, int chunk, long archive, long offset)
    {
        var key = new byte[16];
        MD5.HashData(encoded).AsSpan(0, 9).CopyTo(key);
        ulong word = (ulong)key[8] << 56;
        int position = 56;
        Place(ref word, ref position, IndexBits, (ulong)layout);
        Place(ref word, ref position, Bits[layout].Chunk, (ulong)chunk);
        Place(ref word, ref position, Bits[layout].Archive, (ulong)archive);
        Place(ref word, ref position, Bits[layout].Offset, (ulong)offset);
        BinaryPrimitives.WriteUInt64BigEndian(key.AsSpan(8), word);
        return key;
    }

    private static void Place(ref ulong word, ref int position, int bits, ulong value)
    {
        position -= bits;
        if (bits > 0) word |= (value & ((1UL << bits) - 1)) << position;
    }
}

/// <summary>Writes a TVFS manifest (16-byte keys, an ESpec table) for <see cref="StaticInstallBuilder"/>.</summary>
internal static class TvfsWriter
{
    public sealed record File(string Path, byte[] Key, long EncodedSize, long ContentSize, string ESpec);

    private sealed class Folder
    {
        public SortedDictionary<string, Folder> Folders { get; } = new(StringComparer.Ordinal);
        public SortedDictionary<string, int> Files { get; } = new(StringComparer.Ordinal);
    }

    /// <param name="separators">False drops the separator after folder names, as D2R's Steam manifests do.</param>
    public static byte[] Write(IReadOnlyList<File> files, bool separators = true)
    {
        var especs = files.Select(f => f.ESpec).Distinct().ToList();
        var est = new MemoryStream();
        var especOffsets = new Dictionary<string, int>();
        foreach (var spec in especs) { especOffsets[spec] = (int)est.Length; est.Write(Encoding.ASCII.GetBytes(spec)); est.WriteByte(0); }
        int especWidth = Width((int)est.Length);

        var cft = new MemoryStream();
        var cftOffsets = new int[files.Count];
        for (int i = 0; i < files.Count; i++)
        {
            cftOffsets[i] = (int)cft.Length;
            cft.Write(files[i].Key);
            cft.Write(BigEndian(files[i].EncodedSize, 4));
            cft.Write(BigEndian(especOffsets[files[i].ESpec], especWidth));
        }
        int cftWidth = Width((int)cft.Length);

        var vfs = new MemoryStream();
        var root = new Folder();
        for (int i = 0; i < files.Count; i++)
        {
            int vfsOffset = (int)vfs.Length;
            vfs.WriteByte(1);
            vfs.Write(BigEndian(0, 4));
            vfs.Write(BigEndian(files[i].ContentSize, 4));
            vfs.Write(BigEndian(cftOffsets[i], cftWidth));
            var parts = files[i].Path.Split('/');
            var folder = root;
            foreach (var part in parts[..^1]) folder = folder.Folders.TryGetValue(part, out var sub) ? sub : folder.Folders[part] = new Folder();
            folder.Files[parts[^1]] = vfsOffset;
        }
        var paths = PathTable(root, separators);

        const int headerSize = 46;
        int pathOffset = headerSize, vfsOffsetInFile = pathOffset + paths.Length, cftOffset = vfsOffsetInFile + (int)vfs.Length;
        int estOffset = cftOffset + (int)cft.Length;
        var header = new MemoryStream();
        header.Write("TVFS"u8);
        header.Write([1, headerSize, 16, 16]);
        header.Write(BigEndian(2, 4)); // ESpec table present
        foreach (var value in new[] { pathOffset, paths.Length, vfsOffsetInFile, (int)vfs.Length, cftOffset, (int)cft.Length })
            header.Write(BigEndian(value, 4));
        header.Write(BigEndian(0, 2));
        header.Write(BigEndian(estOffset, 4));
        header.Write(BigEndian(est.Length, 4));
        return [.. header.ToArray(), .. paths, .. vfs.ToArray(), .. cft.ToArray(), .. est.ToArray()];
    }

    private static byte[] PathTable(Folder folder, bool separators)
    {
        var table = new MemoryStream();
        foreach (var (name, sub) in folder.Folders)
        {
            var children = PathTable(sub, separators);
            Name(table, name);
            if (separators) table.WriteByte(0);
            table.WriteByte(0xFF);
            table.Write(BigEndian(0x80000000L | (uint)(children.Length + 4), 4));
            table.Write(children);
        }
        foreach (var (name, vfsOffset) in folder.Files)
        {
            Name(table, name);
            table.WriteByte(0xFF);
            table.Write(BigEndian(vfsOffset, 4));
        }
        return table.ToArray();
    }

    private static void Name(Stream table, string name)
    {
        table.WriteByte((byte)name.Length);
        table.Write(Encoding.ASCII.GetBytes(name));
    }

    private static int Width(int tableSize) => tableSize > 0xFFFFFF ? 4 : tableSize > 0xFFFF ? 3 : tableSize > 0xFF ? 2 : 1;

    private static byte[] BigEndian(long value, int width)
    {
        var bytes = new byte[width];
        for (int i = width - 1; i >= 0; i--, value >>= 8) bytes[i] = (byte)value;
        return bytes;
    }
}
