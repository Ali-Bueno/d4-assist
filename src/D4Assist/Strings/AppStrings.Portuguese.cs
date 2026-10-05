using D4Assist.Game;

namespace D4Assist.Strings;

public sealed partial record AppStrings
{
    /// <summary>Portuguese (ptBR).</summary>
    public static readonly AppStrings Portuguese = new(
        AssistantActive: "Diablo 4 Assist ativo",
        WaitingForGame: "Aguardando Diablo IV",
        EnteringDungeon: "Entrando na masmorra",
        LeavingDungeon: "Saindo da masmorra",
        GuideOn: "Guia ativado.",
        GuideOff: "Guia desativado.",
        EnteringWorldFormat: "Entrando em {0}",
        SoundExamples: ["Saída", "Passagem aberta", "Parede", "Porta", "Saída inexplorada", "Som de missão", "Sinal do guia", "Chegada"],
        ShelterGuide: "Guia de abrigos.",
        ShelterReachedFormat: "Abrigo {0} de {1}.",
        GuideToPortal: "Tudo explorado. Levando você a um portal.",
        GuideToUsedPortal: "Tudo explorado. Levando você de volta ao portal por onde chegou.",
        AssistantClosed: "Diablo 4 Assist fechado",
        Restarting: "Reiniciando o assistente",
        SkillsStolen: "Mefisto tirou suas habilidades. Pegue as esferas.",
        SkillsRecovered: "Habilidades recuperadas.",
        ReadFailures: new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "não consigo ler a posição do personagem, o assistente não pode funcionar",
            [ReadCheck.Addresses] = "não encontro os dados desta versão do jogo, sem salas, radar nem sons de objetos",
            [ReadCheck.Rooms] = "não consigo ler as salas, sem anúncios de salas nem radar",
            [ReadCheck.Actors] = "não consigo ler os objetos, sem sons de portais, objetivos nem personagens",
            [ReadCheck.Attributes] = "não consigo ler o estado dos objetos, objetos já usados podem continuar soando e as missões não são acompanhadas",
            [ReadCheck.Menus] = "não encontro os menus do jogo, os sons não vão pausar neles",
        },
        ReadWarningFormat: "Aviso: {0}. Envie o registro.",
        Stopped: "O assistente parou por causa de um erro. Envie o registro.",
        UpdateAvailableFormat: "O Diablo 4 Assist {0} está disponível. Atualizar agora?",
        UpdateDownloading: "Baixando a atualização.",
        UpdateRestarting: "Atualizado. Reiniciando.",
        UpdateFailed: "A atualização falhou; sua versão não foi alterada.",
        UpdateFolderNotWritable: "A atualização falhou: a pasta do aplicativo não permite gravação. Sua versão não foi alterada.",
        UpToDate: "Você está com a versão mais recente.",
        UpdateCheckFailed: "Não foi possível verificar se há atualizações.",
        UpdateOnlyDownloaded: "As atualizações só funcionam na versão baixada.",
        UpdateProgressFormat: "{0}%",
        UpdatedToFormat: "Atualizado para a versão {0}.");
}
