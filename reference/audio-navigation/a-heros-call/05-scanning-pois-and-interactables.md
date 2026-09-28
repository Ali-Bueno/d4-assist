# 05 — POI Scanning, Examine & Interact

Source: `FirstPerson/Ian/POIHelpers.cs`, `POIList.cs`, `POI.cs`, plus the action handlers in
`FPExploring.cs`. Interactable data lives in `RPG/Ian/MapScriptObject.cs` + `MSOStrain.cs`.

Where the radar (Doc 02) is *continuous ambient* feedback, **scanning is on-demand speech**: "tell me
what's in front of me." A scan casts a ray and produces an ordered, spoken list of everything notable
along it — region transitions, walls/doors, objects — each with a distance.

---

## 1. The scan raycast — `GetLineCollisionWithPointsOfInterest`

```csharp
public static POIList GetLineCollisionWithPointsOfInterest(
        IFPWorld ent, V2 directionUnitVector, float maxDistance)
{
    IClientMap map = ent.Map;
    POIList list = new POIList();
    FPTile playerTile = FSO.FPML.GetPlayerTile(ent);
    TString curRegion = playerTile.Region.FriendlyName;
    V2 position = ent.Position;
    float step = 0.1f;                                  // march granularity (tiles)
    V2 v = step * directionUnitVector;

    // First: does the ray hit a map OBJECT? (separate circle/segment test, Doc 03 CH.GetObjectCollision)
    SObjectCollision oc = CH.GetObjectCollision(position, directionUnitVector, maxDistance,
                                                FSO.FPML.GetVisibleObjects(ent.Map.GetObjects()));
    IClientMapObject obj = oc?.Object;

    for (float d = 0f; d <= maxDistance; d += step) {
        position += v;
        int tx = (int)Math.Floor(position.X), ty = (int)Math.Floor(position.Y);
        if (tx < 0 || ty < 0 || tx > map.PaddedWidth || ty > map.PaddedHeight) return null;
        FPTile tile = map.Tiles.Get(tx, ty);
        if (tile == playerTile) continue;

        // (1) Wall / door blocks the ray -> final POI, stop.
        if (tile.Terrain.Wall || tile.Terrain.Door) {
            AddRangeToLastRegion(list, curRegion, d);
            AddWallPOI(list, position, d, tile);        // "<region> <terrain> <dist>"
            return list;
        }
        // (2) Object within its radius -> object POI; focus it if within 2.5; stop.
        if (obj != null && (position - obj.Position).Length < obj.Radius) {
            AddRangeToLastRegion(list, curRegion, d);
            POI p = AddSObjectPOI(list, position, d, tile, obj);   // "<name> <dist>"
            if (d <= 2.5f) list.FocusedInteractible = p;           // <-- close enough to interact
            list.FirstInteractible = p;
            return list;
        }
        // (3) Region/terrain name changed -> emit a section POI at this boundary.
        if (GetSectionName(tile) != curRegion) {
            AddRangeToLastRegion(list, curRegion, d);
            if (GetSectionName(tile) == FPRegion.None.FriendlyName) {
                if (list.Count == 0) AddEndOfRegionPOI(list, curRegion, position, d, tile);
            } else {
                AddSectionPOI(list, position, d, tile);            // "<region|terrain> <dist>"
            }
            curRegion = GetSectionName(tile);
        }
    }
    AddRangeToLastRegion(list, curRegion, d);
    AddOutOfSightPOI(list, position, d, null);          // "As far as I can see <dist>"
    return list;
}

private static TString GetSectionName(FPTile t)
    => t.Terrain.Impassable ? t.Terrain.FriendlyName : t.Region.FriendlyName;
```

### Impassable terrain is its own named "section", not the region

Note the beam only **stops** on `Wall || Door` (step 1 above) — `Impassable` terrain does NOT block it.
Instead, crossing into an `Impassable` tile trips the "section changed" check (step 3): `GetSectionName`
returns the terrain's own `FriendlyName` for `Impassable` tiles, and only falls back to the enclosing
`Region.FriendlyName` otherwise (`POIHelpers.cs:86-93`). So an impassable clump (rubble, water, a pit —
whatever the map author tagged `Impassable`) interrupts the region reading and is announced by its own
terrain name, exactly like a region change would be, even though the player could be standing right next
to it with clear line of sound past it.

