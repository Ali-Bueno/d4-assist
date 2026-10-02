using System.Buffers.Binary;
using System.Text;
using WiseOwl.Casc.Internal;

namespace D4Data.Casc.Static;

/// <summary>One span of a TVFS file: its encoding key (all the bytes the manifest stores), encoded size, encoding spec and content size.</summary>
internal readonly record struct TvfsSpan(UInt128 KeyValue, int KeyLength, long EncodedSize, string? ESpec, long ContentSize)
{
    private const int MaxKeyLength = 16;

    public static TvfsSpan Of(ReadOnlySpan<byte> key, long encodedSize, string? espec, long contentSize)
    {
        Span<byte> padded = stackalloc byte[MaxKeyLength];
        key[..Math.Min(key.Length, MaxKeyLength)].CopyTo(padded);
        return new TvfsSpan(BinaryPrimitives.ReadUInt128BigEndian(padded), key.Length, encodedSize, espec, contentSize);
    }

    /// <summary>The key's bytes, zero-padded to 16.</summary>
    public byte[] Key()
    {
        var bytes = new byte[MaxKeyLength];
        BinaryPrimitives.WriteUInt128BigEndian(bytes, KeyValue);
        return bytes;
    }
}

/// <summary>
/// A TVFS manifest tree, path → spans. Same walk as WiseOwl's TvfsManifest (MIT), but it keeps every span with its
/// encoded size and spec, which Steam's static storage needs to read a file (research/steam-storage.md §6).
/// </summary>
internal sealed class TvfsFileTable
{
    private const uint Magic = 0x53465654; // "TVFS"
    private const uint FolderNode = 0x80000000;
    private const int FolderSizeMask = 0x7FFFFFFF;
    private const int NodeValueLength = 1 + 4; // 0xFF + big-endian value
    private const byte NodeValueMarker = 0xFF;
    private const uint ESpecTableFlag = 2;
    private const int MaxSpans = 224;
    private const int ESpecTableHeaderOffset = 38;
    private const int HeaderSizeField = 5;

    // Nearly every file has one span; the few others live apart so the big table holds no arrays.
    private readonly Dictionary<ulong, TvfsSpan> _single;
    private readonly Dictionary<ulong, TvfsSpan[]> _multiple;

    private TvfsFileTable(Dictionary<ulong, TvfsSpan> single, Dictionary<ulong, TvfsSpan[]> multiple, bool separatorsIgnored, IReadOnlyList<string> samplePaths)
    {
        _single = single;
        _multiple = multiple;
        SeparatorsIgnored = separatorsIgnored;
        SamplePaths = samplePaths;
    }

    public int Count => _single.Count + _multiple.Count;

    /// <summary>True when <paramref name="data"/> starts with the TVFS magic.</summary>
    public static bool IsManifest(ReadOnlySpan<byte> data) => data.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(data) == Magic;

    /// <summary>Paths hashed without their separators: D2R's Steam manifests drop empty directory names, gluing the parts.</summary>
    public bool SeparatorsIgnored { get; }

    /// <summary>The first few paths walked, for the log.</summary>
    public IReadOnlyList<string> SamplePaths { get; }

    public bool TryResolve(string path, out TvfsSpan[] spans) => TryResolve(Hash(Encoding.ASCII.GetBytes(path), SeparatorsIgnored), out spans);

    /// <summary>By the path's hash (WiseOwl's CascPathHash of the upper-case, backslash path).</summary>
    public bool TryResolve(ulong pathHash, out TvfsSpan[] spans)
    {
        if (_single.TryGetValue(pathHash, out var span)) { spans = [span]; return true; }
        return _multiple.TryGetValue(pathHash, out spans!);
    }

    /// <param name="root">The decoded root manifest.</param>
    /// <param name="subManifest">The decoded nested manifest a span points at; null when the span is a file.</param>
    /// <param name="skipped">Told about each nested manifest that turned out not to be one, or broken; its files are left out.</param>
    public static TvfsFileTable Parse(byte[] root, Func<TvfsSpan, byte[]?> subManifest, bool ignoreSeparators = false, Action<string>? skipped = null)
    {
        var walk = new Walk(subManifest, ignoreSeparators, skipped);
        walk.Directory(root, new List<byte>());
        return new TvfsFileTable(walk.Single, walk.Multiple, ignoreSeparators, walk.Samples);
    }

