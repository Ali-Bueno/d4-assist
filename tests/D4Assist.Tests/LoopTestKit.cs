using D4Assist.Audio;
using D4Assist.Game;

namespace D4Assist.Tests;

internal static class LoopTestKit
{
    private static readonly string[] SoundIds =
    [
        LoopTargetSelector.QuestItemSound, LoopTargetSelector.EnticementSound, LoopTargetSelector.SafeZoneSound, LoopTargetSelector.WaypointSound,
        InteractableClassifier.CorpseSound, InteractableClassifier.ChestSound, InteractableClassifier.ScrollSound,
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
    public void RaiseTiles(params TileState[] tiles) => TilesUpdated?.Invoke(tiles);
}

internal static class ObjectiveMemoryTestKit
{
    /// <summary>One actor poll where the given objective actors loop and nothing else stands around.</summary>
    public static void Heard(this ObjectiveMemory memory, params ActorState[] looping) =>
        memory.Observe(new ObjectiveSighting(looping, [], looping));

    /// <summary>Hears only <paramref name="looping"/> for as long as an objective missing from the actors takes to read done.</summary>
    public static void Gone(this ObjectiveMemory memory, params ActorState[] looping)
    {
        for (int i = 0; i < ObjectiveMemory.MissingPollsForDone; i++) memory.Heard(looping);
    }
}
