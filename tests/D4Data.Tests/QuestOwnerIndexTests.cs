using D4Data.Readers;

namespace D4Data.Tests;

public class QuestOwnerIndexTests
{
    private static readonly QuestOwnerIndex Index = new([(1, "QST_Frac_PaleKnightsKin"), (2, "QST_Frac_PaleKnightsKin_Lectern_Set"), (3, "QST_Hawe_TheHeretic")]);

    [Theory]
    [InlineData("QST_Frac_PaleKnightsKin_Lectern_Set_01", 2)] // the longest quest name wins
    [InlineData("QST_Frac_PaleKnightsKin_Lectern_02", 1)]
    [InlineData("QST_Frac_PaleKnightsKin_Lectern", 1)]
    [InlineData("qst_hawe_theheretic_Plant_01", 3)]
    public void An_object_belongs_to_the_quest_its_name_starts_with(string actor, int quest) =>
        Assert.Equal(quest, Index.OwnerOf(actor));

    [Theory]
    [InlineData("QST_Frac_PaleKnightsKinship_Lectern")] // not at a "_" boundary
    [InlineData("Corpse_Frac_Villager_M_01")]
    [InlineData("QST_Frac_PaleKnightsKin")] // the quest's own name is no object of it
    public void Other_names_belong_to_no_quest(string actor) => Assert.Null(Index.OwnerOf(actor));
}

/// <summary>Reads the local Diablo IV install; passes vacuously when the install or definitions.json is absent.</summary>
public class QuestOwnerIndexRealDataTests
{
    [Fact]
    public void The_Light_s_Watch_lecterns_belong_to_Pale_Knight_s_Kin()
    {
        if (!D4Data.Casc.SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new D4Data.Casc.SnoStore(install);
        var schema = D4Data.Schema.TypeSchema.Load(definitions);
        int questGroup = schema.SnoGroup("QuestDefinition");
        var index = new QuestOwnerIndex(store.AllEntries(questGroup));
        int quest = store.Resolve(questGroup, "QST_Frac_PaleKnightsKin");

        foreach (string lectern in new[] { "QST_Frac_PaleKnightsKin_Lectern", "QST_Frac_PaleKnightsKin_Lectern_02", "QST_Frac_PaleKnightsKin_Lectern_03" })
            Assert.Equal(quest, index.OwnerOf(lectern));
        Assert.Null(index.OwnerOf("bandit_melee_bomb_fire_unique_DGN_Frac_LightsWatch_01"));
    }
}
