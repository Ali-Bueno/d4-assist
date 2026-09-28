namespace D4Memory.Scan;

/// <summary>Array-indexed candidate lookup so the per-int32 hot loop avoids dictionary hashing.</summary>
public sealed class CandidateIndex
{
    private readonly CandidateScene?[] _bySno;

    public CandidateIndex(IReadOnlyDictionary<int, CandidateScene> candidates)
    {
        int maxSno = candidates.Count == 0 ? 0 : candidates.Keys.Max();
        _bySno = new CandidateScene?[maxSno + 1];
        foreach (var (sno, candidate) in candidates)
            if (sno >= 0) _bySno[sno] = candidate;
        Count = candidates.Count;
    }

    public int Count { get; }

    public bool TryGet(int sno, out CandidateScene candidate)
    {
        if ((uint)sno < (uint)_bySno.Length && _bySno[sno] is { } found)
        {
            candidate = found;
            return true;
        }
        candidate = null!;
        return false;
    }
}
