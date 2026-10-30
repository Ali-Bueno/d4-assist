using D4Assist.Navigation;

namespace D4Assist.Game;

/// <summary>
/// Remembers the objective loops heard in the current dungeon (quest steps, dungeon objectives) so the exploration guide
/// can lead back to one out of earshot. One that falls silent near the player is done. One the player stood at stays the goal
/// until it is done; walked away from undone, it waits until another objective is done (a key found may open the door visited
/// earlier) or the guide is switched on again. A door the player can't open yet (LockedDoors) never leads.
/// The places the active quest's current phase names (a trigger to walk into: "chase Lilith") lead in the quest tier too,
/// until the phase moves on: reaching one is not the end of the step (live 2026-10-01: phase 154's trigger is one of the
/// step's hidden callbacks; the shown one waits for a group of enemies), so the guide holds there (ExplorationGuideBridge).
/// A step naming several places sets one aside once the player stood in it and walked on, while another is still to reach:
/// six triggers named one step through a story dungeon (live 2026-10-17, CSD_Skov_Temis_SmugglersRun_02). So does a step naming
/// an item to carry not found yet (research/dungeon-objectives.md §28).
/// A trigger (a place or a gizmo a tile places, with a trigger radius) is set aside once walked out of even when it is the last
/// one, and one named on ground the player already walked never leads: the game fires it as the player walks in, so a phase
/// that stays is waiting on something farther on (research/dungeon-objectives.md §27).
/// </summary>
public sealed class ObjectiveMemory
{
    /// <summary>Within this distance the client lists an objective's actor while it stands (LockedDoors.ListedWithinUnits), so no
    /// loop there means it was used, locked or removed. Farther out it drops from the list now and then: at 16-19 units a lectern,
    /// corpses and the gate read done and looped again a poll later, over and over (live 2026-10-17, the Light's Watch).</summary>
    public const float ConfirmRadiusUnits = LockedDoors.ListedWithinUnits;

    /// <summary>Walking this far from an objective the player stood at, still undone, sets it aside: it can't be used yet, or
    /// its use went unseen. Beyond the distance that confirms a silent one, so a player who used it learns that first.</summary>
    public const float LeaveUnits = ExplorationGuide.SeenRadiusUnits;

    /// <summary>An objective left undone this often stays set aside when another one is done; switching the guide on re-arms it.</summary>
    public const int MaxTimesLeft = 2;

    /// <summary>Close enough to use an object or talk to an NPC, past the edge of its own footprint.</summary>
    public const float InteractUnits = 3f;

    /// <summary>Guide tiers: the tracked quest's step leads before the dungeon's own objectives.</summary>
    public const int QuestTier = 0, DungeonTier = 1;

    /// <summary>Id prefix of a quest place's guide destination.</summary>
    public const string QuestPlaceKind = "quest-place";

    /// <summary>Id prefix of a light to escort.</summary>
    public const string EscortKind = "escort";

    // The light moves on a few units ahead whenever the player is in it (live: in and out every second or two), so the guide holds
    // a little beyond its edge rather than chime at every step; its own loop says inside or outside.
    private const float EscortHoldFactor = 2f;

    // A placed gizmo never moves; a re-created actor (new id) lands on the same spot.
    private const float SameSpotUnits = 1f;

    private sealed class Objective(int sno, string name, (float X, float Y) position, float radius, string kind = "objective")
    {
        public float? Z { get; set; }
        public float Radius { get; } = radius;
        public int Sno { get; } = sno;
        public string Name { get; } = name;
        public string Id { get; } = $"{kind}:{sno}:{MathF.Round(position.X)},{MathF.Round(position.Y)}";
        public (float X, float Y) Position { get; set; } = position;
        public uint ActorId { get; set; }
        /// <summary>The actor heard has no game record (a client-side prop); null until heard.</summary>
        public bool? ClientOnly { get; set; }
        /// <summary>A quest place whose actor there was heard and is done (a monster killed): it stays done while the phase names it.</summary>
        public bool ActorDone { get; set; }
        public bool Quest { get; set; }
        public bool Pending { get; set; } = true;
        /// <summary>The player stands within its radius.</summary>
        public bool AtIt { get; set; }
        /// <summary>Set aside: the player stood at it and walked away while it was still undone.</summary>
        public bool Reached { get; set; }
        /// <summary>How often the player walked away from it undone.</summary>
        public int TimesLeft { get; set; }
        /// <summary>A trigger the player stood in, since it was named or before: only nothing left to explore leads to it again.</summary>
        public bool WalkedThrough { get; set; }
        /// <summary>Known only from the tile that places it or the map that marks it; cleared once heard.</summary>
        public bool PlacedOnly { get; set; }
        /// <summary>The game's map marks it now.</summary>
        public bool Marked { get; set; }
        /// <summary>Actor polls in a row it was missing from the actors read.</summary>
        public int MissingPolls { get; set; }
        /// <summary>Done as a followed quest moved on with the player at it: using it was the step.</summary>
        public bool UsedByStep { get; set; }
        /// <summary>A sub-quest step whose phase can't be read (QuestPlace.Step); 0 for none.</summary>
        public int Step { get; init; }
    }

