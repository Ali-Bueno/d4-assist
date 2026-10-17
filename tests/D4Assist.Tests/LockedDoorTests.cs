using System.Numerics;
using D4Assist.Game;
using D4Data.Format;
using D4Data.Readers;

namespace D4Assist.Tests;

public class LockedDoorTests
{
    private const int Gizmo = 2, DoorSno = 1;

    // DRLG_Blocker_Protodun_Keep_DGN_Frac_DerelictLodge 1172068: a door gizmo, half-extents (0.5, 2.2, 2.2) (research §21).
    private static readonly Vector3 DoorCenter = new(0, 0, 2.2f), DoorHalf = new(0.5f, 2.2f, 2.2f);

    private static readonly Dictionary<int, (string Name, ActorDefinitionInfo Info)> Actors = new()
    {
        [DoorSno] = ("DRLG_Blocker_Protodun_Keep_DGN_Frac_DerelictLodge", new(Gizmo, Conventions.GizmoDoor, DoorCenter, DoorHalf)),
    };

    private static ActorDefinitionInfo? Info(int sno) => Actors.TryGetValue(sno, out var a) ? a.Info : null;

    private static string? NameOf(int sno) => Actors.TryGetValue(sno, out var a) ? a.Name : null;

    private static LockedDoors Doors() => new(new DoorObstacles(new InteractableClassifier(Info, NameOf), Info), NameOf);

    // The door stands across Y at x = 10.5; the player comes from +X.
    private static readonly ActorState Door = new(7, DoorSno, 10.5f, 20.5f, 0);
    private static readonly ActorState KeyLocked = Door with { QuestLocked = true };
    private static readonly PlayerState AtTheDoor = At(12, 20.5f), Away = At(40, 20.5f);

    private static PlayerState At(float x, float y) => new(x, y, 0, 0);

    private static HashSet<(int X, int Y)> Footprint() => [.. DoorObstacles.Footprint(Door, DoorCenter, DoorHalf).Cells];

    [Fact]
    public void A_door_the_game_wont_let_the_player_open_is_a_wall_and_one_it_will_is_not()
    {
        var doors = Doors();

        Assert.Equal(Footprint(), doors.Update([KeyLocked], At(20, 20.5f)));
        Assert.True(doors.IsLocked(DoorSno, (10.5f, 20.5f)));

        Assert.Empty(doors.Update([Door], At(20, 20.5f))); // the key found: the phase names it now
        Assert.False(doors.IsLocked(DoorSno, (10.5f, 20.5f)));
    }

    [Fact]
    public void A_locked_door_out_of_the_clients_range_stays_a_wall()
    {
        var doors = Doors();
        doors.Update([KeyLocked], At(20, 20.5f));

        Assert.Equal(Footprint(), doors.Update([], At(80, 20.5f)));
    }

    [Fact]
    public void A_locked_door_seen_open_or_missing_near_its_spot_is_open()
    {
        var opened = Doors();
        opened.Update([KeyLocked], At(20, 20.5f));
        Assert.Empty(opened.Update([KeyLocked with { StateChanged = true }], At(20, 20.5f)));

        var gone = Doors();
        gone.Update([KeyLocked], At(20, 20.5f));
        Assert.Empty(gone.Update([], AtTheDoor)); // blockers vanish about 10 s after opening (research §6)
    }

    [Fact]
    public void A_closed_door_the_player_stood_at_and_walked_away_from_is_a_wall_until_its_quest_changes()
    {
        var doors = Doors();
        Assert.Empty(doors.Update([Door], AtTheDoor));

        Assert.Equal(Footprint(), doors.Update([], Away)); // tried: it doesn't open yet
        Assert.True(doors.IsLocked(DoorSno, (10.5f, 20.5f)));

        Assert.Empty(doors.Update([Door with { NamedByQuest = true }], Away));
    }

    [Fact]
    public void A_tried_door_out_of_the_clients_range_opens_up_once_a_phase_starts_naming_its_sno()
    {
        // Live 2026-10-17, the Light's Watch gate: the key found far away, the phase named the gate.
        var doors = Doors();
        doors.Update([Door], AtTheDoor);
        doors.Update([], Away);
        doors.ObserveNamed(new HashSet<int>());
        Assert.True(doors.IsLocked(DoorSno, (10.5f, 20.5f)));

        doors.ObserveNamed(new HashSet<int> { DoorSno });

        Assert.False(doors.IsLocked(DoorSno, (10.5f, 20.5f)));
        Assert.Empty(doors.Update([], At(80, 20.5f)));
    }

    [Fact]
    public void A_door_tried_while_its_sno_is_already_named_stays_tried()
    {
        var doors = Doors();
        doors.ObserveNamed(new HashSet<int> { DoorSno });
        doors.Update([Door], AtTheDoor);
        doors.Update([], Away);

        doors.ObserveNamed(new HashSet<int> { DoorSno });

        Assert.True(doors.IsLocked(DoorSno, (10.5f, 20.5f)));
    }

    [Fact]
    public void A_door_opened_by_the_player_at_it_is_never_tried()
    {
        var doors = Doors();
        doors.Update([Door], AtTheDoor);
        doors.Update([Door with { StateChanged = true }], AtTheDoor);

        Assert.Empty(doors.Update([], Away));
    }

    [Fact]
    public void A_closed_door_the_player_never_came_to_stays_passable() =>
        Assert.Empty(Doors().Update([Door], At(25, 20.5f)));

    [Fact]
    public void Switching_the_guide_on_lets_a_tried_door_lead_again()
    {
        var doors = Doors();
        doors.Update([Door], AtTheDoor);
        doors.Update([], Away);

        doors.Rearm();

        Assert.Empty(doors.Update([], Away));
    }

    [Fact]
    public void A_quest_moving_on_lets_a_door_out_of_range_lead_again()
    {
        // A boss room's door opens by script once the objective is done, often while the player is too far for the client to list it.
        var tried = Doors();
        tried.Update([Door], AtTheDoor);
        tried.Update([], Away);
        tried.QuestMovedOn();
        Assert.Empty(tried.Update([], Away));

        var locked = Doors();
        locked.Update([KeyLocked], At(20, 20.5f));
        locked.QuestMovedOn();
        Assert.Empty(locked.Update([], At(80, 20.5f)));
    }

    [Fact]
    public void A_door_still_locked_after_the_quest_moved_on_locks_again_once_listed()
    {
        var doors = Doors();
        doors.Update([KeyLocked], At(20, 20.5f));
        doors.QuestMovedOn();

        Assert.Equal(Footprint(), doors.Update([KeyLocked], At(20, 20.5f)));
    }
}
