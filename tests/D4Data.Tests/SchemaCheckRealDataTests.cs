using D4Data.Casc;
using D4Data.Schema;

namespace D4Data.Tests;

/// <summary>The schema against the local Diablo IV install; the check passes vacuously without the install, like the other real-data tests.</summary>
public class SchemaCheckRealDataTests
{
    [Fact]
    public void The_real_data_tests_have_definitions_json_wherever_the_game_is_installed()
    {
        if (!SnoStore.TryLocateInstall(out var install)) return; // no game here: every real-data test is vacuous, and that is known

        Assert.True(RealData.FindDefinitions() is not null,
            $"Diablo IV is installed ({install}) but data/definitions.json is missing: every test that reads the game data "
            + "passes without testing anything. Get it from DiabloTools/d4data (README \"Setup\").");
    }

    [Fact]
    public void The_repo_definitions_fit_the_installed_game()
    {
        if (!SnoStore.TryLocateInstall(out var install) || RealData.FindDefinitions() is not { } definitions) return;
        using var store = new SnoStore(install);

        var result = SchemaCheck.Run(store, TypeSchema.Load(definitions));

        Assert.True(result.Passed, $"{result.Records} records, failures: {string.Join("; ", result.Failures)}");
        Assert.True(result.Records >= SchemaCheck.AppRootTypes.Length * SchemaCheck.DefaultSamplesPerType / 2);
    }
}
