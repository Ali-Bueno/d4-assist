using System.Buffers.Binary;
using System.Globalization;

namespace D4Data.Casc.Static;

/// <summary>Where an encoding key's data sits in Steam's static storage, read from bits 9–15 of the key (research/steam-storage.md §3–4).</summary>
internal readonly record struct StaticLocation(int LayoutIndex, StaticKeyLayout Layout, long Chunk, long Archive, long Offset)
{
    public const int KeyLength = 16;
    private const int LocationBits = 56; // bytes 9..15 of the key; byte 8 belongs to the hash prefix
    private const int LocationWordStart = 8;
    // Layout 1 (TACTLib Fenris): the archive's low 16 bits are the file type, 0 the payload and n type n-1.
    private const int TypeBits = 16, FirstNumberedType = 1;

    /// <summary>A layout with no offset bits holds whole files: one file per archive number.</summary>
    public bool WholeFile => Layout.OffsetBits == 0;

    public static StaticLocation Decode(ReadOnlySpan<byte> key, int indexBits, IReadOnlyDictionary<int, StaticKeyLayout> layouts)
    {
        if (key.Length < KeyLength) throw new InvalidDataException($"A static storage key has {KeyLength} bytes, not {key.Length}.");
        ulong word = BinaryPrimitives.ReadUInt64BigEndian(key[LocationWordStart..]);
        int position = LocationBits;
        int index = (int)Take(word, ref position, indexBits);
        if (!layouts.TryGetValue(index, out var layout)) throw new InvalidDataException($"Key layout {index} is not in the build config.");
        long chunk = (long)Take(word, ref position, layout.ChunkBits);
        long archive = (long)Take(word, ref position, layout.ArchiveBits);
        long offset = (long)Take(word, ref position, layout.OffsetBits) * layout.Unit;
        return new StaticLocation(index, layout, chunk, archive, offset);
    }

    /// <summary>
    /// The archive file names to try under <c>Data</c>, most likely first: TACTLib's Fenris names by layout index, then
    /// rustydemon's names by layout shape (the two disagree on whole-file layouts).
    /// </summary>
    public IEnumerable<string> ArchiveNames()
    {
        string folder = Chunk.ToString("D3", CultureInfo.InvariantCulture);
        string hex = $"0x{Archive:x4}";
        var names = new List<string>();
        switch (LayoutIndex)
        {
            case 1:
                long type = Archive & ((1L << TypeBits) - 1);
                string suffix = type == 0 ? "payload" : (type - FirstNumberedType).ToString("D4", CultureInfo.InvariantCulture);
                names.Add($"{Archive >> TypeBits}-{suffix}.dat");
                break;
            case 2: names.Add($"{Archive}-child.dat"); break;
            case 3: names.Add($"{hex}-payload.dat"); break;
            default: names.Add($"{hex}-meta.dat"); break;
        }
        if (WholeFile)
            names.AddRange([$"{Archive:x}-child.dat", $"{Archive:x}-payload.dat", $"{hex}-child.dat", $"{hex}-payload.dat"]);
        else
            names.Add(Layout.Unit > 1 ? $"{hex}-payload.dat" : $"{hex}-meta.dat");
        return names.Distinct(StringComparer.OrdinalIgnoreCase).Select(name => Path.Combine(folder, name));
    }

    private static ulong Take(ulong word, ref int position, int bits)
    {
        if (bits == 0) return 0;
        if (bits > position) throw new InvalidDataException($"Key layout needs {bits} more bits but only {position} are left.");
        position -= bits;
        return (word >> position) & ((1UL << bits) - 1);
    }
}
