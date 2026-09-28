# 03 — Grid Raycaster & Wall/Door Occlusion

Source: `Sound/Ian/CollisionHelperCopy.cs` (audio) and `FirstPerson/Ian/CH.cs` (gameplay).

> **Correction — there are TWO distinct ray systems in this game, not one.** This doc covers system
> (A), the grid **DDA**: "march a ray across a tile grid and report every tile boundary it crosses, in
> order." It is used by the Style-1 **reactive radar**, **audio occlusion**, **beacon line-of-sight**,
> and **object/script rays**. The forward/side **POI scans** — the primary spoken "what's ahead"
> readout — are **NOT** a thin wrapper over this DDA: they use a completely separate fixed **0.1-tile
> point-march**, `POIHelpers.GetLineCollisionWithPointsOfInterest` (Doc 05 §1), that stops on
> `Wall || Door` (not `Impassable`) and is only conceptually similar. Evidence:
> `Sound/Ian/ReactiveRadar.cs:165`, `FPExploring.cs:281`, `RPG/Ian/RPGScriptAPI.cs:158` (all DDA
> consumers) vs `POIHelpers.cs:12-84` (the point-march).
>
> **`CH.cs` is a near-identical copy of `CollisionHelperCopy`, but not byte-for-byte:** it orders
> crossings by **`ManhattanLength`** (`CH.cs:139`) where the audio copy uses **`LengthSquared`**
> (`CollisionHelperCopy.cs:91`), and the two expose different APIs — `CH` has `GetWallCollision`,
> `GetWallOrImpassableCollision`, `IsBehindWallOrDoor`, `GetObjectCollision`; the audio copy has the
> door-aware `GetWallOrImpassableOrDoorCollision`. Bounds checks differ syntactically (`PaddedWidth`/
> `PaddedHeight` in `CH.cs:113,131` vs `TileWidth`/`TileHeight` in the audio copy) but are equal values —
> `IAudibleMap.TileWidth/TileHeight` are just aliases for the Padded dimensions (`FPClientMap.cs:36-38`).
> The core traversal math itself (gridline stepping, `SolveForX`/`SolveForY`) is identical between the
> two and is independently verified line-for-line accurate below — only the sort key, API surface, and
> bounds-check spelling differ. This doc covers the canonical `CollisionHelperCopy` (audio) version.
>
> **The `ManhattanLength` vs `LengthSquared` sort keys produce a PROVABLY identical ordering, not merely
> "the same in practice".** Both helpers sort the *same* set of crossing points, and every one of those
> points is **collinear** — they all lie on the single ray `P0 + t·slope` (`t ≥ 0`), since each is
> produced by `SolveForX`/`SolveForY` on one `Line`. For collinear points parametrized by `t ≥ 0` from
> the shared origin `P0`, **both** Euclidean length (`|t|·|slope|`) and Manhattan length
> (`|t|·(|slope.x|+|slope.y|)`) are strictly increasing in `t` — each is just `t` times a positive
> constant — so they induce the **same total order**. `LengthSquared` is monotonic in Euclidean length,
> so it agrees too. Corner hits (both coordinates integer → the ray crosses an H and a V gridline at the
> same point) show up as **duplicates at equal `t`** in both sweeps, and tie identically under either
> key. Net: swapping the sort key changes nothing about the emitted tile order.

---

## 1. Public entry points

```csharp
// First wall/impassable/door tile the ray hits (null if none within scanDistance).
// Used by the Style-1 REACTIVE RADAR (not the POI scans — those use a different point-march, Doc 05).
public static AudibleTileCollision? GetWallOrImpassableOrDoorCollision(
        IAudibleMap map, V2 startPosition, V2 slope /*unit dir*/, float scanDistance)
{
    foreach (AudibleTileCollision c in GetLineToTileCollision(map, startPosition, slope, scanDistance))
        if (c.Tile.IsWall || c.Tile.IsImpassable || c.Tile.IsDoor)
            return c;
    return null;
}

// Total thickness (in tile-units) of walls + doors lying between source and target. Used by audio occlusion.
public static float GetWallAndDoorDepth(
        IAudibleMap map, V2 source, V2 target,
        bool heardThroughWalls = false, bool heardThroughDoors = false)
{
    float length = (source - target).Length;
    V2 slope = (target - source); slope.Normalize();
    List<AudibleTileCollision> hits = GetLineToTileCollision(map, source, slope, length);
    float depth = 0f;
    for (int i = 0; i < hits.Count - 1; i++) {
        var t = hits[i].Tile;
        if ((!heardThroughWalls && t.IsWall) || (!heardThroughDoors && t.IsDoor))
            depth += (hits[i + 1].Point - hits[i].Point).Length;   // span of this blocking tile along the ray
    }
    return depth;
}
```

