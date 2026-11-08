using System.Numerics;
using D4Assist.Audio;
using D4Assist.Game;
using D4Assist.Input;
using D4Assist.Navigation;
using D4Assist.Speech;
using D4Assist.Strings;
using D4Memory.Actors;

namespace D4Assist.Tests;

/// <summary>The guide inside a legion event: the Dry Steppes legion of 2026-11-04 07:45 (live log), its targets as the map marked them.</summary>
public class LegionGuideTests
{
    private const string World = "Sanctuary_Eastern_Continent";
    private const int EventArea = 1, OtherArea = 2;
    private const int Boss = 145695, Shrine = 1093402, ShrineSpawner = 1092805, Idol = 971924, FallenCaptain = 1092023,
        Demon = 205815, Bonfire = 1017563, Chest = 1048395, Cellar = 9,
        IslandArea = 3, HighHp = 2243107, Building = 2138852, PingMiddle = 2164144, PingSouth = 2164146, PingWest = 2164148,
        PingNorth = 2164150, Guard = 2411902, Catapults = 2181327, Pirate = 2188923;
    private static readonly Dictionary<int, string> Names = new()
    {
        [EventArea] = "Step_ZoneEvent", [OtherArea] = "Step_WesternFlats",
        [Boss] = "Goatman_abom_boss", [Shrine] = "Structure_Goatman_CorruptedShrine", [ShrineSpawner] = "ZE_Invasion_Demon_GoatmenStructure_Spawner",
        [Idol] = "Structure_Fallen_IdolOfTheShaman", [FallenCaptain] = "fallen_brute_unique_ACD_ZE_Invasion_Demon_FallenCaptain",
        [Demon] = "demon_swarmer_phys", [Bonfire] = "bonfire_restingSpot", [Chest] = "ZE_Chest_Reward",
        [IslandArea] = "Skov_ZoneEvent", [HighHp] = "ZE_IslandDefense_Structure_HighHP", [Building] = "ZE_IslandDefense_Structure",
        [PingMiddle] = "ZE_IslandDefense_MapPing", [PingSouth] = "ZE_IslandDefense_MapPing_1", [PingWest] = "ZE_IslandDefense_MapPing_2",
        [PingNorth] = "ZE_IslandDefense_MapPing_3", [Guard] = "ZE_IslandDefense_AmazonGuard",
        [Catapults] = "ZE_IslandDefense_spawner_catapult", [Pirate] = "pirate_ranged_dreg_miniboss",
    };
    private static readonly LegionTargets Targets = new(Names.GetValueOrDefault);
    private static readonly LegionStrings English = LegionStrings.English;

    private static readonly Vector2 Center = new(-634f, -1163f);
    private static readonly Vector2 ShrineAt = new(-656.2f, -1141.7f);
    private static readonly Vector2 IdolAt = new(-654.3f, -1101.7f);
    private static readonly Vector2 CaptainAt = new(-643.8f, -1105f);

    private static MarkedActor Marker(int sno, Vector2 at, int kind = MapMarkerReader.KindBoss, int area = EventArea) => new(sno, at.X, at.Y, 9, area, kind);

