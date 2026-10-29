using D4Assist.Audio;

namespace D4Assist.Config;

/// <summary>A set of sound cues that share a switch, a volume and a repeat speed on top of each cue's own.</summary>
public enum SoundGroup { Radar, Objects, Quest, Guide, Characters, Danger }

/// <summary>Every sound cue the user can switch off, level and, when it repeats, speed up or slow down.</summary>
public enum SoundCue
{
    RadarExit, RadarOpen, RadarWall, RadarDoor,
    RadarFront, RadarRight, RadarBack, RadarLeft,
    Door, Chest, Corpse, Readable, PointOfInterest, HealingWell,
    Portal, Waypoint, QuestObjective, Prisoner, Enticement, SafeZone, StolenSkill, AkaratLight, WardBreaker,
    GuidePing, GuideArrival,
    Npc, Boss, Elite,
    Telegraph,
}

/// <summary>A switch, a volume and a repeat speed: one group's or one cue's entry in d4assist.json.</summary>
public sealed class CueSetting
{
    /// <summary>Volume is a percentage of the cue's designed level (0-100).</summary>
    public const int MinVolume = 0, MaxVolume = 100;

    /// <summary>Speed is a percentage of the cue's designed repeat rate: 25 = four times slower, 300 = three times faster.</summary>
    public const int MinSpeed = 25, NormalSpeed = 100, MaxSpeed = 300;

    public bool Enabled { get; set; } = true;
    public int Volume { get; set; } = MaxVolume;
    public int Speed { get; set; } = NormalSpeed;

    public CueSetting Clone() => new() { Enabled = Enabled, Volume = Volume, Speed = Speed };
}

/// <summary>The "Sound" section of d4assist.json: master volume, preview switch, and one entry per group and per cue.</summary>
/// <remarks>Entries are keyed by enum name and missing ones mean the defaults, so older or hand-edited files still load.</remarks>
public sealed class SoundSettings
{
    private Dictionary<string, CueSetting> _groups = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CueSetting> _cues = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Half the designed level by default: the user's choice over the full level (2026-10-01).</summary>
    public const int DefaultMasterVolume = 50;

    public int MasterVolume { get; set; } = DefaultMasterVolume;

    /// <summary>Play a cue while its volume or speed is being changed in the settings window.</summary>
    public bool Preview { get; set; } = true;

    /// <summary>How many of the nearest characters to talk to tap at once (1 to NpcCuePlayer.MaxVoices).</summary>
    public int NpcVoices { get; set; } = NpcCuePlayer.DefaultVoices;

    public Dictionary<string, CueSetting> Groups
    {
        get => _groups;
        set => _groups = new(value ?? [], StringComparer.OrdinalIgnoreCase); // "Groups": null = the defaults
    }

    public Dictionary<string, CueSetting> Cues
    {
        get => _cues;
        set => _cues = new(value ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The group's entry, created with the defaults when missing.</summary>
    public CueSetting Group(SoundGroup group) => Entry(_groups, group.ToString());

    /// <summary>The cue's entry, created with the defaults when missing.</summary>
    public CueSetting Cue(SoundCue cue) => Entry(_cues, cue.ToString(), DefaultVolume(cue));

    // The scroll loop on books, notes and signs sounds at half its designed level by default (user, 2026-10-01).
    private static int DefaultVolume(SoundCue cue) => cue == SoundCue.Readable ? CueSetting.MaxVolume / 2 : CueSetting.MaxVolume;

    public SoundSettings Clone() => new()
    {
        MasterVolume = MasterVolume,
        Preview = Preview,
        NpcVoices = NpcVoices,
        Groups = _groups.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
        Cues = _cues.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
    };

    /// <summary>Every group and cue entry present (defaults filled in), so the saved file lists them all.</summary>
    public SoundSettings Complete()
    {
        foreach (var group in Enum.GetValues<SoundGroup>()) Group(group);
        foreach (var cue in Enum.GetValues<SoundCue>()) Cue(cue);
        return this;
    }

    private static CueSetting Entry(Dictionary<string, CueSetting> entries, string key, int volume = CueSetting.MaxVolume)
    {
        if (!entries.TryGetValue(key, out var entry) || entry is null) entries[key] = entry = new CueSetting { Volume = volume };
        return entry;
    }
}
