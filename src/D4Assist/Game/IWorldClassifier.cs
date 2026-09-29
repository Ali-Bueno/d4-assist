namespace D4Assist.Game;

/// <summary>What a world is to the assistant.</summary>
public enum WorldKind
{
    /// <summary>Anything else: no memory reads.</summary>
    Other,
    /// <summary>A DRLG dungeon: full assistant (room announcements, radar, loops, exploration guide).</summary>
    Dungeon,
    /// <summary>A non-DRLG world listed in the config's RadarOnlyWorlds: radar and actor loops only, no announcements, no guide.</summary>
    RadarOnly,
}

/// <summary>What GameSession needs from WorldClassifier — lets tests inject a classification without CASC data.</summary>
public interface IWorldClassifier
{
    (int WorldSno, WorldKind Kind) Classify(string worldName);
}
