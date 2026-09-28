# Diablo IV Dungeon Assist --- Research Brief for Claude

**Date:** 2026-09-26\
**Goal:** Research and prototype an accessibility-focused dungeon
navigation assistant for blind Diablo IV players.

## 1. Product goal

Build an external Dungeon Assist that helps a blind player **manually
explore** Diablo IV dungeons using spatial audio and concise TTS.

Desired information includes nearby traversable directions,
intersections, dead ends, doors/transitions, explored versus unexplored
branches when legitimately known, known objective direction, and
direction back to known landmarks/entrance.

Do not automate movement, solve the dungeon, reveal enemies/loot through
walls, or add automatic path following. Treat this as an accessible
equivalent of visual dungeon/minimap information.

## 2. Research conclusion

Investigate three independent information channels:

1.  **Offline game data:** CASC/TACT/TVFS + SNO/CoreTOC for static
    World/Scene/dungeon data.
2.  **Accessibility output:** Diablo IV's third-party screen-reader/TTS
    channel for text the game intentionally exposes.
3.  **Minimal runtime anchor:** player position/orientation plus enough
    identity/state to associate the player with the currently
    instantiated procedural dungeon.

The preferred architecture is:

`offline dungeon model + minimal runtime anchor + accessibility text -> accessible navigation`

Do not assume memory reading is required until the first two channels
have been exhausted. Do not assume CASC alone contains the current
procedural instance.

## 3. Strongest finding: explicit DRLG structures

Community-recovered Diablo IV schema/name data contains many **DRLG
(random level generation)** concepts:

-   `DRLGDungeonQuestEntry`
-   `DRLGDungeonState`
-   `DRLGDungeonStateData`
-   `DRLGGroupData`
-   `DRLGNodeBase`
-   `DRLGNodeConnectionData`
-   `DRLGNodeDungeonStateData`
-   `DRLGNodeHeightTransitionData`
-   `DRLGNodeLink`
-   `DRLGNodeRandomRoomsData`
-   `DRLGNodeStartData`
-   `DRLGNodeTileSetTransitionData`
-   `DRLGPartialTileData`
-   `DRLGPortalInfo`
-   `DRLGSceneCreationInfo`
-   `DRLGSpawningData`
-   `DRLGTileDebugInfo`
-   `DRLGWorldDebugInfo`
-   `DRLGWorldDebugInfoMessage`

Other potentially relevant recovered names include `DoorGizmoData`,
`LevelAreaDefinition`, `CurrentWorldSubcondition`,
`CurrentLevelAreaSubcondition`, `ACDWorldPositionMessage`,
`DT_VECTOR2D`, and `DT_VECTOR3D`.

This is a major lead: investigate DRLG schemas and their World/Scene
references before blindly searching generic map assets.

**Caveat:** existence of these names proves that these concepts exist in
recovered type/schema data. It does not prove that every structure is
available offline, completely decoded, or sufficient to reconstruct the
current dungeon instance.

Sources: -
https://github.com/blizzhackers/d4data/blob/master/names.txt -
https://github.com/DiabloTools/d4data

## 4. Offline data stack

### WiseOwl.Casc

This is a modern, documented route into a local Diablo IV installation.

Verified useful properties:

-   reads local CASC/TACT/TVFS storage;
-   has a Diablo-IV-specific layer;
-   Diablo IV addresses SNO content numerically;
-   `CoreTOC` provides SNO name \<-\> ID \<-\> group mappings;
-   documented SNO access includes paths such as `Base\Meta\<id>`.

Prefer a programmatic CASC/SNO reader for the prototype. CASCExplorer
remains useful for interactive inspection but should not become the
architecture.

Sources: - https://github.com/WiseOwlSoftware/WiseOwl.Casc -
https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main/docs/casc-diablo4-format.md -
https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main/docs/casc-format.md

### d4data

Useful for recovered definitions, field/type names, SNO references, JSON
data, and discovering structures such as DRLG.

The old `blizzhackers/d4data` repository is archived; prefer the
DiabloTools continuation for current work while retaining the old repo
as reference.

Sources: - https://github.com/DiabloTools/d4data -
https://github.com/blizzhackers/d4data

### d4parse