`GetWallAndDoorDepth` returns **how many tile-units of solid wall/door the ray passes through**, by
summing the segment length the ray spends inside each blocking tile (distance from this crossing to
the next). This is the "obstruction" number that drives muffling and muting (Doc 08 §3).

---

## 2. The core: `GetLineToTileCollision` (a grid DDA)

This is a classic **grid traversal / DDA**: it finds every point where the ray crosses an integer
horizontal gridline (`y = k`) and every point where it crosses an integer vertical gridline (`x = k`),
identifies the tile on the entering side of each crossing, then sorts all crossings by distance from
the start. The result is the ordered list of tiles the ray passes through, with the exact entry point
of each.

```csharp
private static List<AudibleTileCollision> GetLineToTileCollision(
        IAudibleMap map, V2 startPosition, V2 slope, float scanDistance)
{
    V2 second = startPosition + slope;
    Line line = Line.FromPoints(startPosition.AsV3(), second.AsV3(), isSegment:false);
    V3 reach = line.Slope * scanDistance;            // ray extent as a vector
    float dy = Math.Abs(reach.Y);                    // how far in Y we travel
    float dx = Math.Abs(reach.X);                    // how far in X we travel
    var list = new List<AudibleTileCollision>();

    // --- horizontal gridline crossings (y = integer) -> gives tile column from solved X ---
    GetGridLineStartAndIncrement(startPosition.Y, slope.Y, dy, out int start, out int inc, out int end);
    if (!line.IsHorizontal)
        for (int i = start; i != end; i += inc) {
            float x = line.SolveForX(i);             // x where ray crosses y=i
            int tx = (int)Math.Floor(x);
            int ty = (inc == -1) ? (i - 1) : i;      // tile is below/above the line depending on direction
            if (!IsInRangeWithExclusiveMax(tx, ty, map.TileWidth, map.TileHeight)) break;
            list.Add(new AudibleTileCollision { Point = new V2(x, i), Tile = map.GetTile(tx, ty) });
        }

    // --- vertical gridline crossings (x = integer) -> gives tile row from solved Y ---
    GetGridLineStartAndIncrement(startPosition.X, slope.X, dx, out int start2, out int inc2, out int end2);
    if (!line.IsVertical)
        for (int j = start2; j != end2; j += inc2) {
            float y = line.SolveForY(j);             // y where ray crosses x=j
            int tx = (inc2 == -1) ? (j - 1) : j;
            int ty = (int)Math.Floor(y);
            if (!IsInRangeWithExclusiveMax(tx, ty, map.TileWidth, map.TileHeight)) break;
            list.Add(new AudibleTileCollision { Point = new V2(j, y), Tile = map.GetTile(tx, ty) });
        }

    // order all crossings by distance from the start (interleaves the H and V crossings correctly)
    return list.OrderBy(p => (p.Point - startPosition).LengthSquared).ToList();
}
```

### The stepping helper

```csharp
private static void GetGridLineStartAndIncrement(
        float startCoord, float slopeComponent, float distance, out int start, out int inc, out int end)
{
    if (slopeComponent > 0f) {                       // moving in +axis
        start = (int)Math.Ceiling(startCoord);       // first integer line ahead
        inc   = 1;
        end   = (int)Math.Floor(startCoord + distance);
    } else {                                          // moving in -axis
        start = (int)Math.Floor(startCoord);
        inc   = -1;
        end   = (int)Math.Ceiling(startCoord - distance);
    }
    end += inc;                                       // make 'end' exclusive for the != loop
}

private static bool IsInRangeWithExclusiveMax(int x, int y, int maxX, int maxY)
    => 0 <= x && x < maxX && 0 <= y && y < maxY;
```

### Notes that bite you when porting

- **Two separate sweeps + sort**, not a single incremental DDA. Simpler to reason about, slightly more
  allocation. For hot paths you can replace with an Amanatides–Woo single-pass DDA, but keep the output
  contract identical: ordered `(entryPoint, tile)` crossings.
