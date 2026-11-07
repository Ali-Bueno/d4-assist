using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using WiseOwl.Casc.Diablo4;

namespace D4Data.Casc;

/// <summary>
/// The game's CoreTOC.dat, kept as the file it is: it lists 1.4 million SNOs, and as the strings and objects of WiseOwl's CoreToc they
/// held about 300 MB for the few names the app asks for. Thread-safe.
/// </summary>
public sealed class SnoToc
{
    // Header: four arrays of a u32 per group (entry counts, entry offsets, one unused, format hashes), then one more u32.
    // Entry: group, id, name offset from the end of its block's entries.
    private const int HeaderArrays = 4, EntryBytes = 12, IdOffset = 4, NameOffset = 8;

    private readonly byte[] _file;
    private readonly List<(int Entries, int Names, int Count)> _blocks = [];
    private readonly int[] _ids, _nameAt; // by id
    private readonly ConcurrentDictionary<int, string> _names = new();

    /// <exception cref="InvalidDataException">The file is cut short.</exception>
    public SnoToc(byte[] file)
    {
        _file = file;
        try
        {
            bool sentinel = (uint)I32(0) == CoreToc.NewFormatSentinel;
            int groups = I32(sentinel ? sizeof(int) : 0), counts = sentinel ? 2 * sizeof(int) : sizeof(int);
            int offsets = counts + groups * sizeof(int), blocksFrom = counts + HeaderArrays * groups * sizeof(int) + sizeof(int);
            for (int group = 0; group < groups; group++)
            {
                int count = I32(counts + group * sizeof(int));
                if (count == 0) continue;
                int entries = I32(offsets + group * sizeof(int)) + blocksFrom;
                _blocks.Add((entries, entries + count * EntryBytes, count));
            }
            (_ids, _nameAt) = Index();
        }
        catch (ArgumentOutOfRangeException e) { throw new InvalidDataException("CoreTOC.dat is cut short.", e); }
    }

    /// <summary>How many SNOs the file lists.</summary>
    public int Count => _ids.Length;

    /// <summary>Every SNO id, in the file's order.</summary>
    public IEnumerable<int> Ids =>
        _blocks.SelectMany(b => Enumerable.Range(0, b.Count).Select(e => I32(b.Entries + e * EntryBytes + IdOffset)));

    /// <summary>A SNO's name (ids are unique across groups); null when the file doesn't list it.</summary>
    public string? Name(int id)
    {
        if (_names.TryGetValue(id, out var known)) return known;
        int at = Array.BinarySearch(_ids, id);
        return at < 0 ? null : _names[id] = Text(_nameAt[at]);
    }

    /// <summary>Every SNO of <paramref name="group"/>, in the file's order.</summary>
    public IEnumerable<(int Id, string Name)> InGroup(int group)
    {
        foreach (var (entries, names, count) in _blocks)
            for (int e = 0; e < count; e++)
            {
                int at = entries + e * EntryBytes;
                if (I32(at) == group) yield return (I32(at + IdOffset), Text(names + I32(at + NameOffset)));
            }
    }

    private (int[] Ids, int[] NameAt) Index()
    {
        int total = _blocks.Sum(b => b.Count), next = 0;
        var (ids, nameAt) = (new int[total], new int[total]);
        foreach (var (entries, names, count) in _blocks)
            for (int e = 0; e < count; e++, next++)
            {
                int at = entries + e * EntryBytes;
                (ids[next], nameAt[next]) = (I32(at + IdOffset), names + I32(at + NameOffset));
            }
        Array.Sort(ids, nameAt);
        return (ids, nameAt);
    }

    private int I32(int at) => BinaryPrimitives.ReadInt32LittleEndian(_file.AsSpan(at));

    private string Text(int at)
    {
        var rest = _file.AsSpan(at);
        int end = rest.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? rest : rest[..end]);
    }
}