    private readonly Func<int, string?> _nameOf;
    private readonly Func<int, float?> _triggerRadius;
    private readonly Func<int, float?> _footprint;
    private readonly Func<int, bool?> _takesHeld;
    private readonly Func<int, (float X, float Y), bool> _locked;
    private readonly Func<int, bool?> _carryableWaits;
    private readonly Func<int, float?> _escortRadius;
    private readonly Func<int, bool> _isBoss;
    private readonly Func<int, bool> _isNpc;
    private volatile bool _bossFight;

    /// <summary>True while a boss or unique to kill is among the objective actors read (looping, or silent while invulnerable): a
    /// fight's objectives are its mechanics, used again and again, and the guide stays in the arena (ExplorationGuideBridge).</summary>
    public bool BossFight => _bossFight;

    /// <summary>Actor polls an objective heard earlier may be missing near the player before it reads done: in Inarius's fight
    /// the chain stations left the actor list for a poll or two and came back under a new id, and each one near the player read
    /// done (live 2026-10-21). About 3 s at the 0.5 s actor poll.</summary>
    public const int MissingPollsForDone = 6;
    private IReadOnlyList<ActorState> _escorts = [];
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private readonly List<Objective> _objectives = [];
    private readonly List<Objective> _places = [];
    private (float X, float Y)? _player;
    // The 1-unit cells the player stood in, this world.
    private readonly HashSet<(int X, int Y)> _trail = [];

    /// <param name="log">Debug sink for sightings, arrivals and completions; null when not debugging.</param>
    /// <param name="triggerRadius">A trigger actor's ground radius (ActorDefinitionInfo.TriggerRadius): a quest place that is a
    /// trigger counts as there only inside it (Trigger_Sphere_8m: 4). Null for none.</param>
    /// <param name="takesHeld">For a receptacle, whether it takes what the player holds (LoopTargetSelector.ReceptacleTakesHeld): it
    /// leads only then, in the quest tier; empty-handed, the next carryable or unexplored ground comes first
    /// (research/dungeon-objectives.md §14). Null for none.</param>
    /// <param name="footprint">Half the ground size of an actor's box (ActorDefinitionInfo bounds), so a big altar is reached at its
    /// edge; null for none.</param>
    /// <param name="locked">True for a door of that SNO at that spot the player can't open yet (LockedDoors.IsLocked); null for none.</param>
    /// <param name="carryableWaits">For a carryable, whether the player's hands are full (LoopTargetSelector.CarryableWaits); null
    /// for any other actor. A carryable falls silent while another is carried, so it is done only once it left the ground (picked up,
    /// then delivered), and it leads only with empty hands (live 2026-10-16, the Lost Archives' second crate; the same in every
    /// statue, bloodstone and carving dungeon, research/dungeon-objectives.md §14). Null for none.</param>
    /// <param name="isNpc">True for a friendly NPC (NpcClassifier.IsNpc): a quest place it marks is where it stood, and gives way once it is
    /// seen elsewhere or gone from it (research/dungeon-objectives.md §30). Null for none.</param>
    /// <param name="escortRadius">For a light the player walks inside to where the quest needs it (SafeZoneClassifier.EscortRadius),
    /// the distance that counts as inside; null for any other actor. Null for none.</param>
    public ObjectiveMemory(
        IDungeonFeed feed, Func<int, string?> nameOf, Action<string>? log = null, Func<int, float?>? triggerRadius = null,
        Func<int, bool?>? takesHeld = null, Func<int, float?>? footprint = null, Func<int, (float X, float Y), bool>? locked = null,
        Func<int, bool?>? carryableWaits = null, Func<int, float?>? escortRadius = null, Func<int, bool>? isBoss = null,
        Func<int, bool>? isNpc = null)
    {
        _isNpc = isNpc ?? (_ => false);
        _isBoss = isBoss ?? (_ => false);
        _carryableWaits = carryableWaits ?? (_ => null);
        _escortRadius = escortRadius ?? (_ => null);
        _nameOf = nameOf;
        _triggerRadius = triggerRadius ?? (_ => null);
        _footprint = footprint ?? (_ => null);
        _takesHeld = takesHeld ?? (_ => null);
        _locked = locked ?? ((_, _) => false);
        _log = log;
        feed.WorldChanged += (_, _, _) => { lock (_gate) { _objectives.Clear(); _places.Clear(); _trail.Clear(); _escorts = []; _player = null; _namedSnos = new HashSet<int>(); } };
        feed.PlayerMoved += p => { if (!p.IsWorldUnloaded) lock (_gate) OnPlayer((p.X, p.Y)); };
    }