Highly relevant reverse-engineering utility. It already provides:

-   `dumpsnometa` --- dump SNO metadata;
-   `dumptoc` --- dump CoreTOC;
-   `structgen` --- generate Go parsing structures from d4data
    definitions.

Its README demonstrates dumping a `World_SecretCellar` record, which
makes a small cellar/dungeon an excellent first target.

Source: - https://github.com/Dakota628/d4parse

### Diablo4Tools / D4Analyzer

Shows that community tooling can inspect/export several D4 asset
categories. Documented capabilities have included file lists,
translations, textures, models, Worlds/map background, and newer basic
Actor/Power/Quest support. It remains actively maintained for 2026
builds, but World support has changed between patches, so verify current
behavior rather than assuming an old feature still works.

Source: - https://github.com/DiabloTools/Diablo4Tools-Releases

## 5. Proven accessibility/TTS channel

### D4LF

Current D4LF documentation says it gets item information through
screen/vision and Diablo IV accessibility TTS information.

D4 supports a third-party screen-reader path. D4LF installs a signed
`saapi64.dll`; instead of speaking normally, the DLL forwards
game-provided text to the external application.

This is valuable for Dungeon Assist because it is a proven way to
capture text that Diablo IV exposes to accessibility software.

Potential uses:

-   current objective;
-   dungeon/location names;
-   UI state;
-   objective/map labels if D4 emits them;
-   transition announcements.

**Do not claim D4LF supplies player coordinates. Current evidence does
not show that.**

Source: - https://github.com/d4lfteam/d4lf

### D4Scanner

D4Scanner independently uses the game's screen-reader text and
explicitly describes its approach as not reading game memory or
injecting gameplay code. Study its capture documentation as a
potentially simpler reference implementation of the accessibility
channel.

Source: - https://github.com/defessler/D4Scanner

## 6. Community overlays: useful but limited evidence

### d4-map-overlay

Small Python Altar of Lilith overlay. Useful as an example of
transparent external overlay UI, map imagery, and static map data.

Do **not** treat it as proof of live coordinate extraction unless its
source actually demonstrates that.

Source: - https://github.com/mxtsdev/d4-map-overlay

### D4LF overlays

D4LF has real external overlays and is useful for Windows overlay/hotkey
patterns. Its documented dynamic information channels remain screen/TTS
for the relevant features.

### DiabloDungeonTimer

`Shaydera/DiabloDungeonTimer` is described as automatically
tracking/timing Diablo IV dungeons and is written in C#. Inspect it
specifically for:

-   process/window detection;
-   how dungeon entry/exit is detected;
-   OCR/screen recognition;
-   logs/events;
-   any dungeon identity mechanism.

Do not assume it reads coordinates or memory based only on the
repository description.

Source: - https://github.com/Shaydera/DiabloDungeonTimer

## 7. Proposed architecture

``` text
Local Diablo IV install
        |
CASC / TVFS / CoreTOC
        |
Offline Data Provider
SNO / World / Scene / DRLG
        |
        v
+----------------------+       +----------------------+
| Dungeon State Model  | <---- | Accessibility / TTS  |
| graph + exploration  |       | objectives/UI text   |
+----------+-----------+       +----------------------+
           ^
           |
+----------+-----------+
| Runtime Anchor       |
| player pose          |
| world/scene/instance |
+----------+-----------+
           |
           v
+----------------------+
| Accessibility Layer  |
| spatial audio + TTS  |
+----------------------+
```

Keep providers replaceable. Do not bake offsets, OCR, CASC, TTS, or any
one reverse-engineering method into navigation logic.

Suggested interfaces:

``` csharp
public interface IPlayerPoseProvider
{
    bool TryGetPose(out PlayerPose pose);
}

public interface IDungeonContextProvider
{
    bool TryGetContext(out DungeonContext context);
}

public interface IGameAccessibilityTextProvider
{
    event Action<GameTextEvent> TextReceived;
}

public interface IOfflineDungeonDataProvider
{
    DungeonDefinition? GetDungeon(int snoId);
    SceneDefinition? GetScene(int snoId);
}
```

## 8. Claude research plan

### Phase A --- establish the offline pipeline

