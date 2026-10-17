using System.Numerics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Where an instance placed each blueprint the quests name: a shift, read off the actors that stand at the blueprint's markers.
/// A quest's step in a story dungeon named only blueprint markers, so the guide had nothing to lead to and the dungeon read
/// explored with the ritual hall unvisited (live 2026-10-17, CSD_Skov_Temis_SmugglersRun_02: shifted by (-128,-128)).
/// Found once per blueprint and kept; not thread-safe.
/// </summary>
public sealed class BlueprintPlacement
{
    /// <summary>An actor still stands where its marker put it: the notes and the barrier read 0.0 apart live, NPCs a little more.</summary>
    public const float AgreeUnits = 1f;

    /// <summary>Actors of this many different kinds must agree on one shift: one kind alone can stand in many spots.</summary>
    public const int MinAnchorKinds = 2;

    private readonly Dictionary<int, Vector3> _shifts = [];

    /// <summary>The blueprint markers that land in the world: every marker of a blueprint whose shift is known, shifted.</summary>
    public IEnumerable<PlacedMarker> Place(IReadOnlyList<BlueprintMarker> markers, IReadOnlyList<(int ActorSno, Vector3 Position)> actors)
    {
        foreach (var frame in markers.Select(m => m.Frame).Distinct())
            if (!_shifts.ContainsKey(frame.MarkerSet) && Locate(frame.Anchors, actors) is { } shift)
            {
                _shifts[frame.MarkerSet] = shift;
                Console.WriteLine($"Blueprint {frame.MarkerSet} placed: shift ({shift.X:F1},{shift.Y:F1},{shift.Z:F1}).");
            }
        return markers.Where(m => _shifts.ContainsKey(m.Frame.MarkerSet))
            .Select(m => m.Marker with { Position = m.Marker.Position + _shifts[m.Frame.MarkerSet] });
    }

    /// <summary>The shift most kinds of actor agree on, at least <see cref="MinAnchorKinds"/>; null while none or two shifts tie.</summary>
    public static Vector3? Locate(IReadOnlyList<PlacedMarker> anchors, IReadOnlyList<(int ActorSno, Vector3 Position)> actors)
    {
        var bySno = anchors.ToLookup(a => a.Sno);
        var pairs = actors.SelectMany(a => bySno[a.ActorSno].Select(m => (a.ActorSno, Shift: a.Position - m.Position))).ToList();
        (Vector3 Shift, int Kinds) best = (default, 0);
        bool tied = false;
        foreach (var candidate in pairs)
        {
            var agreeing = pairs.Where(p => Flat(p.Shift - candidate.Shift) <= AgreeUnits).ToList();
            int kinds = agreeing.Select(p => p.ActorSno).Distinct().Count();
            var shift = Mean(agreeing.Select(p => p.Shift));
            if (kinds > best.Kinds) (best, tied) = ((shift, kinds), false);
            else if (kinds == best.Kinds && Flat(shift - best.Shift) > AgreeUnits) tied = true;
        }
        return best.Kinds >= MinAnchorKinds && !tied ? best.Shift : null;
    }

    private static float Flat(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Y * v.Y);

    private static Vector3 Mean(IEnumerable<Vector3> shifts)
    {
        var list = shifts.ToList();
        return list.Aggregate(Vector3.Zero, (sum, s) => sum + s) / list.Count;
    }
}