    /// <summary>The objectives to lead to: looping when last heard, not set aside and not a locked door; quest steps in <see cref="QuestTier"/>.
    /// A light to escort leads in the quest tier while it is listed: it travels to the quest's next place, and the step's own place
    /// (where it was lit) would lead back the way it came (live 2026-10-20, Rathma's archive).</summary>
    public IReadOnlyList<GuideDestination> Destinations()
    {
        lock (_gate)
            return [.. _escorts.Select(a => new GuideDestination(
                    $"{EscortKind}:{a.Id}", (a.X, a.Y), QuestTier, a.Z, _escortRadius(a.ActorSno)!.Value * EscortHoldFactor, Located: true)),
                .. _places.Where(Leads).Concat(_objectives).Select(o => (Objective: o, Delivers: _takesHeld(o.Sno)))
                .Where(x => x.Objective.Pending && !x.Objective.Reached && x.Delivers != false && !_locked(x.Objective.Sno, x.Objective.Position)
                    && _carryableWaits(x.Objective.Sno) != true)
                .Select(x => new GuideDestination(x.Objective.Id, x.Objective.Position,
                    x.Objective.Quest || x.Delivers == true ? QuestTier : DungeonTier, x.Objective.Z, x.Objective.Radius, Located(x.Objective)))];
    }

    /// <summary>The objectives heard here and not done yet, to outlive a restart of the app (quest places are named again).</summary>
    public IReadOnlyList<SavedObjective> Export()
    {
        lock (_gate)
            return [.. _objectives.Where(o => o.Pending && !o.PlacedOnly).Select(o => new SavedObjective(o.Sno, o.Name, o.Position.X, o.Position.Y, o.Quest))];
    }

    /// <summary>Adds back the objectives an earlier run heard in this instance and not done: still waiting, as when out of earshot.</summary>
    public void Restore(IEnumerable<SavedObjective> saved)
    {
        lock (_gate)
            foreach (var s in saved)
                if (!_objectives.Any(o => o.Sno == s.Sno && Distance(o.Position, (s.X, s.Y)) <= SameSpotUnits))
                    _objectives.Add(new Objective(s.Sno, s.Name, (s.X, s.Y), Reach(s.Sno)) { Quest = s.Quest });
    }

    /// <summary>Takes the places the active quests' current phases name (GameSession.QuestPlacesRead, every actor poll).</summary>
    public void ObservePlaces(IReadOnlyList<QuestPlace> places)
    {
        lock (_gate)
        {
            ObserveAhead([.. places.Where(p => p.Ahead)]);
            var named = new HashSet<Objective>();
            foreach (var place in places.Where(p => !p.Ahead))
            {
                var objective = _places.FirstOrDefault(o => o.Sno == place.ActorSno && Distance(o.Position, (place.X, place.Y)) <= SameSpotUnits);
                if (objective is null)
                {
                    objective = new Objective(place.ActorSno, NameOf(place.ActorSno), (place.X, place.Y), Reach(place.ActorSno), QuestPlaceKind)
                        { Quest = true, Z = place.Z, Step = place.Step };
                    _places.Add(objective);
                    Log("seen (quest place)", objective);
                    SetAsideIfWalked(objective);
                }
                else if (!objective.Pending && !objective.ActorDone)
                {
                    objective.Pending = true;
                    Log("named again", objective);
                    SetAsideIfWalked(objective);
                }
                objective.Marked = place.Marked;
                named.Add(objective);
            }
            foreach (var objective in _places.Where(o => !named.Contains(o)))
            {
                objective.ActorDone = false;
                if (!objective.Pending) continue;
                objective.Pending = false;
                Log("done (phase moved on)", objective);
            }
        }
    }