### What the spoken strings look like (localized via `TranslateSO._`)

| Builder | Text template | When |
|---------|---------------|------|
| `AddWallPOI` | `"{region} {terrain} {dist}"` or `"{terrain} {dist}"` | ray hits wall/door |
| `AddSObjectPOI` | `"{objectFriendlyName} {dist}"` | ray hits an object |
| `AddSectionPOI` | `"{region|terrain} {dist}"` | crossing into a new named region/impassable |
| `AddEndOfRegionPOI` | `"end of {region} {dist}"` | region runs out into unnamed space |
| `AddOutOfSightPOI` | `"As far as I can see {dist}"` | nothing blocked within maxDistance |
| `AddRangeToLastRegion` | appends a second number to make a **range** `"… {from} {to}"` | a region spanned >1 tile before the next POI |

`Math.Round(distance)` is used for spoken distances. `AddRangeToLastRegion` (`POIHelpers.cs:161-173`) is
the clever bit that turns "Corridor 3" + "Corridor 7" into a spoken **range** so a long stretch reads as
one item — it appends the trailing end-distance to the previous POI's text **only when that section spans
more than one tile** (`num2 - num > 1`, `:168`).

Two edge details worth copying:
- `AddEndOfRegionPOI` ("`end of {region} {dist}`") fires **only when the list is still empty**
  (`POIHelpers.cs:68-73`) — i.e. the region ran out into unnamed space before anything else was found;
  once other POIs exist, running into unnamed space is silent.
- The object hit uses a **single precomputed** `CH.GetObjectCollision` taken once before the march
  (`POIHelpers.cs:21`) and then merely radius-tested against the marched position each step
  (`:48-61`); the 0.1-tile march itself only walks *tiles* — it never re-runs object collision.
- The terminal `AddOutOfSightPOI` ("`As far as I can see {dist}`") is appended when nothing blocked the
  ray within `maxDistance` (`POIHelpers.cs:82,122-130`).

`POIList` is just `List<POI>` plus two slots: `FocusedInteractible` (object within 2.5 tiles, the one
examine/interact will act on) and `FirstInteractible`. When spoken, the POI texts are **joined by `", "`**
(`AnnouncePOIs` → `TranslateSO.NTJoin(", ", …)`, `FPExploring.cs:540`).

### Door audibility rides the auto-scan, not the radar — two separate channels (2026-07 verified)

The perception that "doors are audible from far away" is **not** a radar (Doc 02) feature — it comes from
this scan, run automatically every `0.1` s out to `GameConfig.ScanDistance = 30` tiles
(`GameEngine\Ian\GameConfig.cs:59`), stopping at the **first** `Wall` or `Door` tile (`POIHelpers.cs:42-47`,
step 1 above). Two distinct audible channels result from a door being that first blocker:

- **Panned door cue from the scan itself.** When the scan's first hit is a door tile, a directional door
  sound plays — hard-left/center/hard-right variants initialised at `FPExploring.cs:139-141` and played at
  `:461-485`, keyed off which of the three scan rays (front/left/right, §2 below) found the door.
  Occlusion here is inherent: because the march stops at the *first* `Wall || Door` tile, a door behind
  another wall is never reached and never sounds.