- The `ty = (inc == -1) ? i-1 : i` (and the X analogue) chooses **which tile owns the crossing** based
  on travel direction — the tile you're *entering*. Get this wrong and occlusion off-by-one-tiles.
- Bounds check **breaks** the sweep (stops the ray at the map edge). `EPSILON = 0.0001f` exists in the
  class but the canonical paths above don't need it; `CH.cs` uses it for degenerate slopes.
- **The "map edge" the ray dies at is the PADDED grid's border, not the authored map's edge.** The map
  is allocated at `PaddedWidth/Height = Width/Height + 2` with a 1-tile ring force-set to the impassable
  `"map boundary"` terrain (`FPMapType.SetupTiles`, `FPMapType.cs:121-135,357-359`; Doc 01 §5). Every
  bounds check here (`map.TileWidth/TileHeight`, which alias the Padded dims, `FPClientMap.cs:36-38`)
  and in `CH.cs` (`PaddedWidth/PaddedHeight` directly) is against that padded frame, so a ray naturally
  terminates at that border tile rather than needing special-case edge handling.
- `EPSILON`, `IsHorizontal`/`IsVertical` guard the divide-by-zero cases (`SolveForX` divides by `A`,
  `SolveForY` by `B`).

---

## 3. `AudibleTileCollision` and the tile contract

```csharp
// Sound/Ian/AudibleTileCollision.cs
public struct AudibleTileCollision { public V2 Point; public IAudibleTile Tile; }

// Sound/Ian/IAudibleTile.cs  — the per-tile flags the raycaster consults
public interface IAudibleTile {
    int ID; string IndexedTerrainName; string StepSound;
    string ConstantSound; string SecondarySharedSound; bool UniqueEmit;
    string ZoneName;
    bool IsWall; bool IsImpassable; bool IsIndoors; bool IsDoor;
    int X; int Y;
}
// Sound/Ian/IAudibleMap.cs
public interface IAudibleMap {
    int TileWidth; int TileHeight; List<IAudibleZone> Zones; IAudibleTile GetTile(int x, int y);
}
```

So the **entire world model** the navigation system needs is: a width/height, `GetTile(x,y)`, and per
tile the booleans `IsWall / IsImpassable / IsDoor / IsIndoors`, a `ZoneName`, and the terrain/sound
strings. That is the whole adapter surface for a tile game.

> **Doors have no open/closed state anywhere in the game (verified 2026-07).** `Door` is a static
> per-terrain boolean (`FPTerrain.cs:37` → `FPTile.IsDoor`); a grep of the whole decompiled source finds
> no open/closed door concept at all. Every door-terrain tile blocks the radar beam identically, always.
> So when porting to a game with real openable doors, AHC gives no precedent — decide explicitly: a
> *closed* door should radar as blocking (it stops the player) but be distinguishable via the
> interactable/POI channel; an *open* door should not block the beam.

---

## 4. Occlusion → audio (how `GetWallAndDoorDepth` is consumed)

`Sound/Ian/ObstructionHelpers.cs`:

```csharp
public static void PositionSoundForObstruction(
        MapAndPlayer ent, V3 basePosition, IMovableSound sound,
        bool heardThroughWalls = false, bool silentInMenus = false, bool heardThroughDoors = false)
{
    if (silentInMenus && ent.AreWeInMenu) { sound.Mute = true; return; }

    float depth = CollisionHelperCopy.GetWallAndDoorDepth(
        ent.Map, ent.PlayerPosition, basePosition.Xy, heardThroughWalls, heardThroughDoors);

    sound.Mute = depth >= 1f;                         // >= 1 tile of wall/door -> silent

    int px = (int)Math.Floor(ent.PlayerPosition.X), py = (int)Math.Floor(ent.PlayerPosition.Y);
    int sx = (int)Math.Floor(basePosition.X),       sy = (int)Math.Floor(basePosition.Y);
    if (sx >= 0 && sy >= 0 && sx < ent.Map.TileWidth && sy < ent.Map.TileHeight) {
        bool playerIndoors = ent.Map.GetTile(px, py).IsIndoors;
        bool soundIndoors  = ent.Map.GetTile(sx, sy).IsIndoors;
        sound.SetObstructionAndIndoorStatus(depth, playerIndoors, soundIndoors);  // -> FMOD params
        sound.Update(basePosition);                   // -> FMOD 3D position
    }
}
```

