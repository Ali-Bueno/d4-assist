using D4Assist.Audio;
using D4Assist.Game;

namespace D4Assist.Tests;

internal static class LoopTestKit
{
    private static readonly string[] SoundIds =
    [
        LoopTargetSelector.QuestItemSound, InteractableClassifier.CorpseSound, InteractableClassifier.ChestSound, InteractableClassifier.ScrollSound,
        InteractableClassifier.WoodenDoorSound, InteractableClassifier.MetalDoorSound, InteractableClassifier.StoneDoorSound,
    ];

    /// <summary>A loop player with a stub for every loop sound, so sources within reach start a loop.</summary>
    public static SpatialLoopPlayer Player() =>
        new(new AudioMixer(), SoundIds.ToDictionary(id => id, _ => Enumerable.Repeat(0.5f, 480).ToArray()));
}

internal sealed class FakeFeed : IDungeonFeed
{
    public event Action<int, string, bool>? WorldChanged;
    public event Action<IReadOnlyList<TileState>>? TilesUpdated;
    public event Action<PlayerState>? PlayerMoved;
    public event Action<IReadOnlyList<ActorState>>? ActorsUpdated;
    public (string Name, bool IsDungeon)? CurrentWorld => null;

    public void RaiseWorld(int worldSno = 0) => WorldChanged?.Invoke(worldSno, "w", false);
    public void RaisePlayer(PlayerState p) => PlayerMoved?.Invoke(p);
    public void RaiseActors(params ActorState[] actors) => ActorsUpdated?.Invoke(actors);
    public void RaiseTiles() => TilesUpdated?.Invoke([]);
}
