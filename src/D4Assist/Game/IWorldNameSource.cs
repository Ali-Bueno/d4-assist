namespace D4Assist.Game;

/// <summary>What GameSession needs from WorldLogWatcher — lets tests inject a fake log feed.</summary>
public interface IWorldNameSource
{
    event Action<string>? WorldChanged;

    string? ReadLastWorldNameAtStartup();
}