- **Mute threshold**: `depth >= 1.0`. Less than a full tile of obstruction is *not* muted but the
  `depth` value (0..1) is still handed to FMOD as the `"Obstruction"` parameter to drive a low-pass
  muffle (Doc 08 §3). One tile or more → fully silent.
- `heardThroughWalls` / `heardThroughDoors` let specific emitters ignore walls/doors (e.g. a quest
  beacon you should always hear).
- `IMovableSound.SetObstructionAndIndoorStatus(obstruction, playerIndoors, soundIndoors)` maps to FMOD
  event parameters `"Obstruction"`, `"PlayerIndoors"`, `"SoundIndoors"` — the FMOD project's DSP graph
  turns those into the actual filtering/reverb.

---

## 5. Player movement collision is a THIRD, separate system (point test, no radius)

Neither ray system above resolves player movement — that's `FPMapLogic.HandleUnitPointToWallCollision`
(`FPMapLogic.cs:39-132`), called from `MoveUnitBasedOnTravelVector` (`:276`). Key facts, since it's easy
to assume movement reuses the DDA or the POI march:

- **It's a POINT test — the player has no collision radius against walls.** It compares the current
  tile vs the target tile at `Position + changeVector`. There are three cases (`FPMapLogic.cs:46-131`):
  (1) same-tile → **early return**, no collision (`:46-89` head); (2) the target is a **4-orthogonal
  neighbor** and `!CanPass` (`Wall OR Impassable`) → zero the offending axis (axis-aligned wall slide)
  rather than blocking the whole move; (3) the target is a **diagonal neighbor** → the fallback the
  earlier pass missed (`:90-131`): raycast to the first crossed tile (`CH.GetLineToTileCollision[0]`),
  then resolve **per-axis** — test the **X-only** sub-move (`Position + (Δx,0)`) and the **Y-only**
  sub-move separately, zeroing **either or both** axes depending on which sub-tiles are `!CanPass`, and
  emit a bump from whichever blocked. **Only `Terrain.CanPass` is ever consulted** (objects/NPCs/doors
  never block movement). **Tile identity is `FPTile` reference equality** (`tile3 == fPTile`), not
  coordinate comparison. A `BumpSound` plays, throttled to 0.6 s (`PlayBumpSound`, `:142-150`).
