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
        ReadWarningFormat: "Warnung: {0}. Diablo IV wurde möglicherweise aktualisiert. Bitte sende das Protokoll.",
        Stopped: "Der Assistent wurde wegen eines Fehlers beendet. Bitte sende das Protokoll.");
}