    private sealed class Walk(Func<TvfsSpan, byte[]?> subManifest, bool ignoreSeparators, Action<string>? skipped)
    {
        private const int SampleCount = 8;
        public Dictionary<ulong, TvfsSpan> Single { get; } = [];
        public Dictionary<ulong, TvfsSpan[]> Multiple { get; } = [];
        public List<string> Samples { get; } = [];
        // Each spec string once, not once per file.
        public Dictionary<string, string> ESpecs { get; } = [];

        public void Directory(byte[] data, List<byte> path)
        {
            var header = new Header(data);
            var table = data.AsSpan(header.PathOffset, header.PathSize);
            // A leading folder node wraps the whole table.
            if (table.Length > NodeValueLength && table[0] == NodeValueMarker)
            {
                if ((BinaryPrimitives.ReadUInt32BigEndian(table[1..]) & FolderNode) == 0) throw new InvalidDataException("TVFS root is not a folder node.");
                table = table[NodeValueLength..];
            }
            PathTable(data, header, table, path);
        }

        private void PathTable(byte[] data, Header header, ReadOnlySpan<byte> table, List<byte> path)
        {
            int saved = path.Count;
            while (table.Length > 0)
            {
                table = Entry(table, out var name, out bool separatorBefore, out bool separatorAfter, out int? nodeValue);
                if (separatorBefore) path.Add((byte)'/');
                foreach (var b in name) path.Add(b);
                if (separatorAfter) path.Add((byte)'/');
                if (nodeValue is not { } value) continue;
                if (((uint)value & FolderNode) != 0)
                {
                    int length = (value & FolderSizeMask) - 4; // the size counts its own 4 bytes
                    PathTable(data, header, table[..length], path);
                    table = table[length..];
                }
                else
                {
                    File(data, header, value, path);
                }
                path.RemoveRange(saved, path.Count - saved);
            }
        }

