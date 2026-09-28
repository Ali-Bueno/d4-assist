using System.Buffers.Binary;
using System.Numerics;

namespace D4Memory.Scan;

/// <summary>
/// Parses the cheap 32-byte poll window used for repeated sampling: bytes [p-16, p+16) of the player
/// position record (research/client-memory.md §3) — scene/world/actor SNO, the position, and the trailing
/// word. Skips the zero-marker field (unneeded once the record is already located) and the leading word
/// (outside this narrower window).
/// </summary>
public readonly record struct PlayerSampleWindow(int SceneSno, int WorldSno, int ActorSno, Vector3 Position, uint TrailingWord)
{
    /// <summary>Byte offset of `p` within the 32-byte window this type parses.</summary>
    public const int PositionOffsetInWindow = 16;

    public const int WindowSize = 32;

    public static PlayerSampleWindow Parse(ReadOnlySpan<byte> window)
    {
        if (window.Length < WindowSize)
            throw new ArgumentException($"Sample window must be {WindowSize} bytes, got {window.Length}.", nameof(window));

        return new PlayerSampleWindow(
            SceneSno: BinaryPrimitives.ReadInt32LittleEndian(window[..4]),
            WorldSno: BinaryPrimitives.ReadInt32LittleEndian(window.Slice(4, 4)),
            ActorSno: BinaryPrimitives.ReadInt32LittleEndian(window.Slice(8, 4)),
            Position: new Vector3(
                BinaryPrimitives.ReadSingleLittleEndian(window.Slice(16, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(window.Slice(20, 4)),
                BinaryPrimitives.ReadSingleLittleEndian(window.Slice(24, 4))),
            TrailingWord: BinaryPrimitives.ReadUInt32LittleEndian(window.Slice(28, 4)));
    }
}
