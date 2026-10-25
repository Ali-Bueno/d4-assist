using D4Memory.Safety;

namespace D4MemScan.Commands;

/// <summary>
/// `words --address A[,B...]`: the u32 words around each address in the live game (--before / --after bytes, default 32 / 96),
/// each with its offset, hex and decimal value, and the SNO name it equals when the CASC store knows one (--install). Shows what
/// surrounds a hit of `sno-scan`: a table of world states, a quest record, a string list.
/// </summary>
internal static class WordsCommand
{
    public static int Run(string[] args)
    {
        var reader = new ArgReader(args);
        var addresses = reader.Require("--address").Split(',')
            .Select(a => Convert.ToInt64(a.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase), 16)).ToArray();
        int before = int.Parse(reader.Get("--before") ?? "32"), after = int.Parse(reader.Get("--after") ?? "96");
        using var store = new D4Data.Casc.SnoStore(reader.Get("--install") ?? GameData.DefaultInstallPath);
        using var process = ProcessReader.OpenDiabloIv();
        var word = new byte[sizeof(uint)];
        foreach (long at in addresses)
        {
            Console.WriteLine($"== 0x{at:X}");
            for (long a = (at - before) & ~3L; a <= at + after; a += 4)
            {
                if (!process.TryRead(a, word, word.Length)) { Console.WriteLine($"  {a - at,+5}: unreadable"); continue; }
                uint v = BitConverter.ToUInt32(word);
                string name = v is > 0 and < int.MaxValue && store.Name((int)v) is { } n ? n : "";
                Console.WriteLine($"  {a - at,+5}: {v:X8} {(int)v,11} {name}");
            }
        }
        return 0;
    }
}