1.  Inspect/clone:
    -   `WiseOwlSoftware/WiseOwl.Casc`
    -   `DiabloTools/d4data`
    -   `Dakota628/d4parse`
2.  Open the local Diablo IV installation read-only.
3.  Dump CoreTOC and enumerate SNO groups/names.
4.  Search for `DRLG`, `Dungeon`, `World`, `Scene`, `LevelArea`, `Tile`,
    `Nav`, `Door`, `Portal`, and `Marker`.
5.  Pick **one small cellar or dungeon** as the target.
6.  Dump its related SNO metadata.
7.  Recursively follow relevant `DT_SNO` / `DT_SNONAME` references.
8.  Determine which records represent room/tile templates, connections,
    scene transforms, doors/portals, collision/walkability, and
    procedural constraints.

### Phase B --- attack DRLG directly

For every relevant DRLG type:

1.  Locate its recovered schema/definition.
2.  Classify it as offline SNO data, runtime/network/debug schema, or
    mixed.
3.  Record fields and types.
4.  Find World/Scene references.
5.  Determine coordinate spaces and transforms.
6.  Build a small dumper that emits a graph.

Desired proof-of-concept output:

``` json
{
  "world": "example",
  "nodes": [
    {
      "id": 1,
      "sceneSno": 123,
      "connections": [2, 4],
      "transform": {"x": 0, "y": 0, "z": 0}
    }
  ]
}
```

The exact schema is secondary. First prove whether useful dungeon
connectivity can be reconstructed.

### Phase C --- accessibility logger

Study D4Scanner, then D4LF.

Determine:

-   how `saapi64.dll` receives D4 accessibility text;
-   how it forwards data to the companion application;
-   message framing/protocol;
-   what strings D4 emits while entering and exploring a dungeon;
-   whether objectives, location names, map markers, doors, or
    transitions appear.

Build a timestamped diagnostic logger. Avoid UI automation in the first
prototype.

### Phase D --- determine the missing runtime state

After A-C, write down exactly what remains unavailable.

Likely minimum:

``` text
player position x/y/z
player heading
current world/scene/dungeon identity
current instantiated procedural scene/tile placement,
or enough information to derive it
```

Investigate non-memory sources first:

-   accessibility/TTS;
-   game-generated local logs/debug files if useful;
-   minimap/UI observation;
-   window/UI state.

If these are insufficient, document precisely why before adding a
read-only runtime provider.

Do not implement anti-cheat bypasses, process-handle hiding, signature
evasion, stealth, or similar mechanisms.

## 9. Central technical question: template vs. current instance

This is the project's main unknown.

CASC may tell us which assets, room templates, scenes, connection rules,
and generation constraints exist. The running game can instantiate those
pieces differently for each procedural dungeon.

Claude must answer:

1.  Can a generated layout be reproduced from offline data plus a seed?
2.  Is such a seed available through a non-invasive source?
3.  Does the client expose/hold a list of instantiated DRLG
    nodes/scenes?
4.  Can current Scene ID + player coordinates identify the active tile?
5.  Are minimap tiles derived from these same Scene/DRLG structures?
6.  Are all dungeon pieces instantiated immediately or
    streamed/generated progressively?
7.  What information differentiates two different runs of the same
    dungeon?

Do not declare the dungeon reconstruction solved until this distinction
is understood.

## 10. Navigation model

Once geometry and pose exist, convert the current dungeon into an
internal graph.

``` text
DungeonGraph
  Nodes:
    scene/tile/room identity
    bounds
    transform
    exits
    discovery state
    visit state

  Edges:
    source
    destination
    doorway/socket position
    traversal type
```

Keep discovery separate from raw geometry:

``` text
Unknown
Discovered
Visited
Completed
Blocked
```

Even if the backend can see the complete generated dungeon, the
accessibility layer should be able to withhold undiscovered information
to preserve normal exploration.

## 11. Accessibility behavior

Continuous cues:

-   one-shot cue when a meaningful new opening appears;
-   stereo pan based on relative angle;
-   distinct cue/timbre for open, blocked, known, and unexplored;
-   strong anti-spam: repeat only after meaningful state changes.

Explicit queries:

