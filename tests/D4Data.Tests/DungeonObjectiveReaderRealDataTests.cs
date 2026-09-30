using D4Data.Casc;
using D4Data.Readers;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>Reads the local Diablo IV install; each test returns early (passes vacuously) when the install or definitions.json is absent.</summary>
public class DungeonObjectiveReaderRealDataTests
{
    private const string WorldDefinition = "WorldDefinition";

    [Fact]
    public void GuulrahnSlums_DpoQuestNamesItsSwitchesAndJailDoor()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
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
    public void Worldstone_WithoutDpo_UsesTheQuestNamedAfterTheWorld()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        var world = store.Resolve(schema.SnoGroup(WorldDefinition), "S15_Triad_B_Worldstone");

        var actors = new DungeonObjectiveReader(store, schema).ActorSnos(world);

        // research/dungeon-objectives.md §7: S15_Arreat_Summit_Altar_A_Gizmo, S15_TriadB_QST_Soulshard.
        Assert.Contains(2647200, actors);
        Assert.Contains(2671217, actors);
    }

    [Fact]
    public void ActorDefinitionReader_ReturnsNullForAnIdOfAnotherType()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);
        int world = store.Resolve(schema.SnoGroup(WorldDefinition), "DGN_Step_GuulrahnSlums"); // a garbage "actor" id from a pool

        var info = new ActorDefinitionReader(store, schema).Read(world);

        Assert.Null(info);
    }

    [Fact]
    public void ActorDefinitionReader_PlayerActorSnosCoverEveryClassAndSexOnly()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);

        var players = new ActorDefinitionReader(store, schema).PlayerActorSnos();

        // barbarianM, sorcererF, paladinF, warlockM, warlockF (build 3.2.1.73552: 8 classes x 2 sexes, nothing else).
        Assert.Superset(new HashSet<int> { 176832, 72908, 2131351, 2212714, 2257570 }, players);
        Assert.InRange(players.Count, 16, 30);
    }

    [Fact]
    public void ConversationDefinitionReader_ReadsTheConvType()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var reader = new ConversationDefinitionReader(store, TypeSchema.Load(definitions));

        // research/client-memory.md §5 census: story scene = 3 (standard), prologue Vani greeting = 1.
        Assert.Equal(3, reader.ReadType(2639189)); // S15_QST_TheButcher_Optional_DeckardCain_S15REUSE
        Assert.Equal(1, reader.ReadType(843567)); // Frac_Prologue_Vani_OPT_03
        Assert.True(reader.IsStandard(2639189));
    }

    [Fact]
    public void ConversationDefinitionReader_TellsNpcLinesFromHeroLines()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var reader = new ConversationDefinitionReader(store, TypeSchema.Load(definitions));

        // Speaker census 2026-09-30: type-1 lines by NPCs talk; the hero's own type-1 lines and type-0 barks don't.
        Assert.True(reader.IsNpcTalk(513029)); // Frac_Prologue_Isbel_01, said on interaction without a menu
        Assert.True(reader.IsNpcTalk(1226029)); // Frac_Prologue_Devmir_OPT_01
        Assert.True(reader.IsNpcTalk(1012842)); // TWN_Scos_Cerrigar_INN_G01, town gossip
        Assert.True(reader.IsNpcTalk(2639189)); // standard story conversation
        Assert.False(reader.IsNpcTalk(149240)); // HRO_SorcF_NotEnoughHealth
        Assert.False(reader.IsNpcTalk(494407)); // Frac_Prologue_Player_01
        Assert.False(reader.IsNpcTalk(641776)); // Frac_Prologue_Village_07, a bark
    }

    [Fact]
    public void WorldWithoutADpoQuest_HasNoObjectiveActors()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);
        var schema = TypeSchema.Load(definitions);

        var actors = new DungeonObjectiveReader(store, schema).ActorSnos(int.MaxValue);

        Assert.Empty(actors);
    }

    // Walks up from this source file, so a relocated build output still finds the repo's data folder.
}
