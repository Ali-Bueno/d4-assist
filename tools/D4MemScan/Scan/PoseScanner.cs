using System.Buffers.Binary;
using D4Data.Model;

namespace D4MemScan.Scan;

/// <summary>One (x, y, z) candidate for the player's position, at the address it was read from.</summary>
public sealed record PoseSample(long Address, float X, float Y, float Z);

/// <summary>Finds every 4-aligned float triple inside a bounding box (classic position-narrowing scan).</summary>
public static class PoseScanner
{
    public const int Alignment = 4;
    private const int TripleBytes = 12;

    public static List<PoseSample> FindCandidates(ReadOnlySpan<byte> buffer, long baseAddress, Box bounds)
    {
        var results = new List<PoseSample>();
        for (int off = 0; off + TripleBytes <= buffer.Length; off += Alignment)
        {
            float x = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(off, 4));
            float y = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(off + 4, 4));
            float z = BinaryPrimitives.ReadSingleLittleEndian(buffer.Slice(off + 8, 4));
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z)) continue;
            if (x < bounds.Min[0] || x > bounds.Max[0]) continue;
            if (y < bounds.Min[1] || y > bounds.Max[1]) continue;
            if (z < bounds.Min[2] || z > bounds.Max[2]) continue;
            results.Add(new PoseSample(baseAddress + off, x, y, z));
        }
        return results;
    }
}
