using D4Memory.Scan;
using System.Buffers.Binary;

namespace D4MemScan.Scan;

public enum PoseFilterMode { Changed, Unchanged }

/// <summary>Re-reads a snapshot's addresses and keeps the ones that moved or stayed put (classic narrowing).</summary>
public static class PoseFilterer
{
    // Below this distance, float/engine jitter could be mistaken for real movement.
    public const float MinChangeDistanceUnits = 0.05f;

    private const int TripleBytes = 12;

    public static List<PoseSample> Filter(IReadOnlyList<PoseSample> previous, IMemoryReader reader, PoseFilterMode mode)
    {
        var survivors = new List<PoseSample>();
        var buffer = new byte[TripleBytes];
        foreach (var prior in previous)
        {
            if (!reader.TryRead(prior.Address, buffer, TripleBytes)) continue; // address no longer readable; drop
            float x = BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(0, 4));
            float y = BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(4, 4));
            float z = BinaryPrimitives.ReadSingleLittleEndian(buffer.AsSpan(8, 4));
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z)) continue;

            bool changed = Distance(x - prior.X, y - prior.Y, z - prior.Z) > MinChangeDistanceUnits;
            if (changed == (mode == PoseFilterMode.Changed))
                survivors.Add(new PoseSample(prior.Address, x, y, z));
        }
        return survivors;
    }

    private static float Distance(float dx, float dy, float dz) => MathF.Sqrt(dx * dx + dy * dy + dz * dz);
}
