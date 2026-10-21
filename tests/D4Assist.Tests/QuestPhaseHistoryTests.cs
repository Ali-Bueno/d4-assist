using D4Assist.Game;

namespace D4Assist.Tests;

/// <summary>Quest phases kept across app runs (live 2026-10-21: "Verwundete Seelen" lost after a restart).</summary>
public class QuestPhaseHistoryTests
{
    private const int WoundedSouls = 1948800, Collectible = 2507509;

    [Fact]
    public void Phases_read_in_one_run_are_known_in_the_next()
    {
        string dir = Directory.CreateTempSubdirectory("d4assist-quests-").FullName;
        var first = QuestPhaseHistory.Load(dir);
        first.Record(WoundedSouls, 302);
        first.Record(WoundedSouls, 305);
        first.Record(Collectible, 7);

        var next = QuestPhaseHistory.Load(dir);

        Assert.Equal(305u, next.Last(WoundedSouls));
        Assert.Equal(7u, next.Last(Collectible));
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void A_phase_not_read_for_a_month_is_forgotten()
    {
        string dir = Directory.CreateTempSubdirectory("d4assist-quests-").FullName;
        var now = new DateTime(2026, 10, 21, 20, 0, 0, DateTimeKind.Utc);
        var first = QuestPhaseHistory.Load(dir, () => now);
        first.Record(WoundedSouls, 305);
        now += TimeSpan.FromDays(20);
        first.Record(Collectible, 7);

        now += TimeSpan.FromDays(QuestPhaseHistory.KeepDays - 10) + TimeSpan.FromHours(1);
        var next = QuestPhaseHistory.Load(dir, () => now);

        Assert.Null(next.Last(WoundedSouls));
        Assert.Equal(7u, next.Last(Collectible));
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void A_phase_read_again_keeps_it_from_being_forgotten()
    {
        string dir = Directory.CreateTempSubdirectory("d4assist-quests-").FullName;
        var now = new DateTime(2026, 10, 21, 20, 0, 0, DateTimeKind.Utc);
        var history = QuestPhaseHistory.Load(dir, () => now);
        history.Record(WoundedSouls, 305);
        now += TimeSpan.FromDays(QuestPhaseHistory.KeepDays - 1);
        history.Record(WoundedSouls, 305);

        now += TimeSpan.FromDays(5);
        Assert.Equal(305u, QuestPhaseHistory.Load(dir, () => now).Last(WoundedSouls));
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void An_unreadable_file_starts_empty()
    {
        string dir = Directory.CreateTempSubdirectory("d4assist-quests-").FullName;
        File.WriteAllText(Path.Combine(dir, QuestPhaseHistory.FileName), "{ not json");

        var history = QuestPhaseHistory.Load(dir);

        Assert.Null(history.Last(WoundedSouls));
        history.Record(WoundedSouls, 305);
        Assert.Equal(305u, QuestPhaseHistory.Load(dir).Last(WoundedSouls));
        Directory.Delete(dir, recursive: true);
    }
}