- **Objects/NPCs/doors never block player movement** — the movement test only looks at tiles. Dynamic
  objects are tested as circles (`CH.GetObjectCollision` line-to-circle distance test, `CH.cs:67-85`;
  POI march's `dist < Radius`, `POIHelpers.cs:51`) but only for radar/scan/interact/script rays, never
  for movement resolution. Doors are terrain tiles (the `Door` flag), not objects.
- **`Door` tiles are walkable for movement** (`CanPass = !Wall && !Impassable`, `FPTerrain.cs:60-70` —
  `Door` isn't part of the flag), even though the same `Door` flag stops the DDA-based occlusion/LOS
  rays and the POI point-march beam. Same flag, three different consumers, three different rules — see
  the summary table below.

### What "impassable"/"door" mean, precisely, per consumer

| Consumer | Blocked by | Doors? |
|---|---|---|
| Player movement (`HandleUnitPointToWallCollision`) | `!CanPass` = `Wall OR Impassable` | Pass through |
| DDA radar/occlusion/LOS (`GetWallOrImpassableCollision`/`…OrDoorCollision`) | `Wall`, `Impassable`, and/or `Door` per API variant | Blocks (door-aware variant) |
| POI point-march beam (`GetLineCollisionWithPointsOfInterest`) | `Wall OR Door` | Blocks (Impassable does NOT block — it's reported as its own named section instead, Doc 05 §1) |

> **Latent bug worth recording (unhit in practice, but port it correctly).** The POI point-march's
> in-bounds guard uses **`>` instead of `>=`**: `num3 > map.PaddedWidth || num4 > map.PaddedHeight`
> (`POIHelpers.cs:33`), so index `== PaddedWidth`/`PaddedHeight` is treated as in-range and fed to
> `map.Tiles.Get(...)`. The march itself only terminates on `Wall || Door` (`POIHelpers.cs:42`), **not**
> on `Impassable`. So **if** the `"map boundary"` ring terrain (Doc 01 §5) were flagged
> `Impassable`-but-not-`Wall`, a forward ray could walk one tile past the edge and index out of range.
> It's harmless in the shipped data only because that boundary terrain **is** `Wall`-flagged (which stops
> the march at `:42` before the bad index). When porting: use `>=` and/or terminate on `Impassable` too —
> don't rely on the boundary terrain's flags to mask the off-by-one.

---

## 6. Cookbook — what to call for each need

| You want… | Call | Returns |
|-----------|------|---------|
| First wall ahead along a beam (Style-1 reactive radar) | `GetWallOrImpassableOrDoorCollision(map, pos, dir, far)` | `AudibleTileCollision?` (point + tile) |
| All tiles a ray passes (radar / occlusion / beacon LOS / object rays) | `GetLineToTileCollision(map, pos, dir, dist)` (private; mirrored by `CH.GetLineToTileCollision`) | ordered `List<…>` |
| Forward/side POI scan — the main spoken readout | `POIHelpers.GetLineCollisionWithPointsOfInterest` (Doc 05 §1) — a **separate** fixed 0.1-tile point-march, NOT this DDA | `POIList` |
| How muffled is a source | `GetWallAndDoorDepth(map, listener, src)` | `float` thickness in tiles |
| Distance to wall in a direction (overview) | walk tiles via `Get(x,y).Terrain.CanPass` (Doc 04 §map overview) | `int` tiles |

For a **non-tile game**, replace `GetLineToTileCollision` with your physics raycast and synthesize the
two derived helpers: "first blocker" = first raycast hit on the wall layer; "depth" = sum of
penetration spans (sphere/box-cast and accumulate hit thicknesses, or simply count discrete blockers ×
an assumed thickness). The downstream radar/occlusion code does not care how you produced the numbers.

> **The world is purely 2-D for movement, collision and raycasting — "height" is RENDER-ONLY.**
> `FPTerrain.Height` and the derived `FPTile.Top`/`FPTile.Bottom` (`FPTile.cs:68-88`) exist **only** to
> drive **wall render height** in the 3D view: `Wall`/`Door` → `Top = 1`, `Impassable` → `0.5`, floor →
> `0`, and `Bottom` is a constant `-1`. **No** movement, collision, occlusion or raycasting code reads
> them — every path in this doc operates on pure 2-D `V2`. There is **no** stairs/climb/jump/swim logic
> anywhere in the source; vertical/area changes are **discrete map switches via Portals**, not continuous
> 3D traversal. So when porting to a real 3D engine, do **not** try to reverse-engineer a height field
> from AHC — there isn't one to recover; treat the game as a flat tile plane and add your own vertical
> model if the target needs one.

### Trick: getting a wall DISTANCE when the engine's hit result is unreadable (field-validated, RE7)

Some engines let you cast a ray but the **hit struct is not readable** from a mod (e.g. RE Engine's
`via.physics.ContactPoint` is a value-type that REFramework can't unbox — Position/Normal/Distance all
come back null). If all you can reliably get is a **boolean "does a ray of length L hit anything?"**
(e.g. `castRay(...).NumContactPoints > 0`), you can still recover the distance by **binary-searching the
ray length**:

```
if not RayHits(origin, dir, maxRange): return maxRange        # nothing within range = "open"
lo, hi = 0, maxRange
repeat ~6 times:                                              # 6 iters over 8 m ≈ 0.13 m precision
    mid = (lo+hi)/2
    if RayHits(origin, dir, mid): hi = mid else lo = mid
return hi                                                     # distance to first blocker
```

~7 boolean casts per beam, entirely from the reliable hit/no-hit primitive — no dependency on an
unreadable hit struct. Reconstruct the hit point (if needed) as `origin + dir*distance`. **Caveat:** the
distance is quantized (±half the final step) and noisy — fine for the 3-bucket pitch and for the
**plane-tracking** open/closed method (Doc 02 §5.4), but too noisy for the wall-edge line-slope
method. This is exactly why the line-slope open/closed must be replaced in such engines.

Also worth knowing before you binary-search: many engines DO expose a readable hit (Unity
`RaycastHit.distance/normal/point`, or a non-`ref` result object). Check first — a direct distance +
surface normal is strictly better (the normal even lets you classify wall-vs-floor-vs-ceiling for free).
Only fall back to the binary-search trick when the hit result is genuinely unreadable from your mod.
