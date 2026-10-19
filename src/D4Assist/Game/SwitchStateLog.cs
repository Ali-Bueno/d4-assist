using System.Globalization;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>Debug log of the state signals of switches used again and again (braziers to light, levers): one line when one is
/// first seen, then one per change; which signal tells a lit brazier is unknown (research/dungeon-objectives.md §17). The quest
/// lock and minimap flags show when a quest gizmo opens to the player (the warlock quests' binding altars stayed "unusable"
/// through a whole demon fight, live 2026-10-19).</summary>
/// <param name="isMultiUseSwitch">True for a switch SNO whose data allows many uses (ActorDefinitionInfo.MultiUseSwitch).</param>
public sealed class SwitchStateLog(Func<int, bool> isMultiUseSwitch, Func<int, string?> nameOf, Action<string> log)
{
    /// <summary>ACD attributes named in the game's table that may mark a used or locked gizmo (research/client-memory.md §4).</summary>
    public const string Operatable = "Operatable", Disabled = "Disabled";

    /// <summary>The ACD attributes logged; the RActor's previous-state word is logged too.</summary>
    public static readonly string[] Attributes =
        [AcdFlagReader.GizmoState, AcdFlagReader.GizmoOperated, AcdFlagReader.GizmoDisabledByScript, Operatable, Disabled,
         AcdFlagReader.GizmoQuestRestricted, AcdFlagReader.MinimapActive];

    private const string StateWord = "PreviousState";

    private readonly Dictionary<uint, Dictionary<string, uint>> _last = [];

    /// <summary>Takes one actor poll.</summary>
    /// <param name="attributes">The nonzero ACD attribute values of a record; null when unread.</param>
    /// <param name="known">The attribute names this build's table holds: the others are left out rather than logged as 0.</param>
    public void Observe(
        IEnumerable<RActorRecord> records, Func<RActorRecord, IReadOnlyDictionary<string, uint>?> attributes, IReadOnlyCollection<string> known)
    {
        foreach (var r in records.Where(r => r.IsPlaced && isMultiUseSwitch(r.ActorSno)))
        {
            var values = attributes(r);
            var now = new Dictionary<string, uint> { [StateWord] = r.PreviousState };
            foreach (string name in Attributes.Where(known.Contains)) now[name] = values?.GetValueOrDefault(name) ?? 0;
            if (!_last.TryGetValue(r.Id, out var before))
                Write(r, "seen " + string.Join(" ", now.Select(kv => $"{kv.Key}={kv.Value}")));
            else if (now.Where(kv => before.GetValueOrDefault(kv.Key) != kv.Value).Select(kv => $"{kv.Key}={before.GetValueOrDefault(kv.Key)}→{kv.Value}")
                         .ToList() is { Count: > 0 } changed)
                Write(r, string.Join(", ", changed));
            _last[r.Id] = now;
        }
    }

    private void Write(RActorRecord r, string what) =>
        log(string.Create(CultureInfo.InvariantCulture,
            $"SWITCH {nameOf(r.ActorSno) ?? r.ActorSno.ToString()} sno={r.ActorSno} at ({r.Position.X:F1},{r.Position.Y:F1}): {what}"));
}
