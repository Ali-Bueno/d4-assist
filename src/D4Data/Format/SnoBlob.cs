using System.Buffers.Binary;
using System.Numerics;

namespace D4Data.Format;

/// <summary>Little-endian view over a SNO record body (Meta after its header, or a raw Payload).</summary>
/// <remarks>Primitive encodings follow d4data parse.js and WiseOwl.Casc docs/casc-diablo4-format.md section 2.</remarks>
public sealed class SnoBlob(byte[] bytes, int baseOffset = 0)
{
    /// <summary>SNOFileHeader magic (WiseOwl casc-diablo4-format.md section 2).</summary>
    public const uint SnoHeaderMagic = 0xDEADBEEF;

    /// <summary>SNOFileHeader size; Meta record fields are relative to the byte after it.</summary>
    public const int SnoHeaderSize = 16;

    // DT_VARIABLEARRAY = { i64 pad; i32 dataOffset; i32 dataSize } (d4data parse.js).
    private const int VarArrayDataOffset = 8;
    private const int VarArrayDataSize = 12;

    /// <summary>A Meta file: validates and skips the SNO header.</summary>
    public static SnoBlob FromMeta(byte[] file)
    {
        if (file.Length < SnoHeaderSize || BinaryPrimitives.ReadUInt32LittleEndian(file) != SnoHeaderMagic)
            throw new InvalidDataException("Not a SNO meta file (bad header magic).");
        return new SnoBlob(file, SnoHeaderSize);
    }

    /// <summary>A Payload file: observed to carry no header, offsets are file-relative.</summary>
    public static SnoBlob FromPayload(byte[] file) => new(file);

    public int Length => bytes.Length - baseOffset;

    public int I32(int off) => BinaryPrimitives.ReadInt32LittleEndian(Span(off, 4));

    public uint U32(int off) => BinaryPrimitives.ReadUInt32LittleEndian(Span(off, 4));

    public ulong U64(int off) => BinaryPrimitives.ReadUInt64LittleEndian(Span(off, 8));

    public float F32(int off) => BinaryPrimitives.ReadSingleLittleEndian(Span(off, 4));

    public Vector3 Vec3(int off) => new(F32(off), F32(off + 4), F32(off + 8));

    /// <summary>bcQuat { x, y, z, w }.</summary>
    public Quaternion Quat(int off) => new(F32(off), F32(off + 4), F32(off + 8), F32(off + 12));

    /// <summary>The (dataOffset, dataSize) of a DT_VARIABLEARRAY descriptor at <paramref name="off"/>.</summary>
    public (int Offset, int Size) ArrayDescriptor(int off) => (I32(off + VarArrayDataOffset), I32(off + VarArrayDataSize));

    /// <summary>Element offsets of a DT_VARIABLEARRAY whose elements are <paramref name="stride"/> bytes.</summary>
    public IEnumerable<int> Array(int off, int stride)
    {
        var (data, size) = ArrayDescriptor(off);
        if (data <= 0 || size <= 0 || stride <= 0) yield break;
        for (int p = data; p + stride <= data + size; p += stride) yield return p;
    }

    private ReadOnlySpan<byte> Span(int off, int len) => bytes.AsSpan(baseOffset + off, len);
}
