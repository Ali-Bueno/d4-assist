# Swamp — Navigation System Specification

Reverse-engineered from the native VB6 decompilation of `Swamp.exe` (Ghidra) plus the on-disk map
file format. This documents the **collision**, **zone announcement**, **radar**, **facing/heading**,
and **coordinate** systems so they can be reimplemented in another engine.

Sources cross-validated: `navigation.c`, `collision_disasm.txt`, the map files in `..\maps\`, and
`strings.txt`. Confidence is noted per section. Addresses (`FUN_xxxx`, `DAT_xxxx`) refer to the
decompiled artifacts in this folder.

---

## 0. Core data model (the foundation of everything)

The world is a single fixed **601 × 601 tile grid** (stride `0x259 = 601`). Tiles are stored as a
flat array of tile-type IDs:

```
tileId = map[ X + Y * 601 ]          // map base = form field [0x1ab] / [0x1bd]
flag   = tileFlags[ tileId ]         // flag table base = form field [0x20a]
```

Each **tile type** comes from the map file's palette and carries:

| field   | meaning                                                            |
|---------|-------------------------------------------------------------------|
| color   | RGB integer — identifies the tile when the map is painted as image |
| **flag**| **1 = ground (walkable), 2 = wall, 4 = impassable obstacle**       |
| name    | display name (e.g. `Grass`, `Wall`, `Trees`)                      |
| sound   | sound file base name (e.g. `Wall.wav`, `door.wav`, `Fence.wav`)    |

(There is also a "top surface" tile category used by the inspect command; flags 1/2/4 are the ones
that drive movement and radar.)

Player state (globals; radar copies and the keyboard handler share these):
- `playerX` = `DAT_009d8a58` (also form `[0x356]`), `playerY` = `DAT_009d8a68` (`[0x35d]`) — **tile units**.
- `heading` = `DAT_009d8aa8` — **degrees**, always snapped to a multiple of 45.
- **Orientation convention: `0° = East`, angle increases CLOCKWISE** (screen space, Y-down):
  `East → SE → South → SW → West → NW → North → NE`.

Each cell also has **two zone layers**: `zoneID1`, `zoneID2`, resolved to zone names by id.

Spoken distances are in **"feet" = tileDistance × 24** (`0x18`).

> Confidence: **High** (confirmed independently by the radar code, the collision code, and the map
> file palette).

---

## 1. Collision (walking into walls / obstacles) — the main question

Handled in the movement/keyboard routine `FUN_005b9730`.

### Flow per movement step
```
when the player presses a movement key:
    (destX, destY) = current tile + one step in the pressed/facing direction
    flag = tileFlags[ map[destX + destY*601] ]

    if flag == 1:                       # ground / walkable
        player position = (destX, destY)
        play footstep sound for that terrain   # /sounds/Steps/... , per-terrain
    else:                               # flag == 2 (wall) or 4 (impassable)
        # BLOCKED: position is NOT updated
        play collision sound (see below)
        # no speech
```

### How the collision sound is triggered
The blocked branch builds the path and plays it positioned:
```
path = "/sounds/Obstacles/" + tileType.sound          # e.g. door.wav, isle.wav, Wall.wav, Fence.wav
                                                       # (confirmed at collision_disasm 0x5d464b, 0x5f62f7)
# some obstacle types pick a RANDOM variant (rtcRandomNext) before choosing the .wav (e.g. doors)
audio.SetFile(path)
audio.SetPan(pan)        # positioned toward the obstacle
audio.SetVolume(vol)
audio.Play()
```
- The **sound name is defined by the tile type itself** (the `sound` field in the map palette), so a
  wall tile plays its wall sound, a door tile plays `door.wav`, a fence plays `Fence.wav`, etc.
- It is played through the same positional audio object used by the radar, so the bump is heard in the
  direction of the obstacle.

### What the screen reader says on collision
**Nothing.** A normal bump is communicated **by sound only** (the positioned obstacle sound). This is
deliberate for an audio game — it keeps movement fast and non-verbose.

The spoken line *"Tile ID N is a wall tile named, X. It uses the sound file, Y."* is a **separate
inspect/debug command** invoked by the typed command **`SAY TILE ID=`** (in `FUN_007e63b0`, at
`collision_disasm` 0x801c73–0x802376). It also has variants for ground / top-surface / impassable
tiles. It is **not** part of walking collision.

> Confidence: **High** for the sound path construction, the flag 1/2/4 gate, and "no speech on bump".
> The exact compare/branch instruction that decides blocked-vs-move sits earlier in the 296 KB blob
> `FUN_005b9730` and was confirmed by the model (same `tileFlags[map[...]]` lookup as the radar) plus
> the obstacle-sound disassembly, not line-by-line decompiled.

---

## 2. Zone announcement

Zones are **named regions**; every cell carries two zone ids (`zoneID1`, `zoneID2`). Examples from a
real map: `Lobby`, `North hallway`, `South hallway`, `Cubicle office`, `Door, to exit Press Enter.`

```
on player moved to a new tile:
    z1 = cell.zoneID1 ; z2 = cell.zoneID2
    if zones_spoken and (z1 != last_zone1 or z2 != last_zone2):
        if z1 != last_zone1: speak( zoneName[z1] )
        if z2 != last_zone2: speak( zoneName[z2] )
        last_zone1, last_zone2 = z1, z2

