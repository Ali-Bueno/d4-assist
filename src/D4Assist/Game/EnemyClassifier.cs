using System.Collections.Concurrent;
using D4Assist.Audio;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Game;

/// <summary>
/// The enemy cue of a hostile monster from its definition's rarity: bosses and minibosses get D2R's boss ring, uniques its elite
/// teeth, ordinary monsters none (research/dungeon-objectives.md §16).
/// </summary>
/// <param name="definition">ActorDefinition classification of a SNO; null when unreadable.</param>
public sealed class EnemyClassifier(Func<int, ActorDefinitionInfo?> definition)
{
    // Filled from several poll threads; classifying takes the CASC gate, so no lock is held around it.
    private readonly ConcurrentDictionary<int, EnemyClass?> _cache = new();

    public EnemyClass? ClassOf(int actorSno) => _cache.GetOrAdd(actorSno, sno => definition(sno) is { } info ? ClassOf(info) : null);

    public static EnemyClass? ClassOf(ActorDefinitionInfo info) =>
        info.EType != Conventions.ActorTypeMonster || info.DefaultTeam != Conventions.HostileTeam ? null
        : info.MonsterRarity switch
        {
            Conventions.MonsterRarityBoss => EnemyClass.Boss,
            Conventions.MonsterRarityUnique => EnemyClass.Elite,
            _ => null,
        };
}
