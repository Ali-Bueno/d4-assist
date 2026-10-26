using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;

namespace D4Assist.Ui;

/// <summary>Plays one cue (or a group's typical cue) at the level being set in the settings window, on the mixer's
/// preview voice; a repeating cue plays a few times at its chosen speed.</summary>
/// <param name="loopSounds">The actor-loop WAVs by sound id; null when they failed to load (those previews stay silent).</param>
public sealed class CuePreview(AudioMixer mixer, IReadOnlyDictionary<string, float[]>? loopSounds, RadarFrame frame)
{
    // A repeating preview lasts about this long, in at least two and at most six repetitions.
    private const double RepeatPreviewSeconds = 2.5;
    private const int MinRepeats = 2, MaxRepeats = 6;

    /// <summary>The ping and tap repeat at their mid-range cadence: half the distance over which they speed up.</summary>
    private static readonly double GuideIntervalSeconds = GuidePingPlayer.IntervalSeconds(GuidePingPlayer.FarDistanceUnits / 2);
    private static readonly double NpcIntervalSeconds = NpcCuePlayer.IntervalSeconds(NpcCuePlayer.DefaultRangeUnits / 2);
    private static readonly double EnemyIntervalSeconds = EnemyCuePlayer.IntervalSeconds(EnemyCuePlayer.RangeUnits / 2);

    /// <summary>The cue a group's own volume and speed are heard on.</summary>
    public static SoundCue TypicalCue(SoundGroup group) => group switch
    {
        SoundGroup.Radar => SoundCue.RadarExit,
        SoundGroup.Objects => SoundCue.Chest,
        SoundGroup.Quest => SoundCue.QuestObjective,
        SoundGroup.Guide => SoundCue.GuidePing,
        _ => SoundCue.Npc,
    };

    public void PlayCue(SoundCue cue, SoundMix mix) => Play(cue, mix.Level(cue));

    public void PlayGroup(SoundGroup group, SoundMix mix) => Play(TypicalCue(group), mix.GroupLevel(group));

    /// <summary>The master volume is heard on the quest loop, the cue most players know.</summary>
    public void PlayMaster() => Play(SoundCue.QuestObjective, CueLevel.Full);

    public void Stop() => mixer.StopPreview();

    private void Play(SoundCue cue, CueLevel level)
    {
        if (level.Gain <= 0) { mixer.StopPreview(); return; }
        if (Render(cue, level.Rate) is not var (samples, gain, pan) || samples.Length == 0) return;
        mixer.PlayPreview(samples, gain * level.Gain, pan);
    }

    // Samples, designed gain (before the user's) and pan of one cue's preview.
    private (float[] Samples, double Gain, double Pan)? Render(SoundCue cue, double rate)
    {
        const double Boost = RadarCuePlayer.LoudnessBoost;
        switch (cue)
        {
            case SoundCue.RadarExit: return (RadarCueSynth.Synthesize(RadarCueKind.Exit, 0, unexplored: false), Boost, 0);
            case SoundCue.RadarOpen: return (RadarCueSynth.Synthesize(RadarCueKind.Open, 0, unexplored: false), Boost, 0);
            case SoundCue.RadarWall: return (RadarCueSynth.Synthesize(RadarCueKind.Blocked, 0, unexplored: false), Boost, 0);
            case SoundCue.RadarDoor: return (RadarCueSynth.Synthesize(RadarCueKind.Door, 0, unexplored: false), Boost, 0);
            case SoundCue.RadarFront or SoundCue.RadarRight or SoundCue.RadarBack or SoundCue.RadarLeft:
                var ray = SoundCatalog.RayOf(cue); // a wall on that side: the radar's most common sound
                return (RadarCueSynth.Synthesize(RadarCueKind.Blocked, RadarDirectionModel.Vertical(ray, frame), unexplored: false),
                    Boost, RadarDirectionModel.Pan(ray, frame));
            case SoundCue.GuidePing: return (Repeated(GuidePingSynth.Synthesize(1.0), GuideIntervalSeconds / rate), Boost, 0);
            case SoundCue.GuideArrival: return (GuidePingSynth.SynthesizeArrived(), Boost, 0);
            case SoundCue.Npc: return (Repeated(NpcCueSynth.Synthesize(1.0), NpcIntervalSeconds / rate), Boost, 0);
            case SoundCue.Boss: return (Repeated(EnemyCueSynth.Synthesize(EnemyClass.Boss, 1.0), EnemyIntervalSeconds / rate), Boost, 0);
            case SoundCue.Elite: return (Repeated(EnemyCueSynth.Synthesize(EnemyClass.Elite, 1.0), EnemyIntervalSeconds / rate), Boost, 0);
        }
        var (sound, profile) = Loop(cue);
        if (loopSounds?.GetValueOrDefault(sound) is not { } loop) return null;
        return (Repeated(loop, (double)loop.Length / AudioFormat.SampleRate / rate), profile.NearGain, 0);
    }

    // The loop sound and distance profile the game uses for each actor-loop cue, heard at arm's length.
    private static (string Sound, LoopProfile Profile) Loop(SoundCue cue) => cue switch
    {
        SoundCue.Door => (InteractableClassifier.WoodenDoorSound, LoopProfiles.Container),
        SoundCue.Chest => (InteractableClassifier.ChestSound, LoopProfiles.Container),
        SoundCue.Corpse => (InteractableClassifier.CorpseSound, LoopProfiles.Container),
        SoundCue.Readable or SoundCue.PointOfInterest => (InteractableClassifier.ScrollSound, LoopProfiles.Container),
        SoundCue.HealingWell => (InteractableClassifier.HealingWellSound, LoopProfiles.Container),
        SoundCue.Portal => (LoopTargetSelector.QuestItemSound, LoopProfiles.Beacon),
        SoundCue.Waypoint => (LoopTargetSelector.WaypointSound, LoopProfiles.Beacon),
        SoundCue.Prisoner => (InteractableClassifier.CorpseSound, LoopProfiles.Quest),
        SoundCue.Enticement => (LoopTargetSelector.EnticementSound, LoopProfiles.Quest),
        SoundCue.SafeZone => (LoopTargetSelector.SafeZoneSound, LoopProfiles.Beacon),
        SoundCue.StolenSkill => (LoopTargetSelector.StolenSkillSound, LoopProfiles.Quest),
        SoundCue.AkaratLight => (LoopTargetSelector.AkaratLightSound, LoopProfiles.Quest),
        SoundCue.WardBreaker => (LoopTargetSelector.WardBreakerSound, LoopProfiles.Quest),
        _ => (LoopTargetSelector.QuestItemSound, LoopProfiles.Quest),
    };

    private static float[] Repeated(float[] sound, double periodSeconds)
    {
        int count = Math.Clamp((int)Math.Ceiling(RepeatPreviewSeconds / periodSeconds), MinRepeats, MaxRepeats);
        return CueRepeat.Render(sound, periodSeconds, count);
    }
}