    /// <summary>Takes the actor SNOs the active quests' current phases name (GameSession.QuestActorsNamed, every actor poll). One
    /// heard earlier, then done or set aside, leads again as a quest step once a phase starts naming its SNO, though it is out of
    /// earshot: the key found, the phase names the gate (live 2026-10-17, the Light's Watch: the guide fell silent with every
    /// tile explored, and the gate sounded again only once the player came back within earshot).</summary>
    public void ObserveNamed(IReadOnlySet<int> snos)
    {
        lock (_gate)
        {
            foreach (var objective in _objectives.Where(o => snos.Contains(o.Sno) && !_namedSnos.Contains(o.Sno) && (!o.Pending || o.Reached) && !o.UsedByStep))
            {
                objective.Pending = true;
                objective.Reached = false;
                objective.TimesLeft = 0;
                objective.Quest = true;
                if (!SetAsideIfWalked(objective)) Log("named by the quest", objective); // a tile's trigger walked: still behind
            }
            _namedSnos = snos;
        }
    }

    private IReadOnlySet<int> _namedSnos = new HashSet<int>();

    /// <summary>A followed quest's phase moved on (GameSession.QuestMovedOn): the objective the player stands at was the step, and is
    /// done though the game marks no use on it. A later phase naming its kind doesn't call it back from afar; looping again, it
    /// leads again. Guulrahn's jail gate, opened to free Oyun, drew the guide back when the escape's step named its kind again
    /// (tester, 2026-10-28; research/dungeon-objectives.md §29).</summary>
    public void QuestMovedOn()
    {
        lock (_gate)
            foreach (var objective in _objectives.Where(o => o.Pending && o.AtIt))
            {
                objective.Pending = false;
                objective.AtIt = false;
                objective.UsedByStep = true;
                Log("done (quest moved on at it)", objective);
            }
    }

    // A gizmo the phase names, placed by a loaded tile or marked on the map, is an objective before it's heard. It's done once
    // silent near the player (already used), or when the phase stops naming it; a tile's copy with nothing there was never spawned.
    private void ObserveAhead(List<QuestPlace> placed)
    {
        var named = new HashSet<Objective>();
        foreach (var place in placed)
        {
            var objective = _objectives.FirstOrDefault(o => o.Sno == place.ActorSno && Distance(o.Position, (place.X, place.Y)) <= SameSpotUnits);
            if (objective is null)
            {
                objective = new Objective(place.ActorSno, NameOf(place.ActorSno), (place.X, place.Y), Reach(place.ActorSno))
                    { Quest = true, PlacedOnly = true, Z = place.Z };
                _objectives.Add(objective);
                Log(place.Marked ? "seen ahead (map)" : "seen ahead", objective);
                SetAsideIfWalked(objective);
            }
            objective.Marked = place.Marked || (named.Contains(objective) && objective.Marked);
            named.Add(objective);
        }
        foreach (var objective in _objectives.Where(o => o.PlacedOnly && !named.Contains(o)))
        {
            objective.Marked = false;
            if (!objective.Pending) continue;
            objective.Pending = false;
            Log("done (phase moved on)", objective);
        }
    }

