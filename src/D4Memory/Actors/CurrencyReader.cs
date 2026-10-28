using D4Memory.Scan;

namespace D4Memory.Actors;

/// <summary>
/// Reads the local player's currencies: an array of one entry per eCurrencyType in the local player's slot of the player block
/// (research/client-memory.md §4, currencies).
/// </summary>
public static class CurrencyReader
{
    // Slot + 0x7688: 98 entries 16 bytes apart (as many as GlobalData.arCurrencyCaps), each an i64 amount followed by 8 other
    // bytes. Live 2026-10-28 (build 3.2.3.73883): gold at 0 (93,051,377 → 93,217,242 as the player picked gold up), the
    // helltide's cinders at 25 (36 → 0 as the helltide ended), the Baneful Hearts at 66 (1). No field of the schema names it.
    private const int ArrayOffset = 0x7688, EntryStride = 16;

    /// <summary>Entries in the array: GlobalData.arCurrencyCaps' length.</summary>
    public const int Count = 98;

    /// <summary>ItemDefinition.eCurrencyType of Helltide_Currency ("Glut", the cinders) and Helltide_BossSummon_Currency
    /// ("Unheilsherz", the Baneful Heart).</summary>
    public const int Cinders = 25, BanefulHearts = 66;

    // Larger than any cap (gold's is 999,999,999,999): a word that isn't an amount.
    private const long MaxAmount = 1_000_000_000_000;

    /// <summary>The amount of each currency type, indexed by eCurrencyType; null when the player block is unreadable, no slot is
    /// the local player's or an entry reads no amount.</summary>
    public static IReadOnlyList<long>? Read(IMemoryReader memory, long moduleBase, long gameObjectPointerRva)
    {
        if (ActivatedWaypointsReader.LocalSlot(memory, moduleBase, gameObjectPointerRva) is not { } slot) return null;
        var bytes = new byte[Count * EntryStride];
        if (!memory.TryRead(slot + ArrayOffset, bytes, bytes.Length)) return null;
        var amounts = new long[Count];
        for (int i = 0; i < Count; i++)
        {
            long amount = BitConverter.ToInt64(bytes, i * EntryStride);
            if (amount is < 0 or >= MaxAmount) return null;
            amounts[i] = amount;
        }
        return amounts;
    }
}
