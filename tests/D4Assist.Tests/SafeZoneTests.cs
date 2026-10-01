using D4Assist.Audio;
using D4Assist.Config;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

/// <summary>Boss-fight safe zones: which actors are one, how close counts as inside, and the loop falling silent there.</summary>
public class SafeZoneTests
{
    private const int Trigger = Conventions.GizmoTrigger, NoGizmo = -1, Monster = Conventions.ActorTypeMonster, Gizmo = 2, Fx = 3;
    private const int Ally = 2, Hostile = Conventions.HostileTeam;
    private const int DonanBubble = 1, EliasZone = 2, LilithCaster = 3, ThinWall = 4, EnemyBubble = 5, BubbleFx = 6, PlainTrigger = 7, Pad = 8,
        VigoDome = 9, RaidSwitch = 10, SandstormWisps = 11, IssaliaShrine = 12, LightCarrier = 13;

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [DonanBubble] = ("QST_Hell_Battlefield_Donan_SuppressingBubble", new(Gizmo, Trigger, DefaultTeam: Ally, TriggerRadius: 5.25f)),
        [EliasZone] = ("BCD_Trigger_EliasPalace_SafeZone", new(Gizmo, Trigger, DefaultTeam: 1, TriggerRadius: 5f)),
        [LilithCaster] = ("QST_X2_Lilith_Bubble_Caster", new(Monster, NoGizmo, DefaultTeam: Ally)),
        [ThinWall] = ("Thin_Bubble_Wall_Trigger", new(Gizmo, Trigger, DefaultTeam: Ally, TriggerRadius: 0.167f)),
        [EnemyBubble] = ("Enemy_Bubble_Caster", new(Monster, NoGizmo, DefaultTeam: Hostile)),
        [BubbleFx] = ("QST_Conv_Kehj2_03_Horadrim_Bubble", new(Fx, NoGizmo)),
        [PlainTrigger] = ("Trigger_Sphere_8m", new(Gizmo, Trigger, DefaultTeam: Ally, TriggerRadius: 4f)),
        [Pad] = ("X1_Undercity_WarpPad", new(Gizmo, Conventions.GizmoSwitch)),
        [VigoDome] = ("knight_brute_barrier_border", new(Fx, NoGizmo)),
        [RaidSwitch] = ("X1_Raid_Switch_BetaMiniBoss_SafeZone", new(Gizmo, Conventions.GizmoSwitch)),
        [SandstormWisps] = ("environmentVFX_sandstorm_safeZone_wisps", new(Fx, NoGizmo)),
        [IssaliaShrine] = ("CAP_Tier4_Scos_ArchivesofIssalia_SuppressingShrineTrigger", new(Gizmo, Trigger, DefaultTeam: 1, TriggerRadius: 4f)),
        [LightCarrier] = ("QST_Naha_Lingering_Light", new(Monster, NoGizmo, DefaultTeam: Ally, IsNpc: true)),
    };

    private static string? Name(int sno) => Actors.TryGetValue(sno, out var a) ? a.Name : null;

    private static SafeZoneClassifier Classifier() => new(sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, Name);

    [Theory]
    [InlineData(DonanBubble, 5.25f)]
    [InlineData(EliasZone, 5f)]
    [InlineData(IssaliaShrine, 4f)]
    [InlineData(LilithCaster, SafeZoneClassifier.FallbackRadius)] // no readable size
    [InlineData(VigoDome, SafeZoneClassifier.FallbackRadius)]     // only the dome's visual edge reaches the client
    [InlineData(RaidSwitch, SafeZoneClassifier.FallbackRadius)]
    [InlineData(LightCarrier, SafeZoneClassifier.FallbackRadius)] // a light that moves with its carrier
    public void A_shelter_counts_as_inside_one_margin_within_its_edge(int sno, float radius) =>
        Assert.Equal(radius - SafeZoneClassifier.EdgeMargin, Classifier().InsideRadius(sno));

    [Theory]
    [InlineData(ThinWall)]     // a wall-thin trigger is no place to stand
    [InlineData(EnemyBubble)]  // an enemy's shield
    [InlineData(BubbleFx)]     // a visual, not the zone
    [InlineData(SandstormWisps)] // the visual of a trigger zone that already loops
    [InlineData(PlainTrigger)] // a trigger not named for shelter
    [InlineData(Pad)]
    [InlineData(99)]           // no ActorDefinition record
    public void Other_actors_are_no_safe_zone(int sno) => Assert.Null(Classifier().InsideRadius(sno));

    [Fact]
    public void A_safe_zone_claims_the_loop_budget_first_with_its_own_cue()
    {
        var target = Selector().Select(new ActorState(1, DonanBubble, 0, 0, 0))!;

        Assert.Equal(LoopKind.SafeZone, target.Kind);
        Assert.Equal(SoundCue.SafeZone, target.Cue);
        Assert.Equal(LoopTargetSelector.SafeZoneSound, target.Sound);
        Assert.Equal(5.25f - SafeZoneClassifier.EdgeMargin, target.QuietWithin);
        Assert.False(target.StopsWhenUsed);
        Assert.True(target.Kind < LoopKind.ExitBeacon);
    }

    [Fact]
    public void The_loop_sounds_from_afar_and_falls_silent_inside_the_zone()
    {
        var feed = new FakeFeed();
        var player = LoopTestKit.Player();
        _ = new ActorLoopBridge(feed, player, Selector(), Name);
        feed.RaiseWorld();
        feed.RaiseActors(new ActorState(1, LilithCaster, 20, 0, 0));

        feed.RaisePlayer(new PlayerState(0, 0, 0, 0));
        Assert.Equal(1, player.ActiveCount);

        feed.RaisePlayer(new PlayerState(17, 0, 0, 0)); // 3 units away: inside the 4-unit shelter
        Assert.Equal(0, player.ActiveCount);

        feed.RaisePlayer(new PlayerState(15, 0, 0, 0)); // stepped out again
        Assert.Equal(1, player.ActiveCount);
    }

    private static LoopTargetSelector Selector() =>
        new(new HashSet<int>(), objectives: null, _ => new HashSet<int>(), safeZoneRadius: Classifier().InsideRadius);
}
