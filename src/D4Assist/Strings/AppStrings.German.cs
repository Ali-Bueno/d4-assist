using D4Assist.Game;

namespace D4Assist.Strings;

public sealed partial record AppStrings
{
    /// <summary>German (deDE).</summary>
    public static readonly AppStrings German = new(
        AssistantActive: "Diablo 4 Assist aktiv",
        WaitingForGame: "Warte auf Diablo IV",
        EnteringDungeon: "Betrete Dungeon",
        LeavingDungeon: "Verlasse den Dungeon",
        GuideOn: "Führung an.",
        GuideOff: "Führung aus.",
        EnteringWorldFormat: "Betrete {0}",
        SoundExamples: ["Ausgang", "Offener Boden", "Wand", "Tür", "Unerforschter Ausgang", "Questton", "Führungssignal", "Ankunft"],
        ShelterGuide: "Unterschlupf-Führung.",
        ShelterReachedFormat: "Unterschlupf {0} von {1}.",
        GuideToPortal: "Alles erforscht. Ich führe dich zu einem Portal.",
        GuideToUsedPortal: "Alles erforscht. Ich führe dich zurück zum Portal, durch das du gekommen bist.",
        AssistantClosed: "Diablo 4 Assist beendet",
        Restarting: "Der Assistent wird neu gestartet",
        SkillsStolen: "Mephisto hat dir die Fertigkeiten genommen. Sammle die Kugeln ein.",
        SkillsRecovered: "Fertigkeiten zurückerhalten.",
        ReadFailures: new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "Die Position der Spielfigur kann nicht gelesen werden, der Assistent kann nicht arbeiten",
            [ReadCheck.Addresses] = "Die Daten dieser Spielversion wurden nicht gefunden, keine Räume, kein Radar und keine Objekttöne",
            [ReadCheck.Rooms] = "Die Räume können nicht gelesen werden, keine Raumansagen und kein Radar",
            [ReadCheck.Actors] = "Die Objekte können nicht gelesen werden, keine Töne für Portale, Ziele und Figuren",
            [ReadCheck.Attributes] = "Der Zustand der Objekte kann nicht gelesen werden, benutzte Objekte tönen eventuell weiter und Quests werden nicht verfolgt",
            [ReadCheck.Menus] = "Die Spielmenüs wurden nicht gefunden, die Töne pausieren dort nicht",
        },
        ReadWarningFormat: "Warnung: {0}. Bitte sende das Protokoll.",
        Stopped: "Der Assistent wurde wegen eines Fehlers beendet. Bitte sende das Protokoll.",
        UpdateAvailableFormat: "Diablo 4 Assist {0} ist verfügbar. Jetzt aktualisieren?",
        UpdateDownloading: "Das Update wird heruntergeladen.",
        UpdateRestarting: "Aktualisiert. Neustart.",
        UpdateFailed: "Das Update ist fehlgeschlagen; deine Version ist unverändert.",
        UpdateFolderNotWritable: "Das Update ist fehlgeschlagen: Der Programmordner ist nicht beschreibbar. Deine Version ist unverändert.",
        UpToDate: "Du hast die neueste Version.",
        UpdateCheckFailed: "Die Suche nach Updates ist fehlgeschlagen.",
        UpdateOnlyDownloaded: "Updates funktionieren nur in der heruntergeladenen Version.",
        UpdateProgressFormat: "{0} %",
        UpdatedToFormat: "Auf Version {0} aktualisiert.",
        GuideDungeonsOnly: "Die Führung funktioniert nur in Dungeons.",
        ShelterRouteEnd: "Unterschlupf-Führung beendet.",
        DebugMode: "Debug-Modus aktiv.",
        GuideToBossRoom: "Alles erforscht. Ich führe dich zum Bossraum.",
        TrailGuide: "Blütenspur-Führung.",
        TrailEnd: "Ende der Spur.",
        PathGuideFormat: "Questziel: {0}, {1} Meter.",
        PathEnd: "Questziel erreicht.",
        PathNotFound: "Kein Weg gefunden; ich zeige die Richtung.",
        NoQuestPlace: "Kein Questziel auf der Karte.",
        PentagramGuide: "Pentagramm-Führung.",
        PentagramEnd: "Ende des Pentagramms.",
        QuestMarker: "Questmarkierung",
        PlaceCountFormat: "{0} von {1}.",
        GuideExploreMode: "Führung: Erkunden.",
        GuideObjectivesMode: "Führung: Ziele.");
}
