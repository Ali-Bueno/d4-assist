using System.Buffers.Binary;

namespace D4Memory.Safety;

/// <summary>The two PE header fields ModuleLocator matches to identify a module (build-specific, unique enough
/// together): the linker timestamp and the mapped image size.</summary>
public readonly record struct PeHeaderSignature(uint TimeDateStamp, uint SizeOfImage);

/// <summary>
/// Pure PE/COFF header parser (no file or process access itself) — reads just enough of a DOS+NT header to
/// get <see cref="PeHeaderSignature"/>. Offsets are the fixed PE format (Microsoft PE/COFF spec), not
/// derived from any game data. SizeOfImage sits at the same optional-header offset (56) for both PE32 and
/// PE32+: PE32 has an extra 4-byte BaseOfData field but a 4-byte ImageBase, PE32+ has no BaseOfData but an
/// 8-byte ImageBase — the two extra/wider fields cancel out.
/// </summary>
public static class PeHeaderReader
{
    /// <summary>Bytes needed to reach SizeOfImage even in the widest (PE32+) optional header.</summary>
    public const int HeaderReadSize = 0x200;

    private const ushort DosSignature = 0x5A4D; // "MZ"
    private const uint PeSignature = 0x00004550; // "PE\0\0"
    private const int ELfanewOffset = 0x3C;
    private const int FileHeaderSize = 20; // IMAGE_FILE_HEADER
    private const int TimeDateStampOffsetInFileHeader = 4;
    private const int SizeOfImageOffsetInOptionalHeader = 56;

    public static bool TryParse(ReadOnlySpan<byte> buffer, out PeHeaderSignature signature)
    {
        signature = default;
        if (buffer.Length < ELfanewOffset + sizeof(int)) return false;
        if (BinaryPrimitives.ReadUInt16LittleEndian(buffer) != DosSignature) return false;

        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(ELfanewOffset, sizeof(int)));
        int fileHeaderOffset = peOffset + sizeof(uint);
        int optionalHeaderOffset = fileHeaderOffset + FileHeaderSize;
        if (peOffset < 0 || optionalHeaderOffset + SizeOfImageOffsetInOptionalHeader + sizeof(uint) > buffer.Length) return false;
        if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(peOffset, sizeof(uint))) != PeSignature) return false;

        uint timeDateStamp = BinaryPrimitives.ReadUInt32LittleEndian(
            buffer.Slice(fileHeaderOffset + TimeDateStampOffsetInFileHeader, sizeof(uint)));
        uint sizeOfImage = BinaryPrimitives.ReadUInt32LittleEndian(
            buffer.Slice(optionalHeaderOffset + SizeOfImageOffsetInOptionalHeader, sizeof(uint)));
        signature = new PeHeaderSignature(timeDateStamp, sizeOfImage);
        return true;
    }

    /// <summary>Reads and parses the header of the on-disk exe/dll at <paramref name="path"/>.</summary>
    public static PeHeaderSignature ReadFromFile(string path)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[HeaderReadSize];
        int read = stream.Read(buffer, 0, buffer.Length);
        return TryParse(buffer.AsSpan(0, read), out var signature)
            ? signature
            : throw new InvalidDataException($"{path} does not have a valid PE header.");
    }
}
