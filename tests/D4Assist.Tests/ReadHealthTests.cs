using D4Assist.Game;

namespace D4Assist.Tests;

public class ReadHealthTests
{
    private sealed class Probe
    {
        public TimeSpan Now;
        public List<string> Log { get; } = [];
        public List<ReadCheck> Warned { get; } = [];
        public ReadHealth Health { get; }

        public Probe() => Health = new ReadHealth(() => Now, Log.Add, Warned.Add);
    }

    [Fact]
    public void AFailure_WithinItsGrace_SaysNothing()
    {
        var probe = new Probe();

        probe.Health.Report(ReadCheck.Rooms, ok: false, "unreadable");
        probe.Now = TimeSpan.FromSeconds(19);
        probe.Health.Report(ReadCheck.Rooms, ok: false, "unreadable");

        Assert.Empty(probe.Warned);
        Assert.Empty(probe.Log);
    }

    [Fact]
    public void AFailure_PastItsGrace_IsLoggedAndSpokenOnce()
    {
        var probe = new Probe();

        probe.Health.Report(ReadCheck.Rooms, ok: false, "unreadable");
        probe.Now = TimeSpan.FromSeconds(20);
        probe.Health.Report(ReadCheck.Rooms, ok: false, "unreadable");
        probe.Now = TimeSpan.FromSeconds(40);
        probe.Health.Report(ReadCheck.Rooms, ok: false, "unreadable");

        Assert.Equal([ReadCheck.Rooms], probe.Warned);
        Assert.Single(probe.Log, line => line.Contains("Rooms FAILED") && line.Contains("unreadable"));
    }

    [Fact]
    public void MissingAddresses_AreSpokenAtOnce()
    {
        var probe = new Probe();

        probe.Health.Report(ReadCheck.Addresses, ok: false, "G not found");

        Assert.Equal([ReadCheck.Addresses], probe.Warned);
    }

    [Fact]
    public void AReadThatWorkedInThisWorld_IgnoresLaterFailures()
    {
        var probe = new Probe();

        probe.Health.Report(ReadCheck.Actors, ok: true);
        probe.Health.Report(ReadCheck.Actors, ok: false, "mounted");
        probe.Now = TimeSpan.FromMinutes(5);
        probe.Health.Report(ReadCheck.Actors, ok: false, "mounted");

        Assert.Empty(probe.Warned);
        Assert.Equal(["Self-check: Actors ok."], probe.Log);
    }

    [Fact]
    public void ANewWorld_StartsTheGraceAgain_AndTheFirstSuccessIsLoggedOnce()
    {
        var probe = new Probe();
        probe.Health.Report(ReadCheck.Position, ok: true);
        probe.Health.NewWorld();

        probe.Now = TimeSpan.FromSeconds(100);
        probe.Health.Report(ReadCheck.Position, ok: false, "loading");
        probe.Now = TimeSpan.FromSeconds(130);
        probe.Health.Report(ReadCheck.Position, ok: true);

        Assert.Empty(probe.Warned);
        Assert.Equal(["Self-check: Position ok."], probe.Log);
    }

    [Fact]
    public void AFailureInEachWorld_IsLoggedEachTime_ButSpokenOnce()
    {
        var probe = new Probe();
        probe.Health.Report(ReadCheck.Addresses, ok: false, "G not found");
        probe.Health.NewWorld();
        probe.Health.Report(ReadCheck.Addresses, ok: false, "G not found");

        Assert.Equal([ReadCheck.Addresses], probe.Warned);
        Assert.Equal(2, probe.Log.Count(line => line.Contains("Addresses FAILED")));
    }

    [Fact]
    public void EveryCheck_HasAWarningInBothLanguages()
    {
        foreach (var check in Enum.GetValues<ReadCheck>())
        {
            Assert.Contains(Strings.AppStrings.Spanish.ReadFailures[check], Strings.AppStrings.Spanish.ReadWarning(check));
            Assert.Contains(Strings.AppStrings.English.ReadFailures[check], Strings.AppStrings.English.ReadWarning(check));
        }
    }
}
