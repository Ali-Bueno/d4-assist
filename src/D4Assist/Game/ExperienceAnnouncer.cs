using System.Globalization;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;

namespace D4Assist.Game;

/// <summary>The player's level and the experience left to the next one, the same for paragon levels (0 while none), and the
/// highest level (0 when unread). At that level the experience left keeps a leftover value (live 2026-11-13: 7240 at 70 of 70).</summary>
public readonly record struct PlayerExperience(int Level, long ToNextLevel, int ParagonLevel, long ToNextParagonLevel, int LevelCap = 0)
{
    public bool AtLevelCap => LevelCap > 0 && Level >= LevelCap;
}

/// <summary>
/// Ctrl+Alt+Shift+X (player request 2026-11-13): the level and the experience left to the next one, spoken and shown on the
/// braille display; at the level cap, the paragon level and what is left to the next.
/// </summary>
/// <param name="readExperience">The local player's experience (IDungeonSessionReader.ReadExperience); null when unreadable.</param>
public sealed class ExperienceAnnouncer(
    Func<PlayerExperience?> readExperience, ISpeechSink speech, LanguageSetting language, Action<string>? log = null)
{
    public void Announce()
    {
        var read = readExperience();
        log?.Invoke(read is { } xp
            ? $"XP level {xp.Level} of {xp.LevelCap}, {xp.ToNextLevel} to next; paragon {xp.ParagonLevel}, {xp.ToNextParagonLevel} to next"
            : "XP unreadable");
        speech.Output(Text(read, ExperienceStrings.For(language.Current)), interrupt: true);
    }

    public static string Text(PlayerExperience? read, ExperienceStrings strings)
    {
        if (read is not { Level: > 0 } xp) return strings.Unreadable;
        static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        if (!xp.AtLevelCap && xp.ToNextLevel > 0) return string.Format(strings.ToNextLevel, xp.Level, Number(xp.ToNextLevel));
        return xp.ToNextParagonLevel > 0
            ? string.Format(strings.ToNextParagonLevel, xp.Level, xp.ParagonLevel, Number(xp.ToNextParagonLevel))
            : string.Format(strings.LevelOnly, xp.Level);
    }
}