    /// <summary>Takes one actor poll (ActorLoopBridge.ObjectivesSeen): the objective actors looping and silent, and every actor read.</summary>
    public void Observe(ObjectiveSighting seen)
    {
        lock (_gate)
        {
            _escorts = [.. seen.Present.Where(a => _escortRadius(a.ActorSno) is not null)];
            _bossFight = seen.Looping.Concat(seen.Silent).Any(a => !a.ClientOnly && _isBoss(a.ActorSno));
            foreach (var objective in _objectives)
                objective.MissingPolls = Among(objective, seen.Present) ? 0 : objective.MissingPolls + 1;
            CountNpcPlacesMissing(seen);
            var heard = new HashSet<Objective>();
            foreach (var actor in seen.Looping)
            {
                var objective = Find(actor) ?? Add(actor);
                objective.ActorId = actor.Id;
                objective.ClientOnly = actor.ClientOnly;
                objective.Position = (actor.X, actor.Y);
                objective.Z = actor.Z;
                objective.Quest = actor.QuestStep;
                objective.PlacedOnly = false;
                // Done and looping again is a new step at it: a boss silent while invulnerable, sounding again once the player
                // carries his chain, leads though the player left him earlier (live 2026-10-21, Inarius).
                if (!objective.Pending)
                {
                    Log("looping again", objective);
                    objective.Reached = false;
                    objective.UsedByStep = false;
                }
                objective.Pending = true;
                heard.Add(objective);
            }
            SetAsideNpcPlaces(heard);
            if (_player is not { } here) return;

            var done = _objectives.Where(o => o.Pending && !heard.Contains(o) && Distance(o.Position, here) <= ConfirmRadiusUnits && Ended(o, seen)).ToList();
            foreach (var objective in done)
            {
                objective.Pending = false;
                objective.AtIt = false;
                Log("done", objective);
                foreach (var place in _places.Where(p => p.Pending && p.Sno == objective.Sno && Distance(p.Position, objective.Position) <= PlaceOfActorUnits))
                {
                    place.Pending = false;
                    place.AtIt = false;
                    place.ActorDone = true;
                    Log("done (its actor is done)", place);
                }
            }
            // A carryable taken lets the place its step waits on lead again (the Weeping Cairns' ward stone, back to Yorin); any other
            // objective done leaves quest places reached in this phase reached.
            if (done.Count > 0) RearmReached(MaxTimesLeft, places: done.Any(o => _carryableWaits(o.Sno) is not null));
        }
    }

    // A quest place an NPC marks is where it stood: it walked off (Tysa ran to Elias, tester 2026-10-29), so the place gives way once the NPC
    // is heard elsewhere, or once the player stands there and no copy of it is listed for a while. Heard again, the NPC itself leads.
    private void CountNpcPlacesMissing(ObjectiveSighting seen)
    {
        foreach (var place in _places.Where(p => _isNpc(p.Sno)))
            place.MissingPolls = _player is { } here && Distance(here, place.Position) <= ConfirmRadiusUnits
                && !seen.Present.Any(a => a.ActorSno == place.Sno && Distance(place.Position, (a.X, a.Y)) <= ConfirmRadiusUnits)
                ? place.MissingPolls + 1 : 0;
    }

    private void SetAsideNpcPlaces(HashSet<Objective> heard)
    {
        foreach (var place in _places.Where(p => p.Pending && _isNpc(p.Sno)))
        {
            bool elsewhere = heard.Any(o => o.Sno == place.Sno && Distance(o.Position, place.Position) > PlaceOfActorUnits);
            if (!elsewhere && place.MissingPolls < MissingPollsForDone) continue;
            place.Pending = false;
            place.AtIt = false;
            place.ActorDone = true;
            Log(elsewhere ? "done (its NPC is elsewhere)" : "done (its NPC is gone)", place);
        }
    }

    // Heard earlier: silent now, or missing for MissingPollsForDone polls. Not heard yet: its actor there is silent (used), or a
    // tile's copy the map doesn't mark has no actor. A door's or a trigger's copy loops as something else or not at all: it waits
    // for the phase. A carryable: once no copy lies on the ground there (the one in the player's hands has no game record).
    private bool Ended(Objective objective, ObjectiveSighting seen) =>
        _carryableWaits(objective.Sno) is not null ? !Stands(objective, [.. seen.Present.Where(a => !a.ClientOnly)])
        : !objective.PlacedOnly ? Among(objective, seen.Silent) || objective.MissingPolls >= MissingPollsForDone
        : Stands(objective, seen.Silent) || (!objective.Marked && !Stands(objective, seen.Present));

    // A quest place this close to an objective heard is that objective's spot: the place is a marker, the actor stands about there
    // (ActiveQuests.MarkerReach).
    private const float PlaceOfActorUnits = ActiveQuests.MarkerReach;