    [Theory]
    [InlineData("Goatman_abom_boss", MapMarkerReader.KindBoss, LegionTargetKind.Boss)]
    [InlineData("Structure_Demon_DemonicSpire", MapMarkerReader.KindBoss, LegionTargetKind.Structure)]
    [InlineData("demon_mega_unique_ACD_ZE_Invasion_Demon_DemonCaptain", MapMarkerReader.KindBoss, LegionTargetKind.Captain)]
    [InlineData("ZE_Chest_Reward", MapMarkerReader.KindGizmo, LegionTargetKind.Chest)]
    [InlineData("ZE_Chest_Reward_Tier2", MapMarkerReader.KindGizmo, LegionTargetKind.Chest)]
    [InlineData("ZE_Invasion_Demon_DemonStructure_Spawner", MapMarkerReader.KindGizmo, null)]
    [InlineData("ZE_Invasion_Demon_DemonCaptain_Spawner", MapMarkerReader.KindGizmo, null)]
    [InlineData("demon_succubus_phys", MapMarkerReader.KindBoss, null)]
    [InlineData("bonfire_restingSpot_ignite", MapMarkerReader.KindGizmo, null)]
    [InlineData("ZE_IslandDefense_MapPing_2", MapMarkerReader.KindGizmo, LegionTargetKind.Attack)]
    [InlineData("ZE_TowerDefense_MapPing_4", 9, LegionTargetKind.Attack)]
    [InlineData("ZE_IslandDefense_Structure", 9, LegionTargetKind.Defend)]
    [InlineData("ZE_IslandDefense_Structure_HighHP", 9, LegionTargetKind.Defend)]
    [InlineData("ZE_Towerdefense_Inhibitor_3", 9, LegionTargetKind.Defend)]
    [InlineData("ZE_TowerDefense_InhibitorTrigger", MapMarkerReader.KindGizmo, null)]
    [InlineData("ZE_IslandDefense_spawner_catapult", MapMarkerReader.KindGizmo, null)]
    [InlineData("ZE_IslandDefense_AmazonGuard", 8, null)]
    [InlineData("ZE_IslandDefense_GolemPing", MapMarkerReader.KindGizmo, null)]
    [InlineData("pirate_ranged_dreg_miniboss", MapMarkerReader.KindBoss, LegionTargetKind.Boss)]
    public void A_marker_is_a_legion_target_by_its_name(string name, int markerKind, LegionTargetKind? expected) =>
        Assert.Equal(expected, LegionTargets.KindOf(name, markerKind));

    [Fact]
    public void Only_markers_in_the_event_area_near_the_player_count_chests_last()
    {
        var markers = new[]
        {
            Marker(Chest, Center + new Vector2(1, 1), MapMarkerReader.KindGizmo),
            Marker(Idol, IdolAt), Marker(Shrine, ShrineAt), Marker(ShrineSpawner, ShrineAt, MapMarkerReader.KindGizmo),
            Marker(Demon, Center), Marker(Bonfire, Center, MapMarkerReader.KindGizmo),
            Marker(Boss, Center + new Vector2(0, 500)),                      // beyond the range: another event
            Marker(Shrine, Center + new Vector2(5, 5), area: OtherArea),     // not the event's area
        };

        var near = Targets.Near(markers, Center);

        Assert.Equal([(LegionTargetKind.Structure, ShrineAt), (LegionTargetKind.Structure, IdolAt), (LegionTargetKind.Chest, Center + new Vector2(1, 1))],
            near.Select(t => (t.Kind, t.Position)));
    }

    private sealed class Rig
    {
        public Feed Feed = new();
        public Speech Speech = new();
        public Pad Pad = new();
        public AudioMixer Mixer = new();
        public GuidePingPlayer Ping;
        public QuestPathBridge Bridge;
        public double Now;
        public TimeSpan Clock;
        public List<Vector2> Searches = [];
        private Vector2 _last;

        public Rig()
        {
            Ping = new GuidePingPlayer(Mixer, () => Now, autoTick: false);
            var cellar = new QuestMapPlace(0, "Family Cellar", Cellar, new(-609f, -1051f), Entrance: true);
            Bridge = new QuestPathBridge(Feed, w => w == World, _ => [], (_, from, to) => { Searches.Add(to); return [to]; },
                Ping, Speech, new LanguageSetting(SpeechLanguage.English), Pad, runInBackground: work => work(), clock: () => Clock,
                entrances: (_, _, _) => [cellar], legionTargets: Targets.Near);
            Feed.RaiseWorld(World);
        }

        public void Markers(params MarkedActor[] markers) => Bridge.OnMapMarkers(markers);

        public void Wait(double seconds) => Clock += TimeSpan.FromSeconds(seconds);

        public void Move(Vector2 p) { Now += 0.1; Clock += TimeSpan.FromSeconds(0.1); _last = p; Feed.RaisePlayer(p); }

        public bool Pings()
        {
            Now += 2;
            Mixer.Render(new short[AudioFormat.SampleRate * AudioFormat.Channels], AudioFormat.SampleRate);
            Feed.RaisePlayer(_last);
            Ping.Tick();
            var buffer = new short[AudioFormat.SampleRate / 10 * AudioFormat.Channels];
            Mixer.Render(buffer, buffer.Length / AudioFormat.Channels);
            return buffer.Any(s => s != 0);
        }
    }