- **Positioned enter/exit sounds that bypass door occlusion.** Separately, doors emit their own positioned
  enter/exit sounds with `HeardThroughDoors=true` (`FModSoundContext.cs:587-607`), triggered from
  `FPMapLogic.cs:199-210` — these explicitly **ignore door tiles when summing occlusion depth** (contrast
  with `ObstructionHelpers.GetWallAndDoorDepth`, Doc 02's radar-vs-occlusion scope note), so a door sound can
  be heard *through* the door it belongs to. This positioned channel has its own mute distance of `13` tiles
  (`FModEventWrapper.cs:96`), independent of `ScanDistance`.

Keep these three systems distinct when porting: the **radar** (Doc 02) never mentions doors as a
far-audible feature; **this scan's panned door cue** is occlusion-limited to the first blocker; the
**positioned door sound** is a separate always-through-doors channel with its own cutoff.

---

## 2. Where scans are triggered (`FPExploring`)

| Trigger | Direction | Distance | Speech |
|---------|-----------|----------|--------|
| `RunForwardScan` (key `FPScanForward`) | `Front` | `GameConfig.ScanDistance` | `AnnouncePOIs(list)` — **INTERRUPT** (`interruptSpeech:true`) |
| `RunLeftScan` (`FPScanLeft`) | `Left` | `ScanDistance` | **none** — `AnnounceSidePOIs` discards the list (see below) |
| `RunRightScan` (`FPScanRight`) | `Right` | `ScanDistance` | **none** — `AnnounceSidePOIs` discards the list (see below) |
| `RunForwardComparison` (auto, every frame) | `Front` | `ScanDistance` | speaks only when the POI list **changed** *and* you're not walking straight / mouse-turning |

> **Manual side scans speak nothing.** `AnnounceSidePOIs` (`FPExploring.cs:529-532`) computes the POI list
> and then only calls `CancelAllSpeech` and **discards it** — it never speaks. Side awareness in AHC is
> therefore **audio-cue only**: it comes from the per-frame `RunSideComparison` (`FPExploring.cs:441-459`),
> which likewise never speaks — it only fires `PlayInteractibleInDistanceSound` (Doc 02). The left and
> right comparisons share **one** throttle timestamp `LastSideComparison` (`:443,450`), so they can't both
> re-fire within the same `0.1 s` window. Pressing the side-scan keys still runs the raycast (useful if a
> port wants to *add* spoken side readouts) but in the shipped game they are effectively silent no-ops.

### Auto-forward scan (`RunForwardComparison`) — the "look ahead while walking" feature

```csharp
// FPExploring.cs:391-423
if (!announcedRegion && (Now - LastForwardComparison).TotalSeconds < 0.1) return;   // 10 Hz cap
POIList list = POIHelpers.GetLineCollisionWithPointsOfInterest(mWorld, Front, ScanDistance);

ChangeFocusedUnit(list.FocusedInteractible?.SObject);     // updates the currently-targeted object

if (!POIHelpers.ArePOIsSame(list, LastForwardPois)) {     // only act on change
    LastForwardComparison = Now;
    PlayFocusSoundForNewlyFocusedInteractibles(list);
    LastForwardPois = list;
    bool flag  = LastTravel.AlmostEquals(Front);          // walking straight forward   (:411)
    bool flag2 = LastTravel.AlmostEquals(Back);           // walking straight backward  (:412)
    if (!flag && !flag2)                                  // center radar-style cue     (:415)
        PlayInteractibleInDistanceSound(list, RadarDirection.Center);
    if (AutoForwardScanEnabled && !flag && !flag2 && !mTurnedWithMouseThisLoop)   // (:417)
        AnnouncePOIs(list, !announcedRegion && !mIsFirstPOISCheck);               // (:419)
}
```

> **The real suppression is straight-line travel, not just the diff-gate.** Both the center radar cue
> (`:415`) **and** the spoken `AnnouncePOIs` (`:417`) require `!flag && !flag2` — i.e. you are *not*
> travelling straight forward or straight backward. So **walking straight toward what you face produces no
> spoken forward list at all**; the auto-scan speaks only when you **strafe**, **stand and turn**, cross a
> **region boundary** (which sets `announcedRegion`, bypassing the `0.1 s` throttle at `:393`), or press
> the **manual scan key**. The spoken call additionally needs `AutoForwardScanEnabled` and
> `!mTurnedWithMouseThisLoop`, and its interrupt flag is `!announcedRegion && !mIsFirstPOISCheck` — so
> right after an "entering X" announcement it **queues behind it** instead of cutting it off (Doc 06 §5).

Key design choices to copy:
- **Diff-gated** (`ArePOIsSame` compares each `POIText`): never re-act on an unchanged view.
- **Suppress the spoken read while moving straight forward/back or while mouse-turning** — those are
  exactly the cases where the radar (and the straight-ahead footstep flow) already cover you and
  re-reading would be chatter.
- Toggle with `ToggleAutoForwardScanEnabled`.

---

## 3. Interactables: `MapScriptObject` / `MSOStrain`

An interactable is a `MapScriptObject` (implements `IAudibleObject`, `IClientMapObject`, etc.). Its
template ("strain") `MSOStrain` carries the data the navigation/audio systems read:

```csharp
// MSOStrain (template) — the navigation-relevant fields
string Interact;                 // script snippet name to run on interact (blank = not interactable)
TString Description;             // examine text
string FocusSound;               // 3D one-shot when this becomes the focused object
string RadarSound;               // directional radar sound id
string ConstantSound;            // looping ambient (3D positioned, occluded)
ConstList<string> SequenceSoundStrings;  // sequence of vocal/sfx
float Radius;                    // circle radius used by the scan's object-hit test
bool HeardThroughWalls;          // bypass occlusion
bool SilentInMenus;              // mute while a menu is open
```

The `MapScriptObject` exposes `RenderPosition`, `FriendlyName`, `Interact`, `Description`, etc.; the
audio system (Doc 08) starts/stops its sounds as it enters/leaves the active actor set.

---

## 4. Examine & Interact (`FPExploring`)

```csharp
// FPExamine: ray 5 tiles along Front; speak description (or name if none)
void AttemptExamine() {
    POIList list = POIHelpers.GetLineCollisionWithPointsOfInterest(mWorld, Front, 5f);
    var so = list.FocusedInteractible?.SObject;
    if (so == null) { DiagSO.Say("Nothing to examine"); return; }
    TranslateSO.TSay(so.Description.IsBlank() ? so.FriendlyName : so.Description);
}

// FPInteract: ray 5 tiles; run the object's Interact script (FPExploring.cs:678-713)
void AttemptInteract() {
    POIList list = POIHelpers.GetLineCollisionWithPointsOfInterest(mWorld, Front, 5f);
    var so = list.FocusedInteractible?.SObject;
    if (so != null) {
        if (!so.Interact.IsBlank()) {
            mWorld.FPState.InteractedUnit = so;
            FSO.FPL.RunSnippet(mWorld, so.Interact, out _, out bool openedMenu);
            if (openedMenu) so.Message(FPMessages.PlayerStartedTalkingToYou);
        } else DiagSO.Say("No interaction available");     // focused object, but no script  (:695)
    } else if (mCurrentPortal != null) {                    // no object -> use the portal    (:698)
        SwitchClientMap(mCurrentPortal.DestMap);
        Player.Position = new V2(DestX + 0.5f, DestY + 0.5f);
        if (mCurrentPortal.DestFacing.HasValue) { Facing = DestFacing; AnnounceDirection(); }
    } else DiagSO.Say("Nothing to interact with");          // nothing focused, no portal     (:711)
}
```

There are **three** distinct fallbacks, not one: `"No interaction available"` (an object is focused but
its `Interact` script is blank), a **portal teleport** (nothing focused but the player is standing on a
portal — this is how doors/exits are actually taken), and `"Nothing to interact with"` (nothing focused
and no portal). Examine's single fallback is `"Nothing to examine"` (`:715-732`), and it speaks the
object's `Description` when present else its `FriendlyName`.

Note both use **range 5** (not the long scan distance) and act on `FocusedInteractible` — i.e. the
object must be within the **2.5-tile focus window** set during the scan to be acted on, but the ray is
allowed to reach 5 to find/aim at it. When an object becomes focused, `ChangeFocusedUnit` plays its
`FocusSound` (a 3D cue at the object), giving audible confirmation of what you're targeting.

---

## 5. Porting notes

- **Correction:** the scan is *not* the Doc 03 DDA raycaster wearing a different hat — it's an
  independent fixed **0.1-tile point-march** (`GetLineCollisionWithPointsOfInterest`, §1 above) that
  stops on `Wall || Door` (never on `Impassable`, which becomes its own named section instead, per the
  note above). Conceptually the two systems do the same job ("walk along a ray, name what you cross")
  but they are two separate implementations with different stopping rules — don't reuse one to
  reimplement the other when porting. In a 3D game, replace the tile march with a physics ray (or
  several rays in a small fan) and classify hits: wall layer → "wall", enemy/interactable layer → that
  entity's name, trigger volumes → region name. Keep the **0.1-tile march / fine step** idea if you want
  region-boundary distances; otherwise use the physics hit distance directly.
- Reproduce **diff-gated auto-scan** and the **2.5 focus / 5 act** split; both massively reduce verbal
  clutter while keeping a reliable "what am I aimed at" channel — directly useful for SF6 (which enemy
  am I facing + range) and RE7 (what's the interactable/door ahead).
- Spoken distance = `round(rayDistance)`; range-collapse consecutive same-name hits.
