# 06 — Zones, Regions & Boundary Announcements

There are **two distinct spatial-grouping concepts** in A Hero's Call. Keeping them separate is
important when porting.

| Concept | Type | Purpose | Player feedback |
|---------|------|---------|-----------------|
| **Region** | `FPRegion` (per tile, `tile.Region`) | Named navigable area ("the Armory", "East Corridor") | **Speech** on enter/leave; named in scans |
| **Zone** | `Zone : IAudibleZone` (per tile, `tile.ZoneName`) | Acoustic environment (reverb/snapshot) | **Audio** reverb change (no speech) |

A tile carries both a `Region` (gameplay/orientation) and a `ZoneName` (acoustics). They often overlap
spatially but answer different questions: *"where am I?"* vs *"what does this space sound like?"*.

> **Neither Region nor Zone is geometry.** A region/zone is nothing but a tag (`tile.Region.FriendlyName`
> / `tile.ZoneName`) painted onto individual tiles by the map author — there is no rectangle, polygon, or
> radius anywhere in `FPRegion`/`Zone`. It is simply "the set of tiles that carry this tag." The **only**
> rectangular constructs in the game are `Portal` and `AreaTrigger` (§2 below) — don't conflate the two
> when porting: a region boundary is "the tile tag changed," a portal/trigger boundary is "I crossed
> into/out of an integer rect."

For reference, the terrain flags a tile carries (independent of Region/Zone) are `Wall, Impassable,
InDoors, Door, Road, Path` (`FPTerrain.cs:31-41`); passability is `CanPass = !Wall && !Impassable`
(`FPTerrain.cs:60-70`) — note **`Door` is NOT part of `CanPass`**, so a pure door tile is walkable even
though it still blocks the DDA rays and the POI scan beam (Doc 03 §5, Doc 05 §1).

---

## 1. Region announcements (speech) — `FPExploring.HandleRegion`

Already shown in Doc 04 §7. Per frame: read the region of the tile under the player; if it differs from
the remembered region, **cancel current speech** and say `entering {name}` (or `leaving {name}` when
stepping into unnamed space). Returns a "changed" flag that forces a fresh forward POI scan so the new
area is immediately described.

```csharp
FPRegion region = map.Tiles.Get(floor(Position.X), floor(Position.Y)).Region;
if (region != mCurrentRegion) {
    mWorld.Speech.CancelAllSpeech();
    if (region.IsNone() && mCurrentRegion != null) TranslateSO.TSay("leaving {0}",  mCurrentRegion.FriendlyName);
    else                                           TranslateSO.TSay("entering {0}", region.FriendlyName);
    mCurrentRegion = region;
}
```

Regions also surface in scans (Doc 05): `AddSectionPOI` ("`{region} {dist}`"), `AddEndOfRegionPOI`
("`end of {region} {dist}`"), and the range-collapsing in `AddRangeToLastRegion`.

`FPRegion.None` is the sentinel for "no named region"; `region.IsNone()` / comparing against
`FPRegion.None.FriendlyName` is how unnamed space is detected.

> **"leaving X" only fires into unnamed space.** The `leaving` branch (`FPExploring.cs:180-183`) requires
> the **new** region to be `IsNone()` while the old one was non-null. So a transition straight from named
> region A into named region B says **only "entering B"** — you never hear "leaving A". The mod interrupts
> current speech first (`CancelAllSpeech`, `FPExploring.cs:179`) so the announcement of the newest area
> always wins.

**Impassable terrain announces as its own section, separate from the region.** In scans (Doc 05),
`POIHelpers.GetSectionName` returns the tile's terrain `FriendlyName` when `Terrain.Impassable` is set,
and only falls back to `Region.FriendlyName` otherwise (`POIHelpers.cs:86-93`). So an impassable clump
interrupts a region reading and is named by its own terrain, not by the region it happens to sit in.

---

## 2. Portals & AreaTriggers — the only rectangular constructs

Unlike Region/Zone (per-tile tags with no shape), `Portal` and `AreaTrigger` are genuine axis-aligned
integer rectangles: `X1, Y1, X2, Y2` (`Portal.cs`, `AreaTrigger.cs`), tested with an **inclusive**
`IsInRange(x,y)` (`GameEngine/Ian/ExtensionMethodsForNet35.cs:106-113`), i.e. `X1 <= x <= X2 && Y1 <= y
<= Y2`.

- **`Portal`**: speaks the exact string `"Press the enter key to enter {0}"` and plays a
  `PlayPlain2D("Portal")` cue when the player enters its rect (`FPExploring.cs:201-202`) — **queued, not
  interrupting** (no `CancelAllSpeech`). On interact it teleports to the destination tile's *center*
  `(DestX + 0.5, DestY + 0.5)`, optionally switching to a different `IClientMap` and, if `DestFacing` is
  set, re-facing + `AnnounceDirection()` (sound only) (`FPExploring.cs:698-708`).
- **`AreaTrigger`**: fires scripted JS events — `OnEnter`, `OnExit`, `OnProcess`, `OnMove` — keyed on
  tile transitions in/out of its rectangle. `OnProcess` runs every frame the player is inside
  (`FPMapLogic.RunEnterAreaTriggers/RunExitAreaTriggers`, `FPMapLogic.cs:220-274`;
  `RunProcessAreaTriggers` called each frame from `FPExploring.cs:156`). These are a scripting hook, not
  a direct speech/audio feature — any narration they cause goes through normal `TranslateSO`/sound calls
  from the triggered script.

