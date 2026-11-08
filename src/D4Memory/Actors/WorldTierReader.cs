using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.Actors;

/// <summary>Reads the game's world tier, the difficulty the statue sets (research/client-memory.md §4, world tier).</summary>
public static class WorldTierReader
{
    // G + 0x990: u32 eWorldTier, 0 Normal … 3 Penitent, 4 Torment 1 … (live 2026-11-07, build 3.2.4.74043: 5, 4, 7, 5, 0, 3 as
    // the user set Torment 2, Torment 1, Torment 4, Torment 2, Normal, Penitent at the statue; the only spot behind a module
    // pointer to follow each change). G + 0x994 held 7 throughout: the highest tier unlocked (Torment 4), not read.
    private const int TierOffset = 0x990;

    /// <summary>The highest eWorldTier in the game data (Torment 12); a larger word is no tier.</summary>
    public const int MaxTier = 15;

    /// <summary>The current eWorldTier; null when the game object is unreadable or the word is no tier.</summary>
    public static int? Read(IMemoryReader memory, long moduleBase, long gameObjectPointerRva)
    {
        if (!PoolContainer.TryReadU64(memory, moduleBase + gameObjectPointerRva, out ulong g) || g == 0) return null;
        var word = new byte[sizeof(uint)];
        if (!memory.TryRead((long)g + TierOffset, word, word.Length)) return null;
        uint tier = BitConverter.ToUInt32(word);
        return tier <= MaxTier ? (int)tier : null;
    }
}
