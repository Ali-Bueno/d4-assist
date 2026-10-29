using System.Collections.Concurrent;
using D4Assist.Audio;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// The enemy cue of a hostile monster from its definition's rarity: bosses and minibosses get D2R's boss ring, uniques its elite
/// teeth, ordinary monsters none (research/dungeon-objectives.md §16). A monster whose definition puts it on another team counts
/// once its live team is the hostile one: the Horadric guardian starts neutral (team 1) and turns for its fight (live 2026-10-23).
/// </summary>
/// <param name="definition">ActorDefinition classification of a SNO; null when unreadable.</param>
public sealed class EnemyClassifier(Func<int, ActorDefinitionInfo?> definition)
{
    // Filled from several poll threads; classifying takes the CASC gate, so no lock is held around it.
    private readonly ConcurrentDictionary<int, EnemyClass?> _cache = new();

    private readonly ConcurrentDictionary<int, ActorDefinitionInfo?> _definitions = new();

    public EnemyClass? ClassOf(int actorSno) => _cache.GetOrAdd(actorSno, sno => Definition(sno) is { } info ? ClassOf(info) : null);

    /// <summary>The live actor's class: by its definition, or by rarity once its live team is hostile (ActorState.Hostile).</summary>
    public EnemyClass? Of(ActorState actor) =>
        ClassOf(actor.ActorSno) ?? (actor.Hostile && Definition(actor.ActorSno) is { } info ? ClassOf(info, hostile: true) : null);

    private ActorDefinitionInfo? Definition(int sno) => _definitions.GetOrAdd(sno, definition);

    /// <param name="hostile">The live team is the hostile one, whatever the definition's default.</param>
    public static EnemyClass? ClassOf(ActorDefinitionInfo info, bool hostile = false) =>
        info.EType != Conventions.ActorTypeMonster || (info.DefaultTeam != Conventions.HostileTeam && !hostile) ? null
        : info.MonsterRarity switch
        {
            Conventions.MonsterRarityBoss => EnemyClass.Boss,
            Conventions.MonsterRarityUnique => EnemyClass.Elite,
            _ => null,
        };
}
