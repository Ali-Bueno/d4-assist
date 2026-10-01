namespace D4Assist.Navigation;

public sealed partial class NavigationPhrases
{
    /// <summary>Turkish (trTR).</summary>
    public static readonly NavigationPhrases Turkish = new()
    {
        SingularHeader = "Çıkış",
        PluralHeader = "Çıkışlar",
        UnexploredFormat = "{0}, keşfedilmemiş",
        GroupFormat = "{0} çıkış {1}",
        CountWords = ["iki", "üç", "dört"],
        DeadEnd = "Çıkmaz sokak.",
        Entrance = "Giriş.",
        NoOtherExits = "Başka çıkış yok.",
        AllExplored = "Her yer keşfedildi.",
        Rechecking = "Çıkış bulunamadı. Çıkmaz sokaklar yeniden kontrol ediliyor.",
        ToQuestObjective = "Görev hedefine doğru.",
        ToDungeonObjective = "Zindan hedefine doğru.",
        ToUnexplored = "Keşfedilmemiş alana doğru.",
        QuestObjectiveBelow = "Hedef daha aşağıda.",
        QuestObjectiveAbove = "Hedef daha yukarıda.",
        Directions = Map("yukarı", "yukarı sağ", "sağ", "aşağı sağ", "aşağı", "aşağı sol", "sol", "yukarı sol"),
        Labels = new() { [RoomKind.Junction] = "Kavşak.", [RoomKind.BigRoom] = "Büyük oda.", [RoomKind.Corridor] = "Koridor." },
    };
}