-   "Two unexplored branches: left and ahead-right."
-   "Known objective: northwest."
-   "Entrance: behind-left."
-   "Intersection: three exits."
-   "Current branch already explored."

Avoid speaking raw coordinates during normal play.

## 12. Prototype milestones

**P0 --- Offline proof:** enumerate World/Scene/DRLG candidates and dump
one small dungeon/cellar.

**P1 --- DRLG proof:** produce a partial dungeon graph and document the
provenance of each node/edge.

**P2 --- Accessibility proof:** capture and log Diablo IV third-party
screen-reader output during dungeon play.

**P3 --- Runtime-anchor proof:** obtain/infer pose and current instance
identity using the least invasive reliable source discovered.

**P4 --- Static radar:** for a known test scene, report openings
relative to player heading.

**P5 --- Procedural dungeon:** resolve the current generated layout and
track explored/unexplored branches.

**P6 --- UX:** spatial cues, concise TTS, anti-spam, and
controller-friendly controls.

## 13. Do not assume

-   CASC contains the current instantiated procedural layout.
-   A World/map background equals collision or navmesh.
-   D4LF reads memory.
-   D4LF or D4Scanner exposes player coordinates.
-   DiabloDungeonTimer uses memory; inspect it.
-   A recovered type name means its binary layout is fully understood.
-   2023 offsets or abandoned overlay techniques remain valid in 2026.
-   read-only process access is necessarily invisible to anti-cheat.
-   computer vision is required before the offline data path is tested.

## 14. Confidence summary

### High confidence

-   Modern community tooling can read the local D4 CASC/TVFS
    installation.
-   CoreTOC maps SNO names, IDs and groups.
-   Recovered schemas/names contain extensive DRLG concepts.
-   `d4parse` already dumps SNO metadata/CoreTOC and generates parsing
    structures.
-   Diablo IV has a third-party accessibility/screen-reader channel used
    by active community tools.
-   External overlays are straightforward and already demonstrated by
    several projects.

### Medium confidence

-   World/Scene/DRLG offline data will expose enough topology to
    materially help dungeon reconstruction.
-   TTS can help identify dungeon/objective/context transitions.

### Unknown --- must be proven

-   Whether offline data can reconstruct a complete generated dungeon
    instance.
-   How to obtain player world position and heading without memory
    access.
-   How to identify the exact instantiated DRLG graph for the current
    run.
-   Whether useful collision/navmesh data is recoverable from current
    assets.
-   Whether the minimap's discovered-state information is exposed
    outside runtime state.

## 15. First concrete task for Claude

Do **not** start by building the full assist.

Start with this research spike:

> Using WiseOwl.Casc, DiabloTools/d4data and d4parse, inspect a current
> local Diablo IV installation and select one small cellar/dungeon.
> Resolve its World/Scene-related SNOs and every relevant DRLG reference
> you can find. Produce a concise `research/dungeon-data.md` documenting
> the reference chain, recovered fields, coordinate systems, room/tile
> connections, and what remains unknown. Then write the smallest
> possible diagnostic dumper that outputs the discovered structure as
> JSON. Do not read or write Diablo IV process memory during this spike.

After that, separately build a diagnostic TTS logger based on the
D4Scanner/D4LF third-party screen-reader mechanism and record what
Diablo IV emits while entering and traversing that same test dungeon.

Only then decide what runtime provider is actually necessary.

## References

-   WiseOwl.Casc: https://github.com/WiseOwlSoftware/WiseOwl.Casc
-   WiseOwl Diablo IV format:
    https://github.com/WiseOwlSoftware/WiseOwl.Casc/blob/main/docs/casc-diablo4-format.md
-   DiabloTools/d4data: https://github.com/DiabloTools/d4data
-   Historical d4data: https://github.com/blizzhackers/d4data
-   d4parse: https://github.com/Dakota628/d4parse
-   Diablo4Tools/D4Analyzer releases:
    https://github.com/DiabloTools/Diablo4Tools-Releases
-   D4LF: https://github.com/d4lfteam/d4lf
-   D4Scanner: https://github.com/defessler/D4Scanner
-   d4-map-overlay: https://github.com/mxtsdev/d4-map-overlay
-   DiabloDungeonTimer: https://github.com/Shaydera/DiabloDungeonTimer
