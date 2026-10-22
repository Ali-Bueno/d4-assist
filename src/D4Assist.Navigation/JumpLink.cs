namespace D4Assist.Navigation;

/// <summary>
/// A traversal across a gap the walk map can't cross: two jump points (Traversal_Gizmo_Jump) a few units apart, one on each
/// side; the player walks to either one and uses it to land at the other (live 2026-10-18, the Philios mindscape: one tile's
/// floor in four islands joined only by three such pairs). World units. A slide or a climb joins floor the same way (live
/// 2026-10-22, "Die Leere": from the pedestal down a slide, then down a climb, then over a jump to the boss).
/// </summary>
/// <param name="OneWay">Only from A to B: a slide goes down, never back up.</param>
public sealed record JumpLink((float X, float Y) A, (float X, float Y) B, bool OneWay = false);