When porting: implement Region/Zone as per-tile tags (a lookup table keyed by grid cell), and
Portal/AreaTrigger as actual rect (or trigger-volume) checks — they are not interchangeable
representations of the same idea.

---

## 3. Zone acoustics (reverb snapshots) — `MapSoundHandler.UpdateMapSoundss`

Zones bind a tile area to an **FMOD environmental-reverb event** (`Zone.EnvironmentalReverb`). On map
load, each `IAudibleZone` becomes a `ZoneInfo { Name, EnvironmentalReverb, Event }` where `Event` is a
preloaded FMOD event instance.

Per frame, when the player's tile `ZoneName` changes, the old zone's snapshot is switched off and the
new one on, via an FMOD parameter `"StopSnapshot"` (0 = active, 1 = stopped):

```csharp
IAudibleTile curTile = map.GetTile(floor(playerPos.X), floor(playerPos.Y));
if (curTile != mLastTile && (mLastTile == null || curTile.ZoneName != mLastTile.ZoneName)) {
    if (mLastTile != null) {                          // turn OFF previous zone's reverb
        var prev = mZones.FirstOrDefault(z => z.Name == mLastTile.ZoneName);
        prev?.Event?.mEventInstance.setParameterValue("StopSnapshot", 1f);
    }
    var cur = mZones.FirstOrDefault(z => z.Name == curTile.ZoneName);   // turn ON current
    if (cur?.Event != null) {
        cur.Event.mEventInstance.setParameterValue("StopSnapshot", 0f);
        cur.Event.Play();
    }
}
mLastTile = curTile;
```

So crossing a zone boundary cross-fades the reverb character (e.g. dry corridor → cavernous hall)
purely through audio, with no announcement. The `IsIndoors` tile flag additionally feeds the FMOD
`"PlayerIndoors"`/`"SoundIndoors"` parameters per sound (Doc 03 §4, Doc 08 §3) to colour occlusion.

> **`IsIndoors` is a *terrain* property, not a zone one.** `IAudibleTile.IsIndoors` reads
> `Terrain.InDoors` (`FPTile.cs:64`) — it is a per-terrain flag entirely **independent of `ZoneName`**.
> Don't tie the `PlayerIndoors`/`SoundIndoors` occlusion colouring to acoustic zones when porting: a tile
> can be indoors under any zone, or outdoors under a "cave" zone. The obstruction/indoor status is set per
> sound from this terrain flag (`SetObstructionAndIndoorStatus`), and its change also drives the ambient
> bed crossfade (Doc 04 §2).

---

## 4. Interfaces

```csharp
// Sound/Ian/IAudibleZone.cs
public interface IAudibleZone { string Name; string EnvironmentalReverb; }   // (+ flags)
// tile side (Doc 03): IAudibleTile.ZoneName, IAudibleTile.IsIndoors
```

---

## 5. Speech pipeline & interrupt semantics

All spoken output funnels through **one FIFO queue** — there is **no priority system**:

- `TSay` / `Say` / `DSay` / `ESay` all route into `Diag.Write` (`Core/Ian/Diag.cs:7`), which is rebound to
  `ent.Speech.SpeakAsync` at startup (`RPG/Ian/RPGStartup.cs:235`).
- The only method that bypasses that path is `AnnouncePOIs`, which calls `Speech.SpeakLowPriority`
  directly — but **`SpeakLowPriority` and `SpeakAsync` are byte-for-byte identical**
  (`GameEngine/Ian/SpeechContext.cs:72` vs `:109`). The "low priority" name is misleading; both just append
  to the same queue.
- `CancelAllSpeech` (`SpeechContext.cs:80`) is itself a **queued command** — an announcement "interrupts"
  only because its handler enqueues a cancel immediately before the new text.

So "interrupt vs queue" is entirely about whether the caller runs `CancelAllSpeech` first:

| Announcement | Interrupt? | Why |
|---|---|---|
| Region enter / leave | **INTERRUPT** | explicit `CancelAllSpeech` (`FPExploring.cs:179`) before speaking |
| Manual forward scan (`RunForwardScan`) | **INTERRUPT** | `AnnouncePOIs(list)` defaults `interruptSpeech:true` |
| Auto forward scan (`RunForwardComparison`) | conditional | interrupts only when `!announcedRegion && !mIsFirstPOISCheck` (`:419`); right after a region announcement it **queues behind** "entering X" |
| Portal announcement | queue | no `CancelAllSpeech` (`:201`) |
| Examine / Interact | queue | plain `TSay` / `Say` |
| `AnnounceRegion` / `AnnouncePosition` / `AnnounceBeacon` | queue | plain `TSay` / `Say` |

`mIsFirstPOISCheck` is set `true` on `Enter` (`FPExploring.cs:111`) so the very first auto-scan after
entering a scene does not clobber the entry announcement.

---

## 6. Porting notes

- **Separate your two layers.** Use *named trigger volumes / nav regions* for the spoken "entering X"
  and *acoustic zones* for reverb. In Unity (7DTD) these can both be trigger colliders tagged
  differently; in RE Engine (SF6/RE7) hook the existing area/room ids if exposed.
- Region announce pattern: store `currentRegionId`; each frame compare with the region under the
  player; on change, **interrupt speech** then announce. Interrupting (not queueing) is deliberate —
  the newest area is what matters.
- Reverb switching maps cleanly to FMOD snapshots or to your engine's reverb zones; the
  `StopSnapshot 0/1` toggle is just "activate current, deactivate previous."
- Drive occlusion colour with an `indoors` boolean per emitter+listener if your game distinguishes
  interior/exterior; it noticeably improves the sense of space.
