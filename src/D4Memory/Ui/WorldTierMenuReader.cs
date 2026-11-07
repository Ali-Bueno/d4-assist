using D4Memory.Pools;
using D4Memory.Scan;

namespace D4Memory.Ui;

/// <summary>Reads whether the difficulty statue's menu (Gizmo_World_Tier_Select) is open: it is no screen of the screen
/// registry, so the menu watch never sees it (research/client-memory.md §4, world tier).</summary>
public static class WorldTierMenuReader
{
    // [G + 0xC78] (the object the map view is read from) + 0x3F0: a pointer, set while the statue's menu is open and 0 otherwise.
    // Live 2026-11-07 (build 3.2.4.74043): set on each of three openings, cleared on each close, 0 through the map, the
    // inventory and the blacksmith's menu.
    private const int UiObjectOffset = 0xC78, MenuPointerOffset = 0x3F0;

    /// <summary>True while the statue's menu is open; null when the game object is unreadable.</summary>
    public static bool? IsOpen(IMemoryReader memory, long moduleBase, long gameObjectPointerRva)
    {
        if (!PoolContainer.TryReadU64(memory, moduleBase + gameObjectPointerRva, out ulong g) || g == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)g + UiObjectOffset, out ulong ui) || ui == 0) return null;
        if (!PoolContainer.TryReadU64(memory, (long)ui + MenuPointerOffset, out ulong menu)) return null;
        return menu != 0;
    }
}
