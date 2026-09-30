using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

/// <summary>ActorLoopBridge applying the sound settings' per-cue gain and rate (levelOf).</summary>
public class LoopLevelTests
{
    private const int PadSno = 1, ChestSno = 2, CorpseChestSno = 3, World = 7, SoundFrames = 4800;

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [PadSno] = ("Pad", new(2, 0)),
        [ChestSno] = ("ProtoDunKeep_Chest_01", new(2, Conventions.GizmoChest)),
        [CorpseChestSno] = ("corpse_knight_melee_frac", new(2, Conventions.GizmoChest)),
    };

    private static string? Name(int sno) => Actors.TryGetValue(sno, out var a) ? a.Name : null;

    private static (FakeFeed Feed, SpatialLoopPlayer Player, AudioMixer Mixer) Create(Func<SoundCue, CueLevel>? levelOf)
    {
        var feed = new FakeFeed();
        var mixer = new AudioMixer();
        var sounds = new[]
        {
            LoopTargetSelector.QuestItemSound, InteractableClassifier.ChestSound, InteractableClassifier.CorpseSound,
        }.ToDictionary(id => id, _ => Enumerable.Repeat(0.5f, SoundFrames).ToArray());
        var player = new SpatialLoopPlayer(mixer, sounds);
        var selector = new LoopTargetSelector(
            new HashSet<int> { PadSno }, null, _ => new HashSet<int>(),
            new InteractableClassifier(sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, Name));
        selector.SetWorld(World, isDungeon: true);
        _ = new ActorLoopBridge(feed, player, selector, Name, levelOf: levelOf);
        feed.RaiseWorld(World);
        return (feed, player, mixer);
    }

    private static void Poll(FakeFeed feed, params ActorState[] actors)
    {
        feed.RaiseActors(actors);
        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
    }

    // Left channel of the next frames.
    private static short[] Render(AudioMixer mixer, int frames)
    {
        var buffer = new short[frames * AudioFormat.Channels];
        mixer.Render(buffer, frames);
        return buffer.Where((_, i) => i % AudioFormat.Channels == 0).ToArray();
    }

    private static short[] Settled(AudioMixer mixer)
    {
        Render(mixer, AudioFormat.SampleRate); // let the gain smoothing settle
        return Render(mixer, SoundFrames * 4);
    }

    [Fact]
    public void A_disabled_cues_actor_gets_no_loop()
    {
        var (feed, player, _) = Create(cue => cue == SoundCue.Chest ? new CueLevel(0, 1) : CueLevel.Full);

        Poll(feed, new ActorState(1, ChestSno, 1, 0, 0), new ActorState(2, PadSno, 0, 1, 0));

        Assert.Equal(1, player.ActiveCount); // only the pad
    }

    [Fact]
    public void Silent_actors_are_dropped_before_the_per_kind_cap()
    {
        var (feed, player, _) = Create(cue => cue == SoundCue.Chest ? new CueLevel(0, 1) : CueLevel.Full);
        int containers = ActorLoopBridge.MaxVoices(LoopKind.Container);
        var silent = Enumerable.Range(0, containers).Select(i => new ActorState((uint)(10 + i), ChestSno, 1 + i, 0, 0));

        Poll(feed, [.. silent, new ActorState(1, CorpseChestSno, 0, 5, 0)]);

        Assert.Equal(1, player.ActiveCount); // the farther corpse, not crowded out by the nearer silent chests
    }

    [Fact]
    public void Switching_a_cue_back_on_brings_its_loop_without_a_new_bridge()
    {
        var chest = CueLevel.Full with { Gain = 0 };
        var (feed, player, _) = Create(cue => cue == SoundCue.Chest ? chest : CueLevel.Full);
        Poll(feed, new ActorState(1, ChestSno, 1, 0, 0));
        Assert.Equal(0, player.ActiveCount);

        chest = CueLevel.Full;
        Poll(feed, new ActorState(1, ChestSno, 1, 0, 0));

        Assert.Equal(1, player.ActiveCount);
    }

    [Fact]
    public void The_gain_scales_the_loops_level()
    {
        var (fullFeed, _, fullMixer) = Create(null);
        var (halfFeed, _, halfMixer) = Create(_ => new CueLevel(0.5, 1));
        Poll(fullFeed, new ActorState(1, PadSno, 0, 0, 0));
        Poll(halfFeed, new ActorState(1, PadSno, 0, 0, 0));

        double full = Settled(fullMixer).Max();
        double half = Settled(halfMixer).Max();

        Assert.InRange(half / full, 0.49, 0.51);
    }

    [Fact]
    public void The_rate_reaches_the_loop()
    {
        var (feed, _, mixer) = Create(_ => new CueLevel(1, 0.5));
        Poll(feed, new ActorState(1, PadSno, 0, 0, 0));

        var left = Settled(mixer);

        Assert.Contains(left, sample => sample > 0);
        Assert.Contains(left, sample => sample == 0); // half rate: a silence after each repetition
    }
}
