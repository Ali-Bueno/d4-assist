using D4Assist.Game;

namespace D4Assist.Strings;

public sealed partial record AppStrings
{
    /// <summary>Russian (ruRU).</summary>
    public static readonly AppStrings Russian = new(
        AssistantActive: "Diablo 4 Assist работает",
        WaitingForGame: "Ожидание Diablo IV",
        EnteringDungeon: "Вход в подземелье",
        LeavingDungeon: "Выход из подземелья",
        GuideOn: "Проводник включён.",
        GuideOff: "Проводник выключен.",
        EnteringWorldFormat: "Вход: {0}",
        SoundExamples: ["Выход", "Проход", "Стена", "Дверь", "Неисследованный выход", "Звук задания", "Сигнал проводника", "Прибытие"],
        ShelterGuide: "Проводник по укрытиям.",
        ShelterReachedFormat: "Укрытие {0} из {1}.",
        GuideToPortal: "Всё исследовано. Веду к порталу.",
        GuideToUsedPortal: "Всё исследовано. Веду к порталу, через который вы пришли.",
        AssistantClosed: "Diablo 4 Assist закрыт",
        Restarting: "Перезапуск помощника",
        SkillsStolen: "Мефисто забрал ваши умения. Подберите сферы.",
        SkillsRecovered: "Умения возвращены.",
        ReadFailures: new Dictionary<ReadCheck, string>
        {
            [ReadCheck.Position] = "не могу прочитать позицию персонажа, помощник не работает",
            [ReadCheck.Addresses] = "не нахожу данные этой версии игры, не будет комнат, радара и звуков объектов",
            [ReadCheck.Rooms] = "не могу прочитать комнаты, не будет объявлений комнат и радара",
            [ReadCheck.Actors] = "не могу прочитать объекты, не будет звуков порталов, целей и персонажей",
            [ReadCheck.Attributes] = "не могу прочитать состояние объектов, использованные объекты могут продолжать звучать, а задания не отслеживаются",
            [ReadCheck.Menus] = "не нахожу меню игры, звуки в них не будут приостанавливаться",
        },
        ReadWarningFormat: "Внимание: {0}. Отправьте журнал.",
        Stopped: "Помощник остановился из-за ошибки. Отправьте журнал.",
        UpdateAvailableFormat: "Доступен Diablo 4 Assist {0}. Обновить сейчас?",
        UpdateDownloading: "Загрузка обновления.",
        UpdateRestarting: "Обновлено. Перезапуск.",
        UpdateFailed: "Не удалось обновить; ваша версия не изменилась.",
        UpdateFolderNotWritable: "Не удалось обновить: папка приложения недоступна для записи. Ваша версия не изменилась.",
        UpToDate: "У вас последняя версия.",
        UpdateCheckFailed: "Не удалось проверить обновления.",
        UpdateOnlyDownloaded: "Обновления работают только в скачанной версии.",
        UpdateProgressFormat: "{0}%",
        UpdatedToFormat: "Обновлено до версии {0}.");
}
