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
    // DT_POLYMORPHIC_VARIABLEARRAY adds the element count after the descriptor (d4data parse.js), and its data
    // starts with one 8-byte type header per element, followed by the elements themselves.
    private const int PolymorphicCount = 16;
    private const int PolymorphicTypeHeaderSize = 8;

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

    /// <summary>Element offsets of a DT_POLYMORPHIC_VARIABLEARRAY whose elements are all <paramref name="stride"/> bytes
    /// (base type only); empty when the data size does not match that, i.e. some element is a larger subtype.</summary>
    public IEnumerable<int> PolymorphicArray(int off, int stride)
    {
        var (data, size) = ArrayDescriptor(off);
        int count = I32(off + PolymorphicCount);
        int start = data + count * PolymorphicTypeHeaderSize;
        if (data <= 0 || count <= 0 || stride <= 0 || size != count * (PolymorphicTypeHeaderSize + stride)) yield break;
        for (int i = 0; i < count; i++) yield return start + i * stride;
    }

    /// <summary>Element offsets of a DT_POLYMORPHIC_VARIABLEARRAY of mixed subtypes, stored back to back;
    /// <paramref name="elementSize"/> sizes the element at an offset (null = unknown type). Null when the sizes don't
    /// add up to the array's data size.</summary>
    public List<int>? PolymorphicElements(int off, Func<int, int?> elementSize)
    {
        var (data, size) = ArrayDescriptor(off);
        int count = I32(off + PolymorphicCount);
        if (count == 0) return [];
        if (data <= 0 || count < 0 || size <= 0 || data + size > Length) return null;
        int p = data + count * PolymorphicTypeHeaderSize, end = data + size;
        var elements = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            int? s = p < end ? elementSize(p) : null;
            if (s is not > 0 || p + s.Value > end) return null;
            elements.Add(p);
            p += s.Value;
        }
        return p == end ? elements : null;
    }

    private ReadOnlySpan<byte> Span(int off, int len) => bytes.AsSpan(baseOffset + off, len);
}
