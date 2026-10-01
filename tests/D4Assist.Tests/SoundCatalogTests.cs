using D4Assist.Audio;
using D4Assist.Config;

namespace D4Assist.Tests;

public class SoundCatalogTests
{
    [Fact]
    public void Every_cue_has_a_group_and_CuesOf_partitions_all_cues()
    {
        var all = Enum.GetValues<SoundCue>();
        var partitioned = Enum.GetValues<SoundGroup>().SelectMany(SoundCatalog.CuesOf).ToList();

        Assert.All(all, cue => Assert.True(Enum.IsDefined(SoundCatalog.GroupOf(cue))));
        Assert.Equal(all.Order(), partitioned.Order());
        Assert.All(Enum.GetValues<SoundGroup>(), group => Assert.NotEmpty(SoundCatalog.CuesOf(group)));
    }

    [Fact]
    public void Repeats_only_for_objects_quest_guide_ping_and_npc()
    {
        SoundCue[] expected =
        [
            SoundCue.Door, SoundCue.Chest, SoundCue.Corpse, SoundCue.Readable, SoundCue.PointOfInterest,
            SoundCue.Portal, SoundCue.QuestObjective, SoundCue.Prisoner, SoundCue.Enticement, SoundCue.SafeZone, SoundCue.StolenSkill, SoundCue.AkaratLight, SoundCue.WardBreaker, SoundCue.GuidePing, SoundCue.Npc,
        ];

        Assert.Equal(expected.Order(), Enum.GetValues<SoundCue>().Where(SoundCatalog.Repeats).Order());
    }

    [Fact]
    public void A_group_repeats_when_any_of_its_cues_does()
    {
        Assert.False(SoundCatalog.Repeats(SoundGroup.Radar));
        Assert.All([SoundGroup.Objects, SoundGroup.Quest, SoundGroup.Guide, SoundGroup.Characters], g => Assert.True(SoundCatalog.Repeats(g)));
    }

    [Fact]
    public void Radar_rays_round_trip_through_their_direction_cue()
    {
        Assert.All(Enum.GetValues<RadarRay>(), ray =>
        {
            var cue = SoundCatalog.RadarCue(ray);
            Assert.True(SoundCatalog.IsRadarDirection(cue));
            Assert.Equal(ray, SoundCatalog.RayOf(cue));
        });
    }

    [Fact]
    public void Radar_kinds_map_to_distinct_radar_group_cues()
    {
        var cues = Enum.GetValues<RadarCueKind>().Select(SoundCatalog.RadarCue).ToList();

        Assert.Equal(cues.Count, cues.Distinct().Count());
        Assert.All(cues, cue => Assert.Equal(SoundGroup.Radar, SoundCatalog.GroupOf(cue)));
        Assert.All(cues, cue => Assert.False(SoundCatalog.IsRadarDirection(cue)));
    }

    [Fact]
    public void Only_the_four_direction_cues_are_radar_directions()
    {
        Assert.Equal(4, Enum.GetValues<SoundCue>().Count(SoundCatalog.IsRadarDirection));
        Assert.Throws<ArgumentOutOfRangeException>(() => SoundCatalog.RayOf(SoundCue.Chest));
    }
}
