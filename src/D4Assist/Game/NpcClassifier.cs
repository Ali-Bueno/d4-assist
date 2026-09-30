using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// Friendly NPCs for the NPC tap: fIsNPC monsters off the hostile team (research/dungeon-objectives.md §9), quest
/// followers and escorts included (user, 2026-09-30). Hired mercenaries, which always walk beside the player, and
/// cinematic stand-ins are left out.
/// </summary>
/// <param name="definition">ActorDefinition classification of a SNO; null when unreadable.</param>
/// <param name="nameOf">CoreTOC name of a SNO.</param>
public sealed class NpcClassifier(Func<int, ActorDefinitionInfo?> definition, Func<int, string?> nameOf)
{
    private const string MercenaryFamily = "NPC_Mercenary";
    private const string CinematicPrefix = "IGC_";

    private readonly Dictionary<int, bool> _cache = [];

    public bool IsNpc(int actorSno)
    {
        if (_cache.TryGetValue(actorSno, out var cached)) return cached;
        return _cache[actorSno] = definition(actorSno) is { EType: Conventions.ActorTypeMonster, IsNpc: true } info
            && info.DefaultTeam != Conventions.HostileTeam
            && nameOf(info.MonsterFamilySno) != MercenaryFamily
            && nameOf(actorSno)?.StartsWith(CinematicPrefix, StringComparison.OrdinalIgnoreCase) != true;
    }
}
