using D4Assist.Game;

namespace D4Assist.Strings;

public sealed partial record AppStrings
{
    /// <summary>Italian (itIT).</summary>
    public static readonly AppStrings Italian = new(
        AssistantActive: "Diablo 4 Assist attivo",
        WaitingForGame: "In attesa di Diablo IV",
        EnteringDungeon: "Ingresso nel dungeon",
        LeavingDungeon: "Uscita dal dungeon",
        GuideOn: "Guida attivata.",
        GuideOff: "Guida disattivata.",
        EnteringWorldFormat: "Ingresso in {0}",
        SoundExamples: ["Uscita", "Passaggio aperto", "Muro", "Porta", "Uscita inesplorata", "Suono della missione", "Segnale della guida", "Arrivo"],
        ShelterGuide: "Guida ai rifugi.",
        ShelterReachedFormat: "Rifugio {0} di {1}.",
        GuideToPortal: "Tutto esplorato. Ti porto a un portale.",
        GuideToUsedPortal: "Tutto esplorato. Ti riporto al portale da cui sei arrivato.",
        AssistantClosed: "Diablo 4 Assist chiuso",
        Restarting: "Riavvio dell'assistente",
        SkillsStolen: "Mefisto ti ha tolto le abilità. Raccogli le sfere.",
        SkillsRecovered: "Abilità recuperate.",
        ReadFailures: new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "impossibile leggere la posizione del personaggio, l'assistente non può funzionare",
            [ReadCheck.Addresses] = "impossibile trovare i dati di questa versione del gioco, niente stanze, radar o suoni degli oggetti",
            [ReadCheck.Rooms] = "impossibile leggere le stanze, niente annunci delle stanze né radar",
            [ReadCheck.Actors] = "impossibile leggere gli oggetti, niente suoni di portali, obiettivi o personaggi",
            [ReadCheck.Attributes] = "impossibile leggere lo stato degli oggetti, gli oggetti già usati potrebbero continuare a suonare e le missioni non vengono seguite",
            [ReadCheck.Menus] = "impossibile trovare i menu del gioco, i suoni non si fermeranno nei menu",
        },
        ReadWarningFormat: "Attenzione: {0}. Invia il registro.",
        Stopped: "L'assistente si è fermato a causa di un errore. Invia il registro.",
        UpdateAvailableFormat: "Diablo 4 Assist {0} è disponibile. Aggiornare ora?",
        UpdateDownloading: "Scaricamento dell'aggiornamento.",
        UpdateRestarting: "Aggiornato. Riavvio.",
        UpdateFailed: "L'aggiornamento non è riuscito; la tua versione è invariata.",
        UpdateFolderNotWritable: "L'aggiornamento non è riuscito: la cartella dell'app non è scrivibile. La tua versione è invariata.",
        UpToDate: "Hai l'ultima versione.",
        UpdateCheckFailed: "Impossibile cercare gli aggiornamenti.",
        UpdateOnlyDownloaded: "Gli aggiornamenti funzionano solo nella versione scaricata.",
        UpdateProgressFormat: "{0}%",
        UpdatedToFormat: "Aggiornato alla versione {0}.",
        GuideDungeonsOnly: "La guida funziona solo nei dungeon.",
        ShelterRouteEnd: "Guida ai rifugi terminata.",
        DebugMode: "Modalità debug attiva.",
        GuideToBossRoom: "Tutto esplorato. Ti porto alla stanza del boss.");
}
