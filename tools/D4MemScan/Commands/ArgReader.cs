namespace D4MemScan.Commands;

/// <summary>Minimal `--flag value` / `--switch` parsing shared by every command.</summary>
internal sealed class ArgReader(string[] args)
{
    private readonly Dictionary<string, string?> _flags = Parse(args);

    public string? Get(string flag) => _flags.GetValueOrDefault(flag);

    public string Require(string flag) => Get(flag) ?? throw new ArgumentException($"Missing {flag}.");

    public bool Has(string flag) => _flags.ContainsKey(flag);

    private static Dictionary<string, string?> Parse(string[] args)
    {
        var flags = new Dictionary<string, string?>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument '{args[i]}'.");
            bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            flags[args[i]] = hasValue ? args[++i] : null;
        }
        return flags;
    }
}
