using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;
using D4Memory.Actors;

namespace D4Assist.Game;

/// <summary>
/// F2 (user request 2026-10-28): the helltide's cinders and Baneful Hearts, the two counts the HUD shows in a helltide, spoken
/// and shown on the braille display. Read as they are, wherever the player is: today the cinders vanish when the helltide
/// ends ("Glut: keine") while the hearts stay; from patch 3.3.0 the cinders stay too and are said outside a helltide as well.
/// </summary>
/// <param name="readCurrencies">The local player's currencies by eCurrencyType (IDungeonSessionReader.ReadCurrencies); null when
/// unreadable.</param>
/// <param name="currencyName">A currency type's name in the game's language (its ItemDefinition's Name); null when the text has none.</param>
public sealed class WalletAnnouncer(
    Func<IReadOnlyList<long>?> readCurrencies, Func<int, string?> currencyName, ISpeechSink speech, LanguageSetting language,
    Action<string>? log = null)
{
    public void Announce()
    {
        string text = Text(readCurrencies(), currencyName, WalletStrings.For(language.Current));
        log?.Invoke($"WALLET {text}");
        speech.Output(text, interrupt: true);
    }

    public static string Text(IReadOnlyList<long>? amounts, Func<int, string?> currencyName, WalletStrings strings)
    {
        if (amounts is null || amounts.Count <= CurrencyReader.BanefulHearts) return strings.Unreadable;
        string Entry(int type, string fallback) => string.Format(strings.EntryFormat, currencyName(type) ?? fallback,
            amounts[type] == 0 ? strings.None : amounts[type].ToString(System.Globalization.CultureInfo.InvariantCulture));
        return Entry(CurrencyReader.Cinders, strings.CindersFallback) + strings.Separator + Entry(CurrencyReader.BanefulHearts, strings.HeartsFallback);
    }
}
