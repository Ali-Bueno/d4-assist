namespace D4Assist.Game;

/// <summary>What GameSession needs from WorldClassifier — lets tests inject a classification without CASC data.</summary>
public interface IWorldClassifier
{
    (int WorldSno, bool IsDungeon) Classify(string worldName);
}