    [Fact]
    public void D_pad_left_in_a_legion_leads_to_its_nearest_target_before_any_cellar()
    {
        var rig = new Rig();
        rig.Markers(Marker(Shrine, ShrineAt), Marker(Idol, IdolAt));
        rig.Move(Center);

        rig.Pad.Press();

        Assert.Equal("Legion: Structure, 31 meters. 1 of 3.", rig.Speech.Texts.Last());
        Assert.Equal(ShrineAt, rig.Searches.Last());
        Assert.True(rig.Pings());
    }

    [Fact]
    public void At_a_target_it_holds_and_leads_on_by_itself_once_the_map_stops_marking_it()
    {
        var rig = new Rig();
        rig.Markers(Marker(Shrine, ShrineAt), Marker(Idol, IdolAt));
        rig.Move(Center);
        rig.Pad.Press();

        rig.Move(ShrineAt + new Vector2(2, 0));
        Assert.Equal(English.Reached, rig.Speech.Texts.Last());
        Assert.False(rig.Pings()); // fighting there: no ping

        rig.Markers(Marker(Idol, IdolAt)); // the shrine falls
        Assert.Equal(English.Reached, rig.Speech.Texts.Last()); // one read without it may be torn
        rig.Wait(2);
        rig.Markers(Marker(Idol, IdolAt));

        Assert.Equal("Legion: Structure, 40 meters.", rig.Speech.Texts.Last());
        Assert.Equal(IdolAt, rig.Searches.Last());
        Assert.True(rig.Pings());
    }

    [Fact]
    public void Stepping_away_from_a_held_target_leads_back_to_it()
    {
        var rig = new Rig();
        rig.Markers(Marker(Shrine, ShrineAt));
        rig.Move(Center);
        rig.Pad.Press();
        rig.Move(ShrineAt);
        Assert.False(rig.Pings());

        rig.Move(ShrineAt + new Vector2(0, -20));

        Assert.True(rig.Pings());
    }

    [Fact]
    public void A_walking_captain_is_followed_and_the_last_target_gone_says_so()
    {
        var rig = new Rig();
        rig.Markers(Marker(FallenCaptain, CaptainAt));
        rig.Move(Center);
        rig.Pad.Press();
        Assert.Equal("Legion: Captain, 59 meters. 1 of 2.", rig.Speech.Texts.Last());

        rig.Wait(4);
        var walked = CaptainAt + new Vector2(-15, 5);
        rig.Markers(Marker(FallenCaptain, walked));
        Assert.Equal(walked, rig.Searches.Last());

        rig.Markers();
        rig.Wait(2);
        rig.Markers();

        Assert.Equal(English.Done, rig.Speech.Texts.Last());
        Assert.False(rig.Pings());
    }

    // Skovos' island defense, its marker set ZE_IslandDefense placed at (2406,-194) (the miniboss's spot in quest ZE_IslandDefense).
    private static readonly Vector2 Island = new(2406f, -194f);
    private static Vector2 OnIsland(float x, float y) => Island + new Vector2(x, y);
    private static MarkedActor IslandMarker(int sno, Vector2 at, int kind = 9) => new(sno, at.X, at.Y, 5, IslandArea, kind);
    private static readonly Vector2 HighHpAt = OnIsland(8, 6), NorthPingAt = OnIsland(-18, 28), WestPingAt = OnIsland(-29, 5),
        NorthBuilding = OnIsland(-18, 22), SouthBuilding = OnIsland(-24, -16);

    [Fact]
    public void In_a_defense_legion_a_spot_under_attack_comes_first_with_its_side_and_the_buildings_wait()
    {
        var markers = new[]
        {
            IslandMarker(HighHp, HighHpAt), IslandMarker(Building, NorthBuilding), IslandMarker(Building, SouthBuilding),
            IslandMarker(Guard, OnIsland(-21, 31), 8), IslandMarker(Catapults, OnIsland(-29, 42), MapMarkerReader.KindGizmo),
            IslandMarker(PingNorth, NorthPingAt, MapMarkerReader.KindGizmo), IslandMarker(PingWest, WestPingAt, MapMarkerReader.KindGizmo),
            IslandMarker(Pirate, OnIsland(-4, -2), MapMarkerReader.KindBoss),
        };

        var near = Targets.Near(markers, HighHpAt);

        Assert.Equal([LegionTargetKind.Attack, LegionTargetKind.Attack, LegionTargetKind.Boss], near.Select(t => t.Kind));
        Assert.Equal(NorthPingAt, near[0].Position); // 34 off, the west ping 37: nearest first
        Assert.Equal(ScreenDirections.Bin(NorthPingAt.X - HighHpAt.X, NorthPingAt.Y - HighHpAt.Y), near[0].Side);
        Assert.Equal(ScreenDirections.Bin(WestPingAt.X - HighHpAt.X, WestPingAt.Y - HighHpAt.Y), near[1].Side);
        Assert.NotEqual(near[0].Side, near[1].Side);
    }

