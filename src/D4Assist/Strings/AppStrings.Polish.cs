using D4Assist.Game;

namespace D4Assist.Strings;

public sealed partial record AppStrings
{
    /// <summary>Polish (plPL).</summary>
    public static readonly AppStrings Polish = new(
        AssistantActive: "Diablo 4 Assist aktywny",
        WaitingForGame: "Czekam na Diablo IV",
        EnteringDungeon: "Wchodzenie do lochu",
        LeavingDungeon: "Opuszczanie lochu",
        GuideOn: "Przewodnik włączony.",
        GuideOff: "Przewodnik wyłączony.",
        EnteringWorldFormat: "Wchodzisz do: {0}",
        SoundExamples: ["Wyjście", "Otwarta podłoga", "Ściana", "Drzwi", "Niezbadane wyjście", "Dźwięk zadania", "Sygnał przewodnika", "Przybycie"],
        ShelterGuide: "Przewodnik po schronieniach.",
        ShelterReachedFormat: "Schronienie {0} z {1}.",
        GuideToPortal: "Wszystko zbadane. Prowadzę do portalu.",
        GuideToUsedPortal: "Wszystko zbadane. Prowadzę z powrotem do portalu, którym przybyłeś.",
        AssistantClosed: "Diablo 4 Assist zamknięty",
        Restarting: "Ponowne uruchamianie asystenta",
        SkillsStolen: "Mefisto zabrał ci umiejętności. Zbierz kule.",
        SkillsRecovered: "Umiejętności odzyskane.",
        ReadFailures: new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "nie mogę odczytać pozycji postaci, asystent nie może działać",
            [ReadCheck.Addresses] = "nie mogę znaleźć danych tej wersji gry, brak sal, radaru i dźwięków obiektów",
            [ReadCheck.Rooms] = "nie mogę odczytać sal, brak zapowiedzi sal i radaru",
            [ReadCheck.Actors] = "nie mogę odczytać obiektów, brak dźwięków portali, celów i postaci",
            [ReadCheck.Attributes] = "nie mogę odczytać stanu obiektów, użyte obiekty mogą nadal wydawać dźwięk, a zadania nie są śledzone",
            [ReadCheck.Menus] = "nie mogę znaleźć menu gry, dźwięki nie będą wstrzymywane w menu",
        },
        ReadWarningFormat: "Ostrzeżenie: {0}. Wyślij dziennik.",
        Stopped: "Asystent zatrzymał się z powodu błędu. Wyślij dziennik.",
        UpdateAvailableFormat: "Diablo 4 Assist {0} jest dostępny. Zaktualizować teraz?",
        UpdateDownloading: "Pobieranie aktualizacji.",
        UpdateRestarting: "Zaktualizowano. Ponowne uruchamianie.",
        UpdateFailed: "Aktualizacja się nie powiodła; twoja wersja pozostała bez zmian.",
        UpdateFolderNotWritable: "Aktualizacja się nie powiodła: folder aplikacji jest tylko do odczytu. Twoja wersja pozostała bez zmian.",
        UpToDate: "Masz najnowszą wersję.",
        UpdateCheckFailed: "Nie udało się sprawdzić aktualizacji.",
        UpdateOnlyDownloaded: "Aktualizacje działają tylko w pobranej wersji.",
        UpdateProgressFormat: "{0}%",
        UpdatedToFormat: "Zaktualizowano do wersji {0}.",
        GuideDungeonsOnly: "Przewodnik działa tylko w lochach.");
}
