using System.Buffers.Binary;
using System.Numerics;

namespace D4Data.Readers;

/// <summary>Decodes an AICellBytes blob into voxel grids in the m_detailGrid word layout (research/dungeon-data.md §4.1b).</summary>
/// <remarks>Only the 64 core levels are stored: margin layers (levels 0–3 and 68–71) are always zero.</remarks>
public static class CellBytesDecoder
{
    /// <summary>Words per grid: 18 brick layers of 16 × 16 bricks, the same shape as m_detailGridWalkable.</summary>
    public const int WordCount = 4608;

    private const int SectionCount = 17;
    private const int HeaderSize = (SectionCount + 4) * sizeof(ulong);
    private const int BricksPerRow = 16, WordsPerLayer = BricksPerRow * BricksPerRow, CoreLayers = 16;
    private const int OctantOffsetMask = 0x7FF;

    // The blob holds two trees with the same shape: walkable surfaces and free (non-solid) space.
    private static readonly Tree Surface = new(RootSlot: 0, Blocks: 1, Octants: 3, Nodes: 4, Leaves: 5, Dictionary: 7, OctantOffsetBit: 29);
    private static readonly Tree FreeSpace = new(RootSlot: 2, Blocks: 10, Octants: 11, Nodes: 12, Leaves: 13, Dictionary: 15, OctantOffsetBit: 36);

    /// <summary>Walkable surface voxels as stored: the top voxel of each floor an agent can stand on.</summary>
    public static ulong[] Surfaces(ReadOnlySpan<byte> blob) => Decode(blob, Surface);

    /// <summary>All-geometry grid, bit-exact with m_detailGrid on the core levels; all zero for an empty blob.</summary>
    public static ulong[] Geometry(ReadOnlySpan<byte> blob)
    {
        var grid = Decode(blob, FreeSpace);
        if (blob.Length < HeaderSize) return grid;
        for (int w = WordsPerLayer; w < (CoreLayers + 1) * WordsPerLayer; w++) grid[w] = ~grid[w];
        return grid;
    }

    private static ulong[] Decode(ReadOnlySpan<byte> blob, Tree tree)
    {
        var grid = new ulong[WordCount];
        if (blob.Length < HeaderSize) return grid;
        ulong root = U64(blob, (SectionCount + tree.RootSlot) * sizeof(ulong));
        var blocks = Section(blob, tree.Blocks);
        var octants = Section(blob, tree.Octants);
        var nodes = Section(blob, tree.Nodes);
        var leaves = Section(blob, tree.Leaves);
        var dictionary = Section(blob, tree.Dictionary);
        int dictionaryWords = dictionary.Length / sizeof(ulong);
        uint indexMask = dictionaryWords > 1 ? (1u << 32 - BitOperations.LeadingZeroCount((uint)(dictionaryWords - 1))) - 1 : 1;

        int entry = 0;
        for (int bit = 0; bit < 64; bit++)
        {
            if ((root >> bit & 1) == 0) continue;
            // Root and block masks: bit = 4 * (y * 4 + x) + z over a 4 x 4 x 4 grid of 16-voxel blocks.
            int bz = bit & 3, bx = bit >> 2 & 3, by = bit >> 4;
            foreach (ulong block in Expand(blocks, U64(blocks, entry++ * sizeof(ulong))))
            {
                int offset = (int)(block >> tree.OctantOffsetBit) & OctantOffsetMask;
                int octantMask = U16(octants, offset * sizeof(ushort)), child = offset + 1;
                for (int o = 0; o < 8; o++)
                {
                    if ((octantMask >> o & 1) == 0) continue;
                    int node = U16(octants, child++ * sizeof(ushort));
                    int leaf = U16(nodes, node * 4), brickMask = U16(nodes, node * 4 + 2);
                    for (int b = 0; b < 8; b++)
                    {
                        if ((brickMask >> b & 1) == 0) continue;
                        // Octant and brick bits: y + 2x + 4z.
                        int zb = bz * 4 + (o >> 2) * 2 + (b >> 2);
                        int yb = by * 4 + (o & 1) * 2 + (b & 1);
                        int xb = bx * 4 + (o >> 1 & 1) * 2 + (b >> 1 & 1);
                        grid[(zb + 1) * WordsPerLayer + yb * BricksPerRow + xb] |= Leaf(leaves, dictionary, indexMask, U32(leaves, leaf++ * sizeof(uint)));
                    }
                }
            }
        }
        return grid;
    }

    // A block entry with bit 63 clear lists several entries (count in the high half, start in the low half).
    private static IEnumerable<ulong> Expand(byte[] blocks, ulong entry)
    {
        if (entry >> 63 != 0) { yield return entry; yield break; }
        int start = (int)(uint)entry, count = (int)(entry >> 32);
        for (int i = start; i < start + count; i++) yield return U64(blocks, i * sizeof(ulong));
    }

    // A leaf reference with bit 31 set is a dictionary index; otherwise it ORs several references (count << 16 | start).
    private static ulong Leaf(byte[] leaves, byte[] dictionary, uint indexMask, uint reference)
    {
        if (reference >> 31 != 0) return U64(dictionary, (int)(reference & indexMask) * sizeof(ulong));
        ulong word = 0;
        int start = (int)(reference & 0xFFFF), count = (int)(reference >> 16);
        for (int i = start; i < start + count; i++) word |= Leaf(leaves, dictionary, indexMask, U32(leaves, i * sizeof(uint)));
        return word;
    }

    private static byte[] Section(ReadOnlySpan<byte> blob, int index)
    {
        int start = (int)U64(blob, index * sizeof(ulong));
        int end = index + 1 < SectionCount ? (int)U64(blob, (index + 1) * sizeof(ulong)) : blob.Length;
        return blob[start..end].ToArray();
    }

    private static ulong U64(ReadOnlySpan<byte> b, int off) => BinaryPrimitives.ReadUInt64LittleEndian(b[off..]);
    private static uint U32(ReadOnlySpan<byte> b, int off) => BinaryPrimitives.ReadUInt32LittleEndian(b[off..]);
    private static ushort U16(ReadOnlySpan<byte> b, int off) => BinaryPrimitives.ReadUInt16LittleEndian(b[off..]);

    private readonly record struct Tree(int RootSlot, int Blocks, int Octants, int Nodes, int Leaves, int Dictionary, int OctantOffsetBit);
}
