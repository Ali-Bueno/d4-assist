namespace D4Memory.Scan;

/// <summary>Candidate lookup for the per-int32 hot loop: a bit per SNO rejects almost every word before any hashing.</summary>
public sealed class CandidateIndex
{
    private const int BitsPerWord = 64, WordShift = 6;

    private readonly IReadOnlyDictionary<int, CandidateScene> _bySno;
    private readonly ulong[] _known;

    public CandidateIndex(IReadOnlyDictionary<int, CandidateScene> candidates)
    {
        _bySno = candidates;
        int maxSno = candidates.Count == 0 ? 0 : candidates.Keys.Max();
        _known = new ulong[maxSno / BitsPerWord + 1];
        foreach (int sno in candidates.Keys)
            if (sno >= 0) _known[sno >> WordShift] |= 1UL << sno;
        Count = candidates.Count;
    }

    public int Count { get; }

    public bool TryGet(int sno, out CandidateScene candidate)
    {
        if ((uint)sno >> WordShift < (uint)_known.Length && (_known[sno >> WordShift] >> sno & 1) != 0 && _bySno.TryGetValue(sno, out var found))
        {
            candidate = found;
            return true;
        }
        candidate = null!;
        return false;
    }
}
