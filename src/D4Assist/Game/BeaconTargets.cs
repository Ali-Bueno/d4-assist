namespace D4Assist.Game;

/// <summary>Beacon actor names resolved to SNOs through CoreTOC; a name that doesn't resolve is reported once and skipped.</summary>
public static class BeaconTargets
{
    /// <param name="resolve">Name to SNO; throws <see cref="ArgumentException"/> for an unknown name (SnoStore.Resolve).</param>
    public static IReadOnlySet<int> Resolve(IEnumerable<string> names, Func<string, int> resolve, Action<string> reportUnknown)
    {
        var snos = new HashSet<int>();
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { snos.Add(resolve(name)); }
            catch (ArgumentException) { reportUnknown(name); }
        }
        return snos;
    }
}
