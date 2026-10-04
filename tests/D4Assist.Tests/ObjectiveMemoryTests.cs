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

        memory.Observe([Lever(50, 0, quest: true), Altar(0, 50)]);

        var points = memory.Destinations();
        Assert.Equal(2, points.Count);
        Assert.Equal(ObjectiveMemory.QuestTier, points.Single(p => p.Position == (50, 0)).Tier);
        Assert.Equal(ObjectiveMemory.DungeonTier, points.Single(p => p.Position == (0, 50)).Tier);
    }

    [Fact]
    public void Out_of_earshot_it_stays_and_silent_near_the_player_it_is_done()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Observe([Lever(50, 0)]);

        feed.RaisePlayer(At(-Far, 0));
        memory.Observe([]);
        Assert.Single(memory.Destinations());

        feed.RaisePlayer(At(50 - ObjectiveMemory.ConfirmRadiusUnits + 1, 0));
        memory.Observe([]); // used, locked or removed
        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void A_reached_objective_waits_until_another_one_is_done()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Observe([Lever(0, 0), Altar(100, 0)]);

        feed.RaisePlayer(At(1, 0)); // at the lever: the player saw it, the guide moves on
        Assert.Equal((100f, 0f), Assert.Single(memory.Destinations()).Position);

        feed.RaisePlayer(At(100, 0));
        memory.Observe([Lever(0, 0)]); // the altar was used: the lever may open now
        Assert.Equal((0f, 0f), Assert.Single(memory.Destinations()).Position);
    }

    [Fact]
    public void Switching_the_guide_on_lets_a_reached_objective_lead_again()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.Observe([Lever(0, 0)]);
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
        feed.RaisePlayer(At(30, 0));
        memory.Observe([Lever(50, 0)]);
        memory.Observe([]); // locked while the player carries an item elsewhere
        Assert.Empty(memory.Destinations());

        memory.Observe([Lever(50, 0)]);

        Assert.Single(memory.Destinations());
    }

    [Fact]
    public void A_re_created_actor_on_the_same_spot_is_the_same_objective()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(-Far, 0));
        memory.Observe([Lever(50, 0, id: 1)]);
        string id = memory.Destinations()[0].Id;

        memory.Observe([Lever(50.4f, 0, id: 9)]);

        Assert.Equal(id, Assert.Single(memory.Destinations()).Id);
    }

    [Fact]
    public void A_world_change_forgets_every_objective()
    {
        var (feed, memory) = NewMemory();
        memory.Observe([Lever(50, 0)]);

        feed.RaiseWorld();

        Assert.Empty(memory.Destinations());
    }

    [Fact]
    public void The_loop_bridge_reports_only_objectives_that_loop()
    {
        var feed = new FakeFeed();
        var classifier = new ObjectiveClassifier(sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, NameOf);
        var interactables = new InteractableClassifier(sno => Actors.TryGetValue(sno, out var a) ? a.Info : null, NameOf);
        var selector = new LoopTargetSelector(new HashSet<int>(), classifier, _ => new HashSet<int>(), interactables);
        var bridge = new ActorLoopBridge(feed, LoopTestKit.Player(), selector, NameOf, isUsed: _ => false);
        IReadOnlyList<ActorState> heard = [];
        bridge.ObjectivesLooping += actors => heard = actors;

        var locked = new ActorState(3, AltarSno, 5, 5, 0, QuestLocked: true);
        feed.RaiseActors(Lever(1, 1), locked, new ActorState(4, ChestSno, 2, 2, 0));

        Assert.Equal(LeverSno, Assert.Single(heard).ActorSno);
    }

    [Fact]
    public void An_empty_handed_player_is_not_led_to_a_receptacle_and_a_laden_one_is_led_there_first()
    {
        // Two statues, one pedestal left: the second statue is out of earshot (live report 2026-10-03).
        bool? holds = false;
        var feed = new FakeFeed();
        var memory = new ObjectiveMemory(feed, NameOf, takesHeld: sno => sno == AltarSno ? holds : null);
        feed.RaisePlayer(At(0, 0));
        memory.Observe([Altar(50, 0), Lever(0, 50)]);

        Assert.Equal((0f, 50f), Assert.Single(memory.Destinations()).Position);

        holds = true;
        var altar = Assert.Single(memory.Destinations(), d => d.Position == (50, 0));
        Assert.Equal(ObjectiveMemory.QuestTier, altar.Tier);
        Assert.Equal(ObjectiveMemory.DungeonTier, Assert.Single(memory.Destinations(), d => d.Position == (0, 50)).Tier);
    }

    // The Ancient Mausoleum's "chase Lilith" step (QST_Frac_Underworld2_02 phase 80, live 2026-10-01): a trigger to walk into.
    private const int TriggerSno = 42298;

    [Fact]
    public void A_quest_place_leads_in_the_quest_tier_and_stays_while_the_player_stands_in_it()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(40, 50));
        memory.Observe([Altar(100, 0)]);

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
        memory.Observe([]); // pulled earlier: no loop
        memory.ObservePlaces([PlacedLever(100, 0), PlacedLever(-100, 0)]); // still named on every poll

        Assert.Equal([(-100f, 0f)], memory.Destinations().Select(d => d.Position));
    }

    [Fact]
    public void A_placed_gizmo_heard_is_the_same_objective_and_unheard_ones_drop_when_the_phase_moves_on()
    {
        var (feed, memory) = NewMemory();
        feed.RaisePlayer(At(0, 0));
        memory.ObservePlaces([PlacedLever(100, 0), PlacedLever(-100, 0)]);

        memory.Observe([Lever(100.2f, 0, quest: true)]);
        Assert.Equal(2, memory.Destinations().Count);

        memory.ObservePlaces([]); // the phase moved on
        Assert.Equal([(100.2f, 0f)], memory.Destinations().Select(d => d.Position)); // heard: done once silent near the player
    }
}
