namespace D4Assist.Game;

/// <summary>What GameSession needs from WorldLogWatcher — lets tests inject a fake log feed.</summary>
public interface IWorldNameSource
{
    event Action<WorldEntry>? WorldChanged;

    WorldEntry? ReadLastWorldAtStartup();
}

/// <summary>One "Client entered world" line: the world name and its per-instance client id (CWorldID; null if absent).</summary>
public sealed record WorldEntry(string Name, string? Instance);
