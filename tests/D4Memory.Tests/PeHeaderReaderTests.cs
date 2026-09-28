using System.Buffers.Binary;
using D4Memory.Safety;

namespace D4Memory.Tests;

public class PeHeaderReaderTests
{
    private const int PeOffset = 0x80;

    /// <summary>Builds the minimal bytes PeHeaderReader needs: DOS header, PE signature, file header
    /// (TimeDateStamp), optional header (SizeOfImage at the fixed offset shared by PE32 and PE32+).</summary>
    private static byte[] MakeHeader(uint timeDateStamp, uint sizeOfImage, int totalSize = PeHeaderReader.HeaderReadSize)
    {
        var buffer = new byte[totalSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, 0x5A4D); // "MZ"
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0x3C), PeOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(PeOffset), 0x00004550); // "PE\0\0"
        int fileHeaderOffset = PeOffset + 4;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(fileHeaderOffset + 4), timeDateStamp);
        int optionalHeaderOffset = fileHeaderOffset + 20;
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(optionalHeaderOffset + 56), sizeOfImage);
        return buffer;
    }

    [Fact]
    public void ParsesTimeDateStampAndSizeOfImage()
    {
        var buffer = MakeHeader(timeDateStamp: 0x60ABCDEF, sizeOfImage: 0x2FE6000);

        Assert.True(PeHeaderReader.TryParse(buffer, out var signature));
        Assert.Equal(0x60ABCDEFu, signature.TimeDateStamp);
        Assert.Equal(0x2FE6000u, signature.SizeOfImage);
    }

    [Fact]
    public void RejectsAWrongDosSignature()
    {
        var buffer = MakeHeader(1, 1);
        buffer[0] = 0; // corrupt "MZ"

        Assert.False(PeHeaderReader.TryParse(buffer, out _));
    }

    [Fact]
    public void RejectsAWrongPeSignature()
    {
        var buffer = MakeHeader(1, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(PeOffset), 0);

        Assert.False(PeHeaderReader.TryParse(buffer, out _));
    }

    [Fact]
    public void RejectsATruncatedBuffer()
    {
        var full = MakeHeader(1, 1);

        Assert.False(PeHeaderReader.TryParse(full.AsSpan(0, PeOffset + 4), out _));
    }

    [Fact]
    public void ReadsAndParsesAnOnDiskFile()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, MakeHeader(timeDateStamp: 42, sizeOfImage: 4096));

            var signature = PeHeaderReader.ReadFromFile(path);

            Assert.Equal(42u, signature.TimeDateStamp);
            Assert.Equal(4096u, signature.SizeOfImage);
        }
        finally { File.Delete(path); }
    }
}
