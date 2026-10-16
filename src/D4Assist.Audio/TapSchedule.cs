namespace D4Assist.Audio;

/// <summary>The repeat schedule of the entity taps: each aimed target repeats at its own interval and goes silent when it is not
/// re-aimed for the stale time, so a tap can't outlive its feed. Not thread-safe: its player holds the lock.</summary>
public sealed class TapSchedule<TAim>(double staleSeconds)
{
    private sealed class Entry(TAim aim)
    {
        public TAim Aim = aim;
        public double NextDue, LastAim;
    }

    private readonly Dictionary<uint, Entry> _entries = [];

    /// <summary>Replaces the aimed targets; one aimed before keeps its rhythm.</summary>
    public void Aim(IEnumerable<(uint Key, TAim Aim)> aims, double now)
    {
        var aimed = new HashSet<uint>();
        foreach (var (key, aim) in aims)
        {
            aimed.Add(key);
            if (_entries.TryGetValue(key, out var entry)) entry.Aim = aim;
            else _entries[key] = entry = new Entry(aim);
            entry.LastAim = now;
        }
        foreach (uint gone in _entries.Keys.Where(k => !aimed.Contains(k)).ToList()) _entries.Remove(gone);
    }

    public bool IsEmpty => _entries.Count == 0;

    public void Clear() => _entries.Clear();

    /// <summary>The aims due at <paramref name="now"/>, most overdue first and at most <paramref name="max"/>; each one taken is
    /// due again <paramref name="intervalSeconds"/> later.</summary>
    public List<TAim> TakeDue(double now, Func<TAim, double> intervalSeconds, int max = int.MaxValue)
    {
        foreach (uint stale in _entries.Where(e => now - e.Value.LastAim > staleSeconds).Select(e => e.Key).ToList()) _entries.Remove(stale);
        var due = _entries.Values.Where(e => now >= e.NextDue).OrderBy(e => e.NextDue).Take(max).ToList();
        foreach (var entry in due) entry.NextDue = now + intervalSeconds(entry.Aim);
        return [.. due.Select(e => e.Aim)];
    }
}
