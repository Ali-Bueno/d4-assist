namespace D4Assist.Game;

/// <summary>
/// Whether a gizmo was used, remembered by its spot so a re-created actor (chests drop out of the actor list at
/// ~16 units and come back under a new id) stays used. A closed chest carries a glow effect actor that the game
/// removes on opening; chest kinds never seen glowing, and doors, use the RActor state word (research/client-memory.md §4).
/// </summary>
/// <param name="knownGlowingKinds">Chest kinds already seen glowing in earlier runs (GlowingChestStore); null for none.</param>
/// <param name="learned">Called once per chest kind newly seen glowing; null for none.</param>
public sealed class GizmoUsedSignal(Func<int, string?> nameOf, IEnumerable<int>? knownGlowingKinds = null, Action<int>? learned = null)
{
    /// <summary>A glow belongs to the chest it sits on: live, 0.015 units from its centre.</summary>
    public const float OnChestUnits = 1f;

    /// <summary>A missing glow is judged only this close: chests entered the actor list at ~15 units live (2026-09-30).</summary>
    public const float JudgeRangeUnits = 10f;

    private static readonly string[] GlowTokens = ["chest", "glow"];

    private readonly Dictionary<int, bool> _isGlow = [];
    private readonly HashSet<int> _glowingKinds = [.. knownGlowingKinds ?? []]; // a chest kind either glows or never does
    private readonly HashSet<(int Sno, int X, int Y)> _usedSpots = [];
    private List<(float X, float Y)> _glows = [];
    private PlayerState? _player;

    /// <summary>Forgets the used spots; call on world change.</summary>
    public void Reset()
    {
        _usedSpots.Clear();
        _glows = [];
        _player = null;
    }

    /// <summary>Feeds one actor poll before any <see cref="IsUsed"/> call for it.</summary>
    public void Observe(IReadOnlyList<ActorState> actors, PlayerState? player)
    {
        _player = player;
        _glows = [.. actors.Where(a => IsGlow(a.ActorSno)).Select(a => (a.X, a.Y))];
    }

    public bool IsUsed(ActorState gizmo)
    {
        var spot = (gizmo.ActorSno, (int)MathF.Round(gizmo.X), (int)MathF.Round(gizmo.Y));
        if (_usedSpots.Contains(spot)) return true;
        if (!Signal(gizmo)) return false;
        _usedSpots.Add(spot);
        return true;
    }

    private bool Signal(ActorState gizmo)
    {
        bool glowing = _glows.Any(g => Distance(g, (gizmo.X, gizmo.Y)) <= OnChestUnits);
        if (glowing && _glowingKinds.Add(gizmo.ActorSno)) learned?.Invoke(gizmo.ActorSno);
        if (!_glowingKinds.Contains(gizmo.ActorSno)) return gizmo.StateChanged;
        return !glowing && _player is { } p && Distance((p.X, p.Y), (gizmo.X, gizmo.Y)) <= JudgeRangeUnits;
    }

    private bool IsGlow(int sno)
    {
        if (_isGlow.TryGetValue(sno, out var cached)) return cached;
        string name = nameOf(sno) ?? "";
        return _isGlow[sno] = GlowTokens.All(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static float Distance((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
