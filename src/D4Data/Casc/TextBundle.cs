using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Casc;

/// <summary>
/// A language's StringList bundle, kept as the file it is: a table's texts are read the first time one is asked for. As WiseOwl's
/// StringListCatalog the game's 60,000 tables were 680,000 strings for the few dozen names the app speaks. Thread-safe.
/// </summary>
public sealed class TextBundle
{
    // Header: magic, table count, then a {SNO, size} pair per table. Each table starts 8-aligned: its record bytes at +20, the
    // records of 40 bytes at +32, each with a label's offset and length (+8, +12) and its text's (+24, +28), from the table's start.
    private const int HeaderBytes = 8, PairBytes = 8, TableAlignment = 8, RecordsSizeOffset = 20, RecordsOffset = 32, RecordBytes = 40;
    private const int LabelOffset = 8, LabelLength = 12, TextOffset = 24, TextLength = 28;

    private readonly byte[] _file;
    private readonly Dictionary<int, int> _tables = [];
    private readonly ConcurrentDictionary<int, Dictionary<string, string>> _read = new();

    /// <exception cref="InvalidDataException">Not a StringList bundle.</exception>
    public TextBundle(byte[] file)
    {
        _file = file;
        if (file.Length < HeaderBytes || (uint)I32(0) != StringListCatalog.Magic) throw new InvalidDataException("Not a StringList bundle.");
        int count = I32(sizeof(int));
        long next = HeaderBytes + (long)count * PairBytes;
        for (int i = 0; i < count; i++)
        {
            int pair = HeaderBytes + i * PairBytes;
            long table = (next + TableAlignment - 1) & -TableAlignment;
            next = table + (uint)I32(pair + sizeof(int));
            if (table + RecordsOffset > file.Length) break;
            _tables[I32(pair)] = (int)table;
        }
    }

    /// <summary>The text of <paramref name="label"/> in the table <paramref name="sno"/>; false when either is missing.</summary>
    public bool TryGet(int sno, string label, out string text)
    {
        text = string.Empty;
        if (!_tables.TryGetValue(sno, out int table)) return false;
        if (!_read.GetOrAdd(sno, _ => Read(table)).TryGetValue(label, out var found)) return false;
        text = found;
        return true;
    }

    private Dictionary<string, string> Read(int table)
    {
        var texts = new Dictionary<string, string>();
        int bytes = I32(table + RecordsSizeOffset);
        if (bytes <= 0 || bytes % RecordBytes != 0) return texts;
        int record = table + RecordsOffset, end = record + bytes;
        for (; record < end && record + RecordBytes <= _file.Length; record += RecordBytes)
        {
            string label = Utf8(table + I32(record + LabelOffset), I32(record + LabelLength));
            if (label.Length != 0) texts[label] = Utf8(table + I32(record + TextOffset), I32(record + TextLength));
        }
        return texts;
    }

    private int I32(int at) => BinaryPrimitives.ReadInt32LittleEndian(_file.AsSpan(at));

    private string Utf8(int at, int length)
    {
        if (at < 0 || length < 0 || at + (long)length > _file.Length) return string.Empty;
        return Encoding.UTF8.GetString(_file.AsSpan(at, length).TrimEnd((byte)0));
    }
}