        private void File(byte[] data, Header header, int vfsOffset, List<byte> path)
        {
            var vfs = data.AsSpan(header.VfsOffset, header.VfsSize)[vfsOffset..];
            int count = vfs[0];
            if (count is < 1 or > MaxSpans) return;
            var spans = new TvfsSpan[count];
            int itemSize = 4 + 4 + header.CftOffsetSize; // content offset, content size, CFT offset
            for (int i = 0; i < count; i++)
            {
                var item = vfs.Slice(1 + i * itemSize, itemSize);
                long contentSize = BinaryPrimitives.ReadUInt32BigEndian(item[4..]);
                spans[i] = header.Span(data, ReadVariable(item[8..], header.CftOffsetSize), contentSize, ESpecs);
            }
            if (count == 1 && subManifest(spans[0]) is { } nested)
            {
                path.Add((byte)'/');
                try { Directory(nested, path); }
                catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException)
                {
                    skipped?.Invoke($"{Encoding.ASCII.GetString(path.ToArray())}: {e.Message}");
                }
                return;
            }
            var bytes = path.ToArray();
            if (Samples.Count < SampleCount) Samples.Add(Encoding.ASCII.GetString(bytes));
            ulong hash = Hash(bytes, ignoreSeparators);
            if (Single.ContainsKey(hash) || Multiple.ContainsKey(hash)) return;
            if (count == 1) Single[hash] = spans[0];
            else Multiple[hash] = spans;
        }
    }

    // A path-table entry: [0x00] [length name] [0x00] [0xFF value]; a missing value marks a separator after the name.
    private static ReadOnlySpan<byte> Entry(ReadOnlySpan<byte> t, out ReadOnlySpan<byte> name, out bool before, out bool after, out int? nodeValue)
    {
        name = default;
        before = after = false;
        nodeValue = null;
        if (t.Length > 0 && t[0] == 0) { before = true; t = t[1..]; }
        if (t.Length > 0 && t[0] != NodeValueMarker)
        {
            int length = t[0];
            if (length >= t.Length) throw new InvalidDataException("TVFS path name runs past the table.");
            name = t.Slice(1, length);
            t = t[(1 + length)..];
        }
        if (t.Length > 0 && t[0] == 0) { after = true; t = t[1..]; }
        if (t.Length > 0)
        {
            if (t[0] == NodeValueMarker)
            {
                if (t.Length < NodeValueLength) throw new InvalidDataException("TVFS node value runs past the table.");
                nodeValue = BinaryPrimitives.ReadInt32BigEndian(t[1..]);
                t = t[NodeValueLength..];
            }
            else after = true;
        }
        return t;
    }

    private static ulong Hash(ReadOnlySpan<byte> path, bool ignoreSeparators)
    {
        var normalized = new List<byte>(path.Length);
        foreach (var b in path)
        {
            if (b is (byte)'/' or (byte)'\\')
            {
                if (!ignoreSeparators) normalized.Add((byte)'\\');
            }
            else normalized.Add(b is >= (byte)'a' and <= (byte)'z' ? (byte)(b - 32) : b);
        }
        return CascPathHash.Of(normalized.ToArray());
    }

    private static int ReadVariable(ReadOnlySpan<byte> s, int length)
    {
        int value = 0;
        for (int i = 0; i < length; i++) value = (value << 8) | s[i];
        return value;
    }

    private static int OffsetFieldSize(int tableSize) => tableSize switch
    {
        > 0xFFFFFF => 4,
        > 0xFFFF => 3,
        > 0xFF => 2,
        _ => 1,
    };

    private readonly struct Header
    {
        public Header(byte[] d)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(d) != Magic) throw new InvalidDataException("Not a TVFS manifest (bad magic).");
            KeySize = d[6];
            Flags = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(8));
            PathOffset = BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(12));
            PathSize = BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(16));
            VfsOffset = BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(20));
            VfsSize = BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(24));
            CftOffset = BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(28));
            CftSize = BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(32));
            CftOffsetSize = OffsetFieldSize(CftSize);
            if ((Flags & ESpecTableFlag) != 0 && d[HeaderSizeField] >= ESpecTableHeaderOffset + 8)
            {
                ESpecOffset = BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(ESpecTableHeaderOffset));
                ESpecSize = BinaryPrimitives.ReadInt32BigEndian(d.AsSpan(ESpecTableHeaderOffset + 4));
            }
        }

        public int KeySize { get; }
        public uint Flags { get; }
        public int PathOffset { get; }
        public int PathSize { get; }
        public int VfsOffset { get; }
        public int VfsSize { get; }
        public int CftOffset { get; }
        public int CftSize { get; }
        public int CftOffsetSize { get; }
        public int ESpecOffset { get; }
        public int ESpecSize { get; }

        // A CFT entry: key, big-endian encoded size, then (with the ESpec table) the spec's offset in that table.
        public TvfsSpan Span(byte[] d, int cftOffset, long contentSize, Dictionary<string, string> especs)
        {
            var entry = d.AsSpan(CftOffset, CftSize)[cftOffset..];
            var key = entry[..KeySize];
            long encodedSize = BinaryPrimitives.ReadUInt32BigEndian(entry[KeySize..]);
            string? espec = null;
            if ((Flags & ESpecTableFlag) != 0 && ESpecSize > 0 && ESpecOffset + ESpecSize <= d.Length)
            {
                int index = ReadVariable(entry[(KeySize + 4)..], OffsetFieldSize(ESpecSize));
                if (index >= ESpecSize) return TvfsSpan.Of(key, encodedSize, null, contentSize);
                var table = d.AsSpan(ESpecOffset, ESpecSize)[index..];
                int end = table.IndexOf((byte)0);
                espec = Encoding.ASCII.GetString(end < 0 ? table : table[..end]);
                espec = especs.TryGetValue(espec, out var shared) ? shared : especs[espec] = espec;
            }
            return TvfsSpan.Of(key, encodedSize, espec, contentSize);
        }
    }
}