    // Its own actor (a boss moves), or one of its kind on its spot (a gizmo re-created under a new id); one with a game record when
    // the one heard had one: a killed Hatred Spire leaves a client-side copy standing on its spot, and the spire never read done
    // (live 2026-10-24, Skartara's mindscape 4: the guide held at a destroyed spire and led back to another).
    private static bool Among(Objective objective, IReadOnlyList<ActorState> actors) =>
        actors.Any(a => a.ActorSno == objective.Sno && (objective.ClientOnly is not { } clientOnly || a.ClientOnly == clientOnly)
            && (a.Id == objective.ActorId || Distance(objective.Position, (a.X, a.Y)) <= SameSpotUnits));

    // Heard here, or marked on this world's map; a quest place comes from script data, whatever world it belongs to, unless the
    // map marks its trigger there (live 2026-10-22: lock room B's, on a tile not loaded yet).
    private bool Located(Objective objective) =>
        _objectives.Contains(objective) ? !objective.PlacedOnly || objective.Marked : objective.Marked;

    private static bool Stands(Objective objective, IReadOnlyList<ActorState> actors) =>
        actors.Any(a => a.ActorSno == objective.Sno && Distance(objective.Position, (a.X, a.Y)) <= SameSpotUnits);

    /// <summary>Lets the objectives the player walked away from lead again (the guide was just switched on by a lost player).</summary>
    /// <param name="walkedThrough">Triggers the player walked through and quest places reached in this phase too: nothing is left to explore (ExplorationGuideBridge).</param>
    public void Rearm(bool walkedThrough = false)
    {
        lock (_gate) RearmReached(int.MaxValue, walkedThrough, places: walkedThrough);
    }

    // Another objective done may have unlocked one set aside; one left undone MaxTimesLeft times waits for the guide's toggle
    // (live 2026-10-16: the Light's Watch lectern drew the guide back four times, and the second guard was never found). A
    // trigger walked through waits for nothing left to explore: in the Necropolis each tumor broken led back to the chamber's
    // triggers, and switching the guide on led back through the gate just opened (tester, 2026-10-27). So does
    // a quest place reached while its phase lasts: a phase may name several triggers, only the last moving it on (live 2026-10-23:
    // the Tunnels of Hefaetrust's dialog triggers led in circles after a toggle, and in the Kulle wing a lorebook read sent the
    // player back onto the anvil already stood on).
    private void RearmReached(int belowTimesLeft, bool walkedThrough = false, bool places = true)
    {
        foreach (var objective in (places ? _places.Concat(_objectives) : _objectives).Where(o => o.Reached && o.TimesLeft < belowTimesLeft && (walkedThrough || !o.WalkedThrough)))
        {
            objective.Reached = false;
            Log("re-armed", objective);
        }
    }

    private void OnPlayer((float X, float Y) here)
    {
        _player = here;
        _trail.Add(((int)MathF.Floor(here.X), (int)MathF.Floor(here.Y)));
        int? step = CurrentStep();
        foreach (var objective in _objectives.Concat(_places).Where(o => o.Pending && !o.Reached && (o.Step == 0 || o.Step == step)))
        {
            float distance = Distance(objective.Position, here);
            if (distance <= objective.Radius)
            {
                if (objective.AtIt) continue;
                objective.AtIt = true;
                objective.WalkedThrough |= UnheardTrigger(objective);
                Log("reached", objective);
            }
            else if (objective.AtIt && distance > LeaveDistance(objective) && _bossFight)
                objective.AtIt = false; // a fight's objective is used again and again: never set aside
            else if (objective.AtIt && distance > LeaveDistance(objective))
            {
                objective.AtIt = false;
                objective.Reached = true;
                objective.TimesLeft++;
                Log("left undone", objective);
            }
        }
    }

    // Of a sub-quest walked in order, the first step with a place left: the game takes its triggers only then. A place the waiting
    // phase names itself leads too (live 2026-10-23: the Holdout tunnel's trigger, past a door, came before the seal's steps), unless
    // it lies by a later step's place: Tal Rasha's seal and the trigger beside it stood behind a locked door until then.
    private int? CurrentStep() => _places.Where(o => o.Step > 0 && o.Pending && !o.Reached).Select(o => (int?)o.Step).Min();

    /// <summary>A place of the waiting phase this close to a later step's place waits for that step.</summary>
    public const float LaterStepUnits = 10f;

    private bool Leads(Objective place)
    {
        if (CurrentStep() is not { } step) return true;
        if (place.Step > 0) return place.Step == step;
        return !_places.Any(o => o.Step > step && o.Pending && !o.Reached && Distance(o.Position, place.Position) <= LaterStepUnits);
    }

