namespace D4Assist.Game;

/// <summary>What a world is to the assistant.</summary>
public enum WorldKind
{
    /// <summary>Anything else (menus, loading screens, or everything outside dungeons when that is switched off): no memory reads.</summary>
    Other,
    /// <summary>A DRLG dungeon: full assistant (room announcements, radar, loops, exploration guide).</summary>
    Dungeon,
    /// <summary>A non-DRLG gameplay world other than the open world (or one listed in RadarOnlyWorlds): radar and actor
    /// loops only, no announcements, no guide.</summary>
    RadarOnly,
    /// <summary>The open world (towns, roads): player and actor reads for beacons, objective loops and quest routes only;
    /// no tiles, radar, announcements or guide.</summary>
    Overworld,
}

/// <summary>What GameSession needs from WorldClassifier — lets tests inject a classification without CASC data.</summary>
public interface IWorldClassifier
{
    (int WorldSno, WorldKind Kind) Classify(string worldName);
}