    [Fact]
    public void The_middle_s_own_ping_has_no_side_and_with_no_ping_the_nearest_building_is_the_goal()
    {
        var pinged = Targets.Near([IslandMarker(HighHp, HighHpAt), IslandMarker(PingMiddle, OnIsland(8, 5), MapMarkerReader.KindGizmo)], Island);
        Assert.Equal([(LegionTargetKind.Attack, (StickDirection?)null)], pinged.Select(t => (t.Kind, t.Side)));

        var calm = Targets.Near([IslandMarker(HighHp, HighHpAt), IslandMarker(Building, NorthBuilding), IslandMarker(Building, SouthBuilding)],
            SouthBuilding + new Vector2(0, -5));
        Assert.Equal([SouthBuilding, HighHpAt, NorthBuilding], calm.Select(t => t.Position));
        Assert.All(calm, t => Assert.Equal(LegionTargetKind.Defend, t.Kind));
    }

    [Fact]
    public void The_debug_line_lists_every_marker_in_the_legion_area_only()
    {
        var line = Targets.Describe([IslandMarker(Guard, OnIsland(-21, 31), 8), Marker(Shrine, Island + new Vector2(3, 3), area: OtherArea)], Island);

        Assert.Equal("ZE_IslandDefense_AmazonGuard kind 8 flags 0x0 (2385.0,-163.0) 37", line);
        Assert.Null(Targets.Describe([Marker(Shrine, Island, area: OtherArea)], Island));
    }

    [Fact]
    public void D_pad_left_says_the_attacked_side_and_a_building_gives_way_once_a_spot_is_attacked()
    {
        var rig = new Rig();
        var side = NavigationPhrases.For(SpeechLanguage.English).Direction(ScreenDirections.Bin(WestPingAt.X - HighHpAt.X, WestPingAt.Y - HighHpAt.Y));
        rig.Markers(IslandMarker(HighHp, HighHpAt), IslandMarker(Building, SouthBuilding));
        rig.Move(SouthBuilding + new Vector2(0, -5));
        rig.Pad.Press();
        Assert.StartsWith("Legion: Building to defend, 5 meters.", rig.Speech.Texts.Last());

        rig.Markers(IslandMarker(HighHp, HighHpAt), IslandMarker(Building, SouthBuilding), IslandMarker(PingWest, WestPingAt, MapMarkerReader.KindGizmo));
        rig.Wait(2);
        rig.Markers(IslandMarker(HighHp, HighHpAt), IslandMarker(Building, SouthBuilding), IslandMarker(PingWest, WestPingAt, MapMarkerReader.KindGizmo));

        Assert.StartsWith($"Legion: Attack, {side},", rig.Speech.Texts.Last());
        Assert.Equal(WestPingAt, rig.Searches.Last());
    }

    public sealed class Feed : IDungeonFeed
    {
        public event Action<int, string, bool>? WorldChanged;
        public event Action<IReadOnlyList<TileState>>? TilesUpdated { add { } remove { } }
        public event Action<PlayerState>? PlayerMoved;
        public event Action<IReadOnlyList<ActorState>>? ActorsUpdated { add { } remove { } }
        public (string Name, bool IsDungeon)? CurrentWorld => null;

        public void RaiseWorld(string name) => WorldChanged?.Invoke(1, name, false);
        public void RaisePlayer(Vector2 p) => PlayerMoved?.Invoke(new PlayerState(p.X, p.Y, 0, 0));
    }

    public sealed class Speech : ISpeechSink
    {
        public List<string> Texts { get; } = [];
        public string BackendName => "fake";
        public void Speak(string text, bool interrupt) => Texts.Add(text);
        public void Dispose() { }
    }

    public sealed class Pad : IGuideTogglePad
    {
        public event Action? DpadLeftPressed;
        public void Press() => DpadLeftPressed?.Invoke();
    }
}
