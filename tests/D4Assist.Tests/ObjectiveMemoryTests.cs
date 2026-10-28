using D4Assist.Game;
using D4Assist.Navigation;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class ObjectiveMemoryTests
{
    private const int Gizmo = 2;
    private const int LeverSno = 1, AltarSno = 2, ChestSno = 3;
    private const float Far = 200f;

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [LeverSno] = ("QST_Lever", new(Gizmo, Conventions.GizmoQuestSwitch)),
        [AltarSno] = ("QST_Altar", new(Gizmo, Conventions.GizmoQuestSwitch)),
        [ChestSno] = ("Chest_Plain", new(Gizmo, Conventions.GizmoChest)),
    };

    private static string? NameOf(int sno) => Actors.TryGetValue(sno, out var a) ? a.Name : null;

    private static (FakeFeed Feed, ObjectiveMemory Memory) NewMemory()
    {
        var feed = new FakeFeed();
        return (feed, new ObjectiveMemory(feed, NameOf));
    }

    private static ActorState Lever(float x, float y, uint id = 1, bool quest = false) => new(id, LeverSno, x, y, 0, QuestStep: quest);
    private static ActorState Altar(float x, float y, uint id = 2) => new(id, AltarSno, x, y, 0);
    private static PlayerState At(float x, float y) => new(x, y, 0, 0);

    [Fact]
    public void A_looping_objective_is_a_destination_and_a_quest_step_leads_in_the_first_tier()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));

        memory.Heard(Lever(50, 0, quest: true), Altar(0, 50));

        var points = memory.Destinations();
        Assert.Equal(2, points.Count);
        Assert.Equal(ObjectiveMemory.QuestTier, points.Single(p => p.Position == (50, 0)).Tier);
        Assert.Equal(ObjectiveMemory.DungeonTier, points.Single(p => p.Position == (0, 50)).Tier);
    }

    [Fact]
    public void A_light_to_escort_leads_in_the_quest_tier_where_it_is_now_and_not_once_gone()
    {
        // Rathma's archive (live 2026-10-20): the spirit fire lit, a light walks back to the golem chamber with the player inside
        // it, while the step's place at the fire still pointed the guide back.
        const int LightSno = 9;
        var feed = new FakeFeed();
        var memory = new ObjectiveMemory(feed, NameOf, escortRadius: sno => sno == LightSno ? 4f : null);
        feed.RaisePlayer(At(0, 0));
        var light = new ActorState(7, LightSno, 10, 0, 1);

        memory.Observe(new ObjectiveSighting([], [], [light]));
        var lead = Assert.Single(memory.Destinations());
        Assert.Equal((ObjectiveMemory.QuestTier, (10f, 0f), true), (lead.Tier, lead.Position, lead.Located));
        Assert.True(lead.Radius > 4f);

        memory.Observe(new ObjectiveSighting([], [], [light with { X = 30 }]));
        Assert.Equal((30f, 0f), Assert.Single(memory.Destinations()).Position);

        memory.Observe(new ObjectiveSighting([], [], []));
        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void Out_of_earshot_it_stays_and_silent_near_the_player_it_is_done()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Lever(50, 0));

        feed.RaisePlayer(At(-Far, 0));
        memory.Heard();
        Assert.Single(memory.Destinations());

        feed.RaisePlayer(At(50 - ObjectiveMemory.ConfirmRadiusUnits + 1, 0));
        memory.Gone(); // used, locked or removed
        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void An_objective_stays_the_goal_while_the_player_stands_at_it_and_is_done_once_used()
    {
        // Players' reports (2026-10-05): beside the objective, before it was used, the guide turned to unexplored ground.
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Lever(0, 0), Altar(100, 0));

        feed.RaisePlayer(At(1, 0));
        memory.Heard(Lever(0, 0), Altar(100, 0));
        Assert.Contains(memory.Destinations(), d => d.Position == (0, 0));

        memory.Gone(Altar(100, 0)); // pulled
        Assert.Equal((100f, 0f), Assert.Single(memory.Destinations()).Position);
    }

    [Fact]
    public void A_door_the_player_cant_open_yet_never_leads()
    {
        var feed = new FakeFeed();
        bool locked = true;
        var memory = new ObjectiveMemory(feed, NameOf, locked: (sno, at) => locked && sno == LeverSno && at == (50, 0));
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Lever(50, 0, quest: true), Altar(0, 50));

        Assert.Equal((0f, 50f), Assert.Single(memory.Destinations()).Position);

        locked = false; // the key found
        Assert.Equal(2, memory.Destinations().Count);
    }

    [Fact]
    public void An_objective_walked_away_from_undone_waits_until_another_one_is_done()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Lever(0, 0), Altar(100, 0));

        feed.RaisePlayer(At(1, 0)); // at the lever, which can't be used yet
        feed.RaisePlayer(At(ObjectiveMemory.LeaveUnits - 1, 0));
        Assert.Equal(2, memory.Destinations().Count);
        feed.RaisePlayer(At(ObjectiveMemory.LeaveUnits + 2, 0)); // the player gave up on it
        Assert.Equal((100f, 0f), Assert.Single(memory.Destinations()).Position);

        feed.RaisePlayer(At(100, 0));
        memory.Gone(Lever(0, 0)); // the altar was used: the lever may open now
        Assert.Equal((0f, 0f), Assert.Single(memory.Destinations()).Position);
    }

    [Fact]
    public void One_set_aside_then_done_leads_again_once_it_loops_again()
    {
        // Inarius (live 2026-10-21): the player left him, he fell silent while invulnerable, and sounded again with his chain in hand.
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Lever(0, 0), Altar(100, 0));
        feed.RaisePlayer(At(1, 0));
        feed.RaisePlayer(At(ObjectiveMemory.LeaveUnits + 2, 0)); // set aside
        feed.RaisePlayer(At(ObjectiveMemory.ConfirmRadiusUnits - 1, 0));
        memory.Gone(Altar(100, 0)); // silent near the player: done
        Assert.DoesNotContain(memory.Destinations(), d => d.Position == (0, 0));

        memory.Heard(Lever(0, 0), Altar(100, 0));
        Assert.Contains(memory.Destinations(), d => d.Position == (0, 0));
    }

    [Fact]
    public void One_missing_for_a_poll_or_two_near_the_player_is_not_done()
    {
        // Inarius's chain stations (live 2026-10-21) left the actor list for a poll or two and came back under a new id.
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(50 - ObjectiveMemory.ConfirmRadiusUnits + 1, 0));
        memory.Heard(Lever(50, 0));

        memory.Heard();
        memory.Heard();
        Assert.Single(memory.Destinations());

        memory.Heard(Lever(50, 0, id: 9));
        memory.Gone();
        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void In_a_boss_fight_an_objective_walked_away_from_is_never_set_aside()
    {
        const int BossSno = 50;
        var feed = new FakeFeed();
        var memory = new ObjectiveMemory(feed, NameOf, isBoss: sno => sno == BossSno);
        feed.RaisePlayer(At(0, 0));
        var boss = new ActorState(8, BossSno, 30, 30, 0);
        memory.Heard(Lever(0, 0), boss);
        Assert.True(memory.BossFight);

        feed.RaisePlayer(At(1, 0));
        feed.RaisePlayer(At(ObjectiveMemory.LeaveUnits + 2, 0));

        Assert.Contains(memory.Destinations(), d => d.Position == (0, 0));
    }

    [Fact]
    public void An_objective_left_undone_twice_no_longer_returns_when_another_one_is_done()
    {
        // Live 2026-10-16, the Light's Watch: a lectern drew the guide back four times, and the second guard was never found.
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Lever(0, 0), Altar(100, 0, id: 2), Altar(0, 100, id: 3));
        for (int i = 0; i < ObjectiveMemory.MaxTimesLeft; i++)
        {
            feed.RaisePlayer(At(1, 0));
            feed.RaisePlayer(At(ObjectiveMemory.LeaveUnits + 2, 0));
            if (i > 0) break;
            feed.RaisePlayer(At(100, 0));
            memory.Gone(Lever(0, 0), Altar(0, 100, id: 3)); // the first altar was used: the lever leads again
            Assert.Contains(memory.Destinations(), d => d.Position == (0, 0));
        }

        feed.RaisePlayer(At(0, 100));
        memory.Gone(Lever(0, 0)); // the second altar was used
        Assert.Empty(memory.Destinations());

        memory.Rearm();
        Assert.Equal((0f, 0f), Assert.Single(memory.Destinations()).Position);
    }

    [Fact]
    public void An_objective_done_or_set_aside_leads_again_as_a_quest_step_once_a_phase_names_its_sno()
    {
        // Live 2026-10-17, the Light's Watch: the gate read done, the key was found far away, and the guide fell silent.
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Lever(0, 0), Altar(100, 0));
        feed.RaisePlayer(At(100, 0));
        memory.Gone(Lever(0, 0)); // the altar done
        feed.RaisePlayer(At(1, 0));
        feed.RaisePlayer(At(-Far, 0)); // the lever set aside
        memory.ObserveNamed(new HashSet<int>());
        Assert.Empty(memory.Destinations());

        memory.ObserveNamed(new HashSet<int> { AltarSno, LeverSno });

        var points = memory.Destinations();
        Assert.Equal(2, points.Count);
        Assert.All(points, p => Assert.Equal(ObjectiveMemory.QuestTier, p.Tier));

        feed.RaisePlayer(At(99, 0));
        memory.Heard(Lever(0, 0)); // the altar used again; still named, it stays done
        memory.ObserveNamed(new HashSet<int> { AltarSno, LeverSno });
        Assert.Equal((0f, 0f), Assert.Single(memory.Destinations()).Position);
    }

    [Fact]
    public void Switching_the_guide_on_lets_a_reached_objective_lead_again()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Lever(0, 0));
        feed.RaisePlayer(At(1, 0));
        feed.RaisePlayer(At(-Far, 0)); // walked past it and got lost
        Assert.Empty(memory.Destinations());

        memory.Rearm();

        Assert.Single(memory.Destinations());
    }

    [Fact]
    public void An_objective_that_loops_again_leads_again()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(50 - ObjectiveMemory.ConfirmRadiusUnits + 1, 0));
        memory.Heard(Lever(50, 0));
        memory.Gone(); // locked while the player carries an item elsewhere
        Assert.Empty(memory.Destinations());

        memory.Heard(Lever(50, 0));

        Assert.Single(memory.Destinations());
    }

    [Fact]
    public void A_re_created_actor_on_the_same_spot_is_the_same_objective()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(-Far, 0));
        memory.Heard(Lever(50, 0, id: 1));
        string id = memory.Destinations()[0].Id;

        memory.Heard(Lever(50.4f, 0, id: 9));

        Assert.Equal(id, Assert.Single(memory.Destinations()).Id);
    }

    [Fact]
    public void A_world_change_forgets_every_objective()
    {
        var (feed, memory) = NewMemory();
        memory.Heard(Lever(50, 0));

        feed.RaiseWorld();

        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void The_loop_bridge_reports_objectives_looping_and_silent_and_every_actor()
    {
        var feed = new FakeFeed();
        var classifier = new ObjectiveClassifier(sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, NameOf);
        var interactables = new InteractableClassifier(sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, NameOf);
        var selector = new LoopTargetSelector(new HashSet<int>(), classifier, _ => new HashSet<int>(), interactables);
        var bridge = new ActorLoopBridge(feed, LoopTestKit.Player(), selector, NameOf, isUsed: _ => false);
        ObjectiveSighting? seen = null;
        bridge.ObjectivesSeen += s => seen = s;

        var locked = new ActorState(3, AltarSno, 5, 5, 0, QuestLocked: true);
        feed.RaiseActors(Lever(1, 1), locked, new ActorState(4, ChestSno, 2, 2, 0));

        Assert.Equal(LeverSno, Assert.Single(seen!.Looping).ActorSno);
        Assert.Equal(AltarSno, Assert.Single(seen.Silent).ActorSno);
        Assert.Equal(3, seen.Present.Count);
    }

    [Fact]
    public void An_empty_handed_player_is_not_led_to_a_receptacle_and_a_laden_one_is_led_there_first()
    {
        // Two statues, one pedestal left: the second statue is out of earshot (live report 2026-10-03).
        bool? holds = false;
        var feed = new FakeFeed();
        var memory = new ObjectiveMemory(feed, NameOf, takesHeld: sno => sno == AltarSno ? holds : null);
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Altar(50, 0), Lever(0, 50));

        Assert.Equal((0f, 50f), Assert.Single(memory.Destinations()).Position);

        holds = true;
        var altar = Assert.Single(memory.Destinations(), d => d.Position == (50, 0));
        Assert.Equal(ObjectiveMemory.QuestTier, altar.Tier);
        Assert.Equal(ObjectiveMemory.DungeonTier, Assert.Single(memory.Destinations(), d => d.Position == (0, 50)).Tier);
    }

    [Fact]
    public void A_crate_silent_while_another_is_carried_is_not_done_and_leads_again_once_the_hands_are_free()
    {
        // The Lost Archives' two crates (live 2026-10-16): carrying the first, the second fell silent beside it and was taken for
        // done, so the guide never led back to it. The same in every statue, bloodstone and carving dungeon.
        const int CrateSno = 983768;
        bool holding = false;
        var feed = new FakeFeed();
        var memory = new ObjectiveMemory(feed, NameOf, carryableWaits: sno => sno == CrateSno ? holding : null);
        ActorState Crate(float x, uint id) => new(id, CrateSno, x, 0, 0, QuestStep: true);
        var first = Crate(30, 11);
        var second = Crate(33, 12);
        feed.RaisePlayer(At(30, 0));
        memory.Heard(first, second);
        Assert.Equal(2, memory.Destinations().Count);

        holding = true; // the first is picked up: its copy follows the player, the second waits silent beside it
        var inHands = new ActorState(99, CrateSno, 30, 0, 0, ClientOnly: true);
        memory.Observe(new ObjectiveSighting([], [second], [second, inHands]));
        Assert.Empty(memory.Destinations()); // laden: the receptacle leads, not another crate

        feed.RaisePlayer(At(100, 0)); // delivered far away, hands free again
        holding = false;
        memory.Observe(new ObjectiveSighting([], [], []));
        Assert.Equal((33f, 0f), Assert.Single(memory.Destinations()).Position);
    }

    [Fact]
    public void A_crate_is_done_once_it_left_the_ground()
    {
        const int CrateSno = 983768;
        var feed = new FakeFeed();
        var memory = new ObjectiveMemory(feed, NameOf, carryableWaits: sno => sno == CrateSno ? false : null);
        feed.RaisePlayer(At(30, 0));
        memory.Heard(new ActorState(11, CrateSno, 30, 0, 0, QuestStep: true));

        memory.Observe(new ObjectiveSighting([], [], [new ActorState(99, CrateSno, 30, 0, 0, ClientOnly: true)])); // in the hands

        Assert.Empty(memory.Destinations());
    }

    // The Ancient Mausoleum's "chase Lilith" step (QST_Frac_Underworld2_02 phase 80, live 2026-10-01): a trigger to walk into.
    private const int TriggerSno = 42298;

    [Fact]
    public void A_quest_place_leads_in_the_quest_tier_and_stays_while_the_player_stands_in_it()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(40, 50));
        memory.Heard(Altar(100, 0));

        memory.ObservePlaces([new QuestPlace(TriggerSno, -1, 51)]);

        var place = Assert.Single(memory.Destinations(), d => d.Position == (-1, 51));
        Assert.Equal(ObjectiveMemory.QuestTier, place.Tier);
        Assert.StartsWith("quest-place:", place.Id);
        Assert.Equal(3f, place.Radius); // no trigger radius known: an item's reach

        // Live 2026-10-01: phase 154's trigger was reached and the phase stayed; the guide must not turn to unexplored ground.
        feed.RaisePlayer(At(-1, 49)); // inside the trigger
        memory.ObservePlaces([new QuestPlace(TriggerSno, -1, 51)]);
        Assert.Contains(memory.Destinations(), d => d.Position == (-1, 51));
    }

    [Fact]
    public void Of_several_quest_places_one_stood_in_and_walked_on_from_gives_way_to_the_next()
    {
        // Live 2026-10-17 (CSD_Skov_Temis_SmugglersRun_02): one step named six triggers through the dungeon.
        var (feed, memory) = NewMemory();
        QuestPlace[] places = [new(TriggerSno, 0, 0), new(TriggerSno, 10, 0)];
        feed.RaisePlayer(At(-20, 0));
        memory.ObservePlaces(places);
        Assert.Equal(2, memory.Destinations().Count);

        feed.RaisePlayer(At(0, 1)); // in the first
        feed.RaisePlayer(At(8, 0)); // on into the second, closer than LeaveUnits
        memory.ObservePlaces(places);
        Assert.Equal((10f, 0f), Assert.Single(memory.Destinations()).Position);

        feed.RaisePlayer(At(-Far, 0)); // the last one left stays the goal
        memory.ObservePlaces(places);
        Assert.Equal((10f, 0f), Assert.Single(memory.Destinations()).Position);
    }

    [Fact]
    public void A_trigger_place_carries_its_own_radius_and_height()
    {
        var feed = new FakeFeed();
        var memory = new ObjectiveMemory(feed, NameOf, triggerRadius: sno => sno == TriggerSno ? 4f : null);

        memory.ObservePlaces([new QuestPlace(TriggerSno, -126.8f, -26f, -31.2f)]);

        var place = Assert.Single(memory.Destinations());
        Assert.Equal(4f, place.Radius);
        Assert.Equal(-31.2f, place.Z);
    }

    [Fact]
    public void A_quest_place_is_done_when_the_phase_moves_on_and_the_world_change_forgets_it()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(Far, Far));
        memory.ObservePlaces([new QuestPlace(TriggerSno, -1, 51)]);

        memory.ObservePlaces([new QuestPlace(TriggerSno, 24, 33)]); // the next phase names another spot

        Assert.Equal((24f, 33f), Assert.Single(memory.Destinations()).Position);

        feed.RaiseWorld(1);
        memory.ObservePlaces([]);
        Assert.Empty(memory.Destinations());
    }

    // Live 2026-10-04 (S15_Triad_A_Labyrinth): the phase names S15_TriadA_Lever1 by SNO; four copies stand in the tiles.
    private static QuestPlace PlacedLever(float x, float y) => new(LeverSno, x, y, Ahead: true);

    [Fact]
    public void A_gizmo_a_tile_places_leads_as_a_quest_step_before_it_is_heard()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));

        memory.ObservePlaces([PlacedLever(100, 0), PlacedLever(-100, 0)]);

        Assert.Equal(2, memory.Destinations().Count);
        Assert.All(memory.Destinations(), d => Assert.Equal(ObjectiveMemory.QuestTier, d.Tier));
    }

    [Fact]
    public void A_placed_gizmo_silent_near_the_player_was_used_and_stays_done()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.ObservePlaces([PlacedLever(100, 0), PlacedLever(-100, 0)]);

        feed.RaisePlayer(At(100 - ObjectiveMemory.ConfirmRadiusUnits + 1, 0));
        memory.Heard(); // pulled earlier: no loop
        memory.ObservePlaces([PlacedLever(100, 0), PlacedLever(-100, 0)]); // still named on every poll

        Assert.Equal([(-100f, 0f)], memory.Destinations().Select(d => d.Position));
    }

    [Fact]
    public void A_placed_gizmo_that_stands_there_without_an_objective_loop_waits_for_the_phase()
    {
        // A door the phase names loops as a door, a trigger not at all: near it, neither is done before the phase moves on.
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(95, 0));
        memory.ObservePlaces([PlacedLever(100, 0)]);

        memory.Observe(new ObjectiveSighting([], [], [Altar(0, 0), new ActorState(7, LeverSno, 100, 0, 0)]));
        Assert.Single(memory.Destinations());

        memory.ObservePlaces([]);
        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void A_placed_gizmo_silent_as_an_objective_was_used_and_a_tile_copy_with_nothing_there_was_never_spawned()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(1, 0)); // (0,0,0) reads as a world unloading
        memory.ObservePlaces([PlacedLever(10, 0), PlacedLever(-10, 0)]);
        var used = new ActorState(7, LeverSno, 10, 0, 0, Operated: true);

        memory.Observe(new ObjectiveSighting([], [used], [used]));

        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void A_gizmo_the_map_marks_waits_for_the_phase_even_with_no_actor_near()
    {
        // Live 2026-10-04: the map listed the Labyrinth's lair door while it was no actor; its trigger kin never stream in at all.
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(1, 0)); // (0,0,0) reads as a world unloading
        memory.ObservePlaces([PlacedLever(10, 0) with { Marked = true }, PlacedLever(-10, 0), new QuestPlace(TriggerSno, 0, 40)]);

        memory.Heard();

        Assert.True(Assert.Single(memory.Destinations(), d => d.Position == (10, 0)).Located);
        Assert.False(Assert.Single(memory.Destinations(), d => d.Position == (0, 40)).Located); // script data: maybe another world
    }

    [Fact]
    public void An_objective_is_reached_within_reach_of_its_footprint()
    {
        var feed = new FakeFeed();
        var memory = new ObjectiveMemory(feed, NameOf, footprint: sno => sno == AltarSno ? 5f : null);
        feed.RaisePlayer(At(0, 0));
        memory.Heard(Altar(30, 0), Lever(-30, 0));

        Assert.Equal(5 + ObjectiveMemory.InteractUnits, memory.Destinations().Single(d => d.Position == (30, 0)).Radius);
        Assert.Equal(ObjectiveMemory.InteractUnits, memory.Destinations().Single(d => d.Position == (-30, 0)).Radius);
    }

    [Fact]
    public void A_placed_gizmo_heard_is_the_same_objective_and_unheard_ones_drop_when_the_phase_moves_on()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.ObservePlaces([PlacedLever(100, 0), PlacedLever(-100, 0)]);

        memory.Heard(Lever(100.2f, 0, quest: true));
        Assert.Equal(2, memory.Destinations().Count);

        memory.ObservePlaces([]); // the phase moved on
        Assert.Equal([(100.2f, 0f)], memory.Destinations().Select(d => d.Position)); // heard: done once silent near the player
    }

    // Tester, 2026-10-27 (act 1 campaign dungeons): a trigger has the game's 4-unit radius (Trigger_Sphere_8m).
    private static (FakeFeed Feed, ObjectiveMemory Memory) NewTriggerMemory()
    {
        var feed = new FakeFeed();
        return (feed, new ObjectiveMemory(feed, NameOf, triggerRadius: sno => sno == TriggerSno ? 4f : null));
    }

    private static void Walk(FakeFeed feed, (float X, float Y) from, (float X, float Y) to)
    {
        for (float t = 0; t <= 1; t += 0.05f) feed.RaisePlayer(At(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t));
        feed.RaisePlayer(At(to.X, to.Y));
    }

    [Fact]
    public void The_last_trigger_of_a_step_is_set_aside_once_walked_out_of()
    {
        // The Necropolis (live 2026-10-01, phase 154; tester 2026-10-27): the phase waits on a region farther on, and holding on
        // the trigger pulled the player back to it from every room.
        var (feed, memory) = NewTriggerMemory();
        QuestPlace[] step = [new(TriggerSno, 0, 0)];
        feed.RaisePlayer(At(-30, 0));
        memory.ObservePlaces(step);
        Walk(feed, (-30, 0), (0, 1)); // in it
        memory.ObservePlaces(step);
        Assert.Single(memory.Destinations()); // still the goal while the player stands in it

        Walk(feed, (0, 1), (0, 7)); // out of 1.5 times its radius
        memory.ObservePlaces(step);
        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void A_trigger_named_on_ground_the_player_already_walked_never_leads()
    {
        // The Cradle (tester, 2026-10-27): the step names the centre of a holdout tunnel the dungeon repeats; only the first copy,
        // walked through on the way to the gate, was placed, and the guide led back away from the gate just opened.
        var (feed, memory) = NewTriggerMemory();
        Walk(feed, (0, 16), (-24, 16)); // through the tunnel's centre at (-15.7, 16) to the gate
        memory.ObservePlaces([new QuestPlace(TriggerSno, -15.7f, 16)]);
        Assert.Empty(memory.Destinations());

        memory.ObservePlaces([new QuestPlace(TriggerSno, -15.7f, 16), new QuestPlace(TriggerSno, -143.8f, 48)]); // the next copy
        Assert.Equal((-143.8f, 48f), Assert.Single(memory.Destinations()).Position);

        memory.Rearm(walkedThrough: true); // nothing left to explore
        Assert.Equal(2, memory.Destinations().Count);
    }

    [Fact]
    public void A_trigger_named_where_the_player_stands_is_reached_as_usual()
    {
        var (feed, memory) = NewTriggerMemory();
        Walk(feed, (-10, 0), (0, 0));
        memory.ObservePlaces([new QuestPlace(TriggerSno, 1, 0)]);
        Assert.Single(memory.Destinations());
    }

    [Fact]
    public void A_tile_placed_trigger_the_player_walked_never_leads_and_the_next_copy_does()
    {
        // The Cradle (tester, 2026-10-27): the phase names DSH_Generic_Holdout_Trigger by SNO; every holdout tile places one, and
        // the guide led back to the two holdouts already fought until the third tile loaded.
        var (feed, memory) = NewTriggerMemory();
        Walk(feed, (0, 0), (50, 0)); // the first holdout, at (10, 0)
        Walk(feed, (50, 0), (100, 0)); // the second, at (90, 0)
        QuestPlace[] holdouts = [new(TriggerSno, 10, 0, Ahead: true), new(TriggerSno, 90, 0, Ahead: true), new(TriggerSno, 200, 0, Ahead: true)];
        memory.ObservePlaces(holdouts);
        memory.ObserveNamed(new HashSet<int> { TriggerSno });

        Assert.Equal((200f, 0f), Assert.Single(memory.Destinations()).Position);
    }

    [Fact]
    public void A_trigger_walked_through_waits_when_another_objective_is_done_and_when_the_guide_is_switched_on()
    {
        // The Necropolis (tester, 2026-10-27): each tumor broken led back to the central chamber's triggers.
        var (feed, memory) = NewTriggerMemory();
        QuestPlace[] step = [new(TriggerSno, 0, 0), new(TriggerSno, 0, 30)];
        feed.RaisePlayer(At(-20, 0));
        memory.ObservePlaces(step);
        memory.Heard(Lever(40, 0, quest: true));
        Walk(feed, (-20, 0), (40, 0)); // through the first trigger to the lever
        memory.ObservePlaces(step);
        memory.Gone(); // the lever used
        Assert.Equal((0f, 30f), Assert.Single(memory.Destinations()).Position);

        memory.Rearm();
        Assert.Equal((0f, 30f), Assert.Single(memory.Destinations()).Position);
    }
}
