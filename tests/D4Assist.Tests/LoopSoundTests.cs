using D4Assist.Audio;
using D4Assist.Game;

namespace D4Assist.Tests;

public class LoopSoundTests
{
    [Fact]
    public void Every_loop_sound_id_is_an_embedded_wav()
    {
        var sounds = SpatialLoopPlayer.LoadSounds();
        string[] ids =
        [
            LoopTargetSelector.QuestItemSound, LoopTargetSelector.EnticementSound, LoopTargetSelector.SafeZoneSound,
            LoopTargetSelector.StolenSkillSound, LoopTargetSelector.AkaratLightSound, LoopTargetSelector.WardBreakerSound,
            InteractableClassifier.WoodenDoorSound, InteractableClassifier.MetalDoorSound, InteractableClassifier.StoneDoorSound,
            InteractableClassifier.ChestSound, InteractableClassifier.ScrollSound, InteractableClassifier.CorpseSound,
        ];

        Assert.All(ids, id => Assert.True(sounds.ContainsKey(id), $"no {id}.wav under D4Assist.Audio/Sounds"));
    }
}
