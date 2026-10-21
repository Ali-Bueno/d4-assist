using System.Numerics;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Where an instance placed each blueprint the quests name: a shift, read off the actors that stand at the blueprint's markers.
/// A quest's step in a story dungeon named only blueprint markers, so the guide had nothing to lead to and the dungeon read
/// explored with the ritual hall unvisited (live 2026-10-17, CSD_Skov_Temis_SmugglersRun_02: shifted by (-128,-128)).
/// Kept per blueprint, and replaced while more kinds of actor come to agree on another shift: two kinds near the player (a trigger
/// sphere and a spawner, each one of many in the blueprint) agreed on a wrong shift in the Askari dungeon, and the guide held at a
/// trigger 140 units off its real spot, while the map's NPCs and exit portal and four more kinds agreed on (-32,32) (live
/// 2026-10-21, "Die Verstoßenen der Königin", CSD_Skov_Athulua_AskariDungeon_BPMS). Not thread-safe.
/// </summary>
public sealed class BlueprintPlacement
{
    /// <summary>An actor still stands where its marker put it: the notes and the barrier read 0.0 apart live, NPCs a little more.</summary>
    public const float AgreeUnits = 1f;

    /// <summary>Actors of this many different kinds must agree on one shift: one kind alone can stand in many spots.</summary>
    public const int MinAnchorKinds = 2;

    /// <summary>Once this many kinds agree on a shift, it is kept without looking again.</summary>
    public const int SettledKinds = 4;

    private readonly Dictionary<int, (Vector3 Shift, int Kinds)> _shifts = [];

    /// <summary>The blueprint markers that land in the world: every marker of a blueprint whose shift is known, shifted.</summary>
    /// <param name="actors">The actors loaded around the player and those the map marks anywhere (NPCs, portals).</param>
    public IEnumerable<PlacedMarker> Place(IReadOnlyList<BlueprintMarker> markers, IReadOnlyList<(int ActorSno, Vector3 Position)> actors)
    {
        foreach (var frame in markers.Select(m => m.Frame).Distinct())
        {
            bool known = _shifts.TryGetValue(frame.MarkerSet, out var kept);
            if (known && kept.Kinds >= SettledKinds || LocateKinds(frame.Anchors, actors) is not { } found || found.Kinds <= kept.Kinds) continue;
            _shifts[frame.MarkerSet] = found;
            if (known && Flat(found.Shift - kept.Shift) <= AgreeUnits) continue;
            Console.WriteLine($"Blueprint {frame.MarkerSet} {(known ? "moved" : "placed")}: shift ({found.Shift.X:F1},{found.Shift.Y:F1},{found.Shift.Z:F1}), " +
                $"{found.Kinds} kinds of actor agree.");
        }
        return markers.Where(m => _shifts.ContainsKey(m.Frame.MarkerSet))
            .Select(m => m.Marker with { Position = m.Marker.Position + _shifts[m.Frame.MarkerSet].Shift });
    }

    /// <summary>The shift most kinds of actor agree on, at least <see cref="MinAnchorKinds"/>; null while none or two shifts tie.</summary>
    public static Vector3? Locate(IReadOnlyList<PlacedMarker> anchors, IReadOnlyList<(int ActorSno, Vector3 Position)> actors) =>
        LocateKinds(anchors, actors)?.Shift;

    // The shift and how many kinds agree on it.
    private static (Vector3 Shift, int Kinds)? LocateKinds(IReadOnlyList<PlacedMarker> anchors, IReadOnlyList<(int ActorSno, Vector3 Position)> actors)
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
        return best.Kinds >= MinAnchorKinds && !tied ? best : null;
    }

    private static float Flat(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Y * v.Y);

    private static Vector3 Mean(IEnumerable<Vector3> shifts)
    {
        var list = shifts.ToList();
        return list.Aggregate(Vector3.Zero, (sum, s) => sum + s) / list.Count;
    }
}