# toggle:
"/zones on"  -> zones_spoken = true ; speak "Zones spoken."
"/zones off" -> zones_spoken = false; speak "Zones hidden."
```
So: the screen reader announces a zone **only when you cross into a different zone** (id change), and
only while spoken-zones is on. Two layers let a tile belong to both a broad area and a sub-area.

> Confidence: **High** on the data model (two zone layers, names by id, spoken/hidden toggle strings
> `Zones spoken.`/`Zones hidden.`). The exact change-detection call site lives in the skipped 284 KB
> handler `FUN_0050a2d0`; the trigger-on-id-change mechanism is inferred from the model and the toggle.

---

## 3. Radar (`FUN_006e7a70`, "radarstuff")

A **polar ray-march**. Mode `DAT_009d8c28` (0–10) selects which direction(s) to sound and the range:
`0`=silence, `1-4`=Forward/Right/Backward/Left short, `5`=Area short, `6-9`=same dirs long, `10`=Area long.

```
RADAR():
    if mode == 0: return
    for dir in selected_directions:                 # 16 directions, 22.5° apart
        angleRad = (dir*22.5 + heading) * PI/180
        sx = trigX(angleRad); sy = trigY(angleRad)
        hitFlag = -1; hitDist = -1
        for r = 0.5 to rMax step 0.5:               # rMax = 5 (short) or 40 (long) tiles
            X = fix(playerX + r*sx); Y = fix(playerY + r*sy)
            if X<0 or X>600 or Y<0 or Y>600: break
            flag = tileFlags[ map[X + Y*601] ]
            if flag == 2: hitFlag=2; hitDist=r; break    # wall
            if flag == 4: hitFlag=4; hitDist=r; break    # impassable
            # else open -> keep marching
        radarFlag[dir] = hitFlag; radarDist[dir] = hitDist

    # playback for the direction(s) the mode selects:
    file = (flag==2) ? "/sounds/Radar/wall.wav"
         : (flag==4) ? "/sounds/Radar/impassable.wav"
         :             "/sounds/Radar/radar.wav"     # open
    pan  = clamp(20 * sin(relBearing) * f(dist), floor=-5000)   # -5000 = out of range -> silent
    vol  = dist*10*... - 500                                    # nearer = louder
    audio.SetFile(file); audio.SetPan(pan); audio.SetVolume(vol); audio.Play()
```
Key constants: 22.5° (= 360/16), radius 0.5 step, max 5/40, π/180 deg→rad, −5000 pan floor / silence
sentinel.

> Confidence: **High** (full function decompiled and read).

---

## 4. Facing / heading announcement

```
NAME8 = {0:"East",45:"South East",90:"South",135:"South West",
         180:"West",225:"North West",270:"North",315:"North East"}

snap_to_8(angle):
    a = angle mod 360                       # if a<0 a+=360
    # nearest 45° using midpoint boundaries 22.5,67.5,...,337.5
    return nearest multiple of 45

announce_heading():
    heading = snap_to_8(heading)
    speak( NAME8[heading] )
```
The scan/look line also prepends a movement-state word based on a signed "approach" value toward the
currently referenced entity: `|approach|<=0.1 -> "Facing"`, `>0 -> "Heading"`, `<0 -> "Backing up"`,
formatted as `"<word> on <name>."` (or `"<word>."` when no name).

> Confidence: **High** for the degree→name table and the 45° snap. The "Facing/Heading/Backing up"
> field semantics are **inferred** (operands register-passed, not captured by Ghidra).

---

## 5. Direction-to-target ("Go <dir>") and coordinates

Direction to a target point (helper `FUN_00621270`, a quadrant-resolved atan2 in degrees):
```
bearing(dx, dy):
    if dx==0 and dy>0:  r = 0
    elif dx==0 and dy<0: r = 180
    elif dy>0:  r = atan(dx/dy) * 180/PI
    else:       r = (atan(dx/dy) + PI) * 180/PI
    return r mod 360

go_to(target):
    d = snap_to_8( bearing(target.x - playerX, target.y - playerY) )
    speak( "Go " + NAME8[d] )          # separate "Go East/Go South East/..." string table
```
Coordinates (returncoords): `speak("You are at coordinates " + playerX + ", " + playerY)` — raw tile
units, no transform.

> Confidence: bearing math **High**; the "Go" and "You are at coordinates" call sites live in the
> skipped 284 KB handler `FUN_0050a2d0` (strings confirmed, driving code not in the dump).

---

## 6. Summary for reimplementation

1. Represent the world as a tile grid with a per-type **flag (1 walk / 2 wall / 4 impassable)** and a
   per-type **sound name**.
2. **Movement**: step; read destination flag; if 1 → move + terrain footstep; if 2/4 → block + play
   `Obstacles/<tile.sound>` positioned toward the obstacle; **no speech**.
3. **Zones**: two id layers per tile; on id change (when enabled) speak the zone name.
4. **Radar**: polar ray-march in 16 directions, first flag-2/4 hit → wall/impassable/open sound,
   panned by bearing, attenuated by distance.
5. **Orientation**: heading in degrees, 0=East clockwise, snapped to 8 compass names; distances in
   feet = tiles × 24.

### Not fully recovered (inside Ghidra-skipped oversized functions)
- Exact block-vs-move branch inside `FUN_005b9730` (296 KB).
- `/zones on|off` + zone-change call site, "You are at coordinates", "Go <dir>" call sites
  (`FUN_0050a2d0`, 284 KB). Strings confirmed; decompile these in the Ghidra GUI per-function if the
  exact code is needed.
