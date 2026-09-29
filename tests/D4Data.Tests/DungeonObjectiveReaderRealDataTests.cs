using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class DungeonObjectiveReaderRealDataTests
{
    private const string WorldDefinition = "WorldDefinition";
    private const string DefinitionsRelativePath = "data/definitions.json";

    [Fact]
    public void GuulrahnSlums_DpoQuestNamesItsSwitchesAndJailDoor()
    {
        if (!SnoStore.TryLocateInstall(out var install) || FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var world = store.Resolve(schema.SnoGroup(WorldDefinition), "DGN_Step_GuulrahnSlums");

        var actors = new DungeonObjectiveReader(store, schema).ActorSnos(world);

        // research/dungeon-objectives.md §2 table: Human_Stake_Switch_Dyn, Sitting_Skeleton_Switch, Jail_Door_A_01_Dyn.
        Assert.Contains(1120667, actors);
        Assert.Contains(1246560, actors);
        Assert.Contains(1091691, actors);
    }

    [Fact]
    public void ActorDefinitionReader_ReturnsNullForAnIdOfAnotherType()
    {
        if (!SnoStore.TryLocateInstall(out var install) || FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        int world = store.Resolve(schema.SnoGroup(WorldDefinition), "DGN_Step_GuulrahnSlums"); // a garbage "actor" id from a pool

        var info = new ActorDefinitionReader(store, schema).Read(world);

        Assert.Null(info);
    }

    [Fact]
    public void ActorDefinitionReader_PlayerActorSnosCoverEveryClassAndSexOnly()
    {
        if (!SnoStore.TryLocateInstall(out var install) || FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);

        var players = new ActorDefinitionReader(store, schema).PlayerActorSnos();

        // barbarianM, sorcererF, paladinF, warlockM, warlockF (build 3.2.1.73552: 8 classes x 2 sexes, nothing else).
        Assert.Superset(new HashSet<int> { 176832, 72908, 2131351, 2212714, 2257570 }, players);
        Assert.InRange(players.Count, 16, 30);
    }

    [Fact]
    public void WorldWithoutADpoQuest_HasNoObjectiveActors()
    {
        if (!SnoStore.TryLocateInstall(out var install) || FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);

        var actors = new DungeonObjectiveReader(store, schema).ActorSnos(int.MaxValue);

        Assert.Empty(actors);
    }

    // Walks up from this source file, so a relocated build output still finds the repo's data folder.
    private static string? FindDefinitions([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, DefinitionsRelativePath);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
