using D4Assist.Audio;
using D4Assist.Game;

namespace D4Assist.Tests;

public class CutscenePauseTests
{
    private static (CutscenePause Pause, List<bool> Applied) Create()
    {
        var applied = new List<bool>();
        return (new CutscenePause(applied.Add), applied);
    }

    [Fact]
    public void Playback_PausesOnce_PreloadingDoesNot()
    {
        var (pause, applied) = Create();

        pause.OnCutsceneState("PRELOADING");
        Assert.False(pause.IsPaused);

        pause.OnCutsceneState(CutscenePause.PlaybackState);
        pause.OnCutsceneState(CutscenePause.PlaybackState);

        Assert.True(pause.IsPaused);
        Assert.Equal([true], applied);
    }

    [Fact]
    public void AWorldChange_Resumes()
    {
        var (pause, applied) = Create();
        pause.OnCutsceneState(CutscenePause.PlaybackState);

        pause.OnWorldChanged();

        Assert.False(pause.IsPaused);
        Assert.Equal([true, false], applied);
    }

    [Fact]
    public void StandingStill_KeepsThePause_WalkingACellResumes()
    {
        var (pause, _) = Create();
        pause.OnCutsceneState(CutscenePause.PlaybackState);

        pause.OnPlayer(new PlayerState(10, 10, 0, 0));
        pause.OnPlayer(new PlayerState(10.3f, 10.3f, 0, 0));
        Assert.True(pause.IsPaused);

        pause.OnPlayer(new PlayerState(11, 10, 0, 0));
        Assert.False(pause.IsPaused);
    }

    [Fact]
    public void AnUnloadedWorldPositionIsNotAStep()
    {
        var (pause, _) = Create();
        pause.OnCutsceneState(CutscenePause.PlaybackState);
        pause.OnPlayer(new PlayerState(10, 10, 0, 0));

        pause.OnPlayer(new PlayerState(0, 0, 0, 0));

        Assert.True(pause.IsPaused);
    }

    [Fact]
    public void WithoutACutscene_NothingIsApplied()
    {
        var (pause, applied) = Create();

        pause.OnWorldChanged();
        pause.OnPlayer(new PlayerState(1, 1, 0, 0));

        Assert.Empty(applied);
    }

    [Fact]
    public void PausedMixer_RendersSilence_DropsCues_AndResumesItsLoops()
    {
        var mixer = new AudioMixer();
        var tone = Enumerable.Repeat(0.5f, 480).ToArray();
        var loop = mixer.StartLoop(tone, 1, 0)!;
        mixer.Paused = true;
        mixer.Play(tone, 1, 0);
        mixer.PlayGuide(tone, 1, 0);

        var buffer = new short[480 * AudioFormat.Channels];
        mixer.Render(buffer, 480);

        Assert.All(buffer, s => Assert.Equal(0, s));
        Assert.Equal(0, mixer.ActiveVoiceCount);
        Assert.Equal(0, mixer.ActiveGuideVoiceCount);
        Assert.Equal(1, mixer.ActiveLoopCount);

        mixer.Paused = false;
        mixer.Render(buffer, 480);
        Assert.Contains(buffer, s => s != 0);
        loop.Stop();
    }
}