    // Walking this far sets it aside. A trigger: just out of it, as the game fired it on entering. Live 2026-10-01 (the
    // Necropolis, phase 154) the trigger was a hidden callback and the shown step a region farther on; holding the guide on it
    // until the phase moved on pulled the player back to it from every room (tester, 2026-10-27). Another quest place: just
    // out of it while another place is still to reach or an item to carry still to find, else never (talking or using it is
    // the step).
    private float LeaveDistance(Objective objective) =>
        objective.WalkedThrough ? objective.Radius * PlaceLeaveFactor
        : !_places.Contains(objective) ? LeaveUnits
        : _places.Any(o => o != objective && o.Pending && !o.Reached) || CarryToFind() ? objective.Radius * PlaceLeaveFactor : float.PositiveInfinity;

    private const float PlaceLeaveFactor = 1.5f;

    // The step names an item to carry, or where it goes, that nothing here has shown yet: its work starts elsewhere. The Weeping
    // Cairns' step at the sealed tomb gate names Yorin there and a ward stone that an altar deeper in gives; held at Yorin, the
    // guide led back to the gate from everywhere (tester, 2026-10-28). Once the stone is found and taken, Yorin leads again
    // (another objective done).
    private bool CarryToFind() =>
        _namedSnos.Any(sno => (_carryableWaits(sno) is not null || _takesHeld(sno) is not null) && !_objectives.Any(o => o.Sno == sno));

    // A trigger the quest names by place, or a tile places: it has no loop and no "used" state.
    private bool UnheardTrigger(Objective objective) =>
        _triggerRadius(objective.Sno) is not null && (objective.PlacedOnly || _places.Contains(objective));

    // A trigger named on ground the player already walked is behind them: the copy in a scene the dungeon repeats that was
    // passed (the Cradle's two holdout tunnels, tester 2026-10-27), the trigger of an event already fought. It waits like one
    // walked through. One the player stands near now is reached as usual.
    private bool SetAsideIfWalked(Objective objective)
    {
        if (!UnheardTrigger(objective) || _player is not { } here || Distance(objective.Position, here) <= objective.Radius * PlaceLeaveFactor) return false;
        if (!objective.WalkedThrough && !Walked(objective)) return false;
        if (!objective.WalkedThrough) Log("walked through earlier", objective);
        objective.WalkedThrough = true;
        objective.Reached = true;
        return true;
    }

    private bool Walked(Objective objective)
    {
        float r = objective.Radius;
        var (x, y) = objective.Position;
        for (int cx = (int)MathF.Floor(x - r); cx <= (int)MathF.Floor(x + r); cx++)
            for (int cy = (int)MathF.Floor(y - r); cy <= (int)MathF.Floor(y + r); cy++)
                if (_trail.Contains((cx, cy)) && Distance((cx + 0.5f, cy + 0.5f), (x, y)) <= r) return true;
        return false;
    }

    // How close counts as there: inside a trigger, else within reach of the object's edge.
    private float Reach(int sno) => _triggerRadius(sno) ?? (_footprint(sno) ?? 0) + InteractUnits;

    private string NameOf(int sno) => _nameOf(sno) ?? sno.ToString();

    private Objective? Find(ActorState actor) =>
        _objectives.FirstOrDefault(o => o.ActorId == actor.Id && o.Sno == actor.ActorSno)
        ?? _objectives.FirstOrDefault(o => o.Sno == actor.ActorSno && Distance(o.Position, (actor.X, actor.Y)) <= SameSpotUnits);

    private Objective Add(ActorState actor)
    {
        var objective = new Objective(actor.ActorSno, NameOf(actor.ActorSno), (actor.X, actor.Y), Reach(actor.ActorSno));
        _objectives.Add(objective);
        Log(actor.QuestStep ? "seen (quest)" : "seen (dungeon)", objective);
        return objective;
    }

    private void Log(string what, Objective objective) =>
        _log?.Invoke($"OBJECTIVE {what}: {objective.Name} at ({objective.Position.X:F1},{objective.Position.Y:F1})");

    private static float Distance((float X, float Y) a, (float X, float Y) b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>A remembered objective as ExplorationStore saves it.</summary>
public sealed record SavedObjective(int Sno, string Name, float X, float Y, bool Quest);
