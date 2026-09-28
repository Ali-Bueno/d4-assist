using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;

namespace D4Memory.Scan;

/// <summary>One occurrence of a focus SNO with the raw bytes around it, for offline struct-layout analysis.</summary>
public sealed record FocusOccurrence(string Address, int Sno, string? Name, int ContextStart, string ContextHex)
{
    /// <summary>Parses <see cref="Address"/> (formatted "0x...") back to a numeric address.</summary>
    public long AddressValue => long.Parse(Address.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

/// <summary>Collects every aligned occurrence of a small set of focus SNOs plus surrounding bytes (thread-safe, capped).</summary>
public sealed class FocusCollector(CandidateIndex focus, int bytesBefore, int bytesAfter, int maxOccurrences)
{
    private readonly ConcurrentDictionary<long, FocusOccurrence> _found = new();

    public IReadOnlyList<FocusOccurrence> Occurrences => [.. _found.OrderBy(kv => kv.Key).Select(kv => kv.Value)];

    public void Collect(ReadOnlySpan<byte> data, long baseAddress)
    {
        for (int off = 0; off + sizeof(int) <= data.Length; off += CSceneRecordMatcher.Alignment)
        {
            int sno = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(off, sizeof(int)));
            if (!focus.TryGet(sno, out var scene)) continue;
            if (_found.Count >= maxOccurrences) return;

            int start = Math.Max(0, off - bytesBefore);
            int end = Math.Min(data.Length, off + sizeof(int) + bytesAfter);
            _found.TryAdd(baseAddress + off, new FocusOccurrence(
                $"0x{baseAddress + off:X}", sno, scene.Name, start - off, Convert.ToHexString(data[start..end])));
        }
    }
}
