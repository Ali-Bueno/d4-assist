# Campaign routes — guiding to story points outside dungeons (offline)

Research spike, 2026-09-29, local install (build as of that date), read **read-only** through CASC with
`data/definitions.json`, same method as [dungeon-data](dungeon-data.md) §1. Confidence: **H** verified on game data
from several angles, **M** a few samples, **L** inference. Scratch programs:
`scratchpad/sandstorm/*.cs` (session-local, not kept).

## 1. Method: a quest's subzone MarkerSet places its story points in world space (H)

- A campaign quest's props live in a MarkerSet named `<region> (<region>_Subzone_<quest area>)` (group 24). It
  holds the quest's actors and one marker naming the Quest SNO itself (group 31), which is where the quest starts.
- These sets have no nested prefabs here, and their coordinates are world space in `Sanctuary_Eastern_Continent`
  (World 69068, 1220 chunks, bounds X −2400..3456, Y −2016..1440). Every position below lands inside the
  expected 96-unit chunk.
- The quest's own callbacks (`DungeonObjectiveParser.QuestTokens`, [dungeon-objectives](dungeon-objectives.md) §2)
  name the actors that matter, the same way DPO quests do in dungeons.
- The overworld scenes ship no uncompressed detail grid, only the compressed `m_cellBytes` form, decoded since
  2026-09-30 ([dungeon-data](dungeon-data.md) §4.1). The app still loads no walk grid for the open world (user: the
  game's navigation assist covers it), so there is no path to follow; guidance is a straight line to the target.
- Chunk `LevelAreaSno` is −1 on these chunks; the LevelArea isn't in the chunk data.

## 2. The desert sandstorm crossing with Meshif (user: act 3)

Quest `VGN_Kehj_EliasPalace_Sandstorm` (577560). The player crosses the desert toward Elias's palace with
Meshif (the user calls him Mesif) while sandstorms hit, and must shelter in safe zones.
MarkerSet 577564 `Kehj_Ridge (Kehj_Ridge_Subzone_EliasPalace_Sandstorm)` has about 70 markers. The intro set 1043935
holds only a row of 6 invisible collision cubes at X≈−491, Y 22..59.

| What | SNO | World position (X, Y, Z) | Conf. |
|---|---|---|---|
| Quest marker (start) | quest 577560 | (−491.6, 45.1, 39.0) | H |
| Safe zone | `BCD_Trigger_EliasPalace_SafeZone` 972175 | (−573.2, 40.2, 47.0) | H |
| Safe zone | same | (−476.6, −31.7, 33.1) | H |
| Safe zone | same | (−516.1, −142.7, 26.9) | H |
| Guide NPC, one within 1.5 units of each safe zone | `NPC_QST_Kehj_EliasPalace_Guide` 576066 | — | H |
| Storm triggers | `Trigger_EliasPalace_SandStorm_Highwind` 978721 | (−501.6, −95.2), (−502.3, −94.3), (−521.8, −10.8), (−578.4, −1.3) | H |
| Lorath at the start | `NPC_QST_Lorath_NonCombat` 811653 | (−478.5, 45.9) | H |
| Sand walls | `fxKit_harmless_persistentBarrier_sand` (group 14) | (−652.6, 32.1) west wall; (−484.9, 45.8) at the start | H |

- The quest's callbacks name exactly two actors: the safe zone 972175 and the highwind trigger 978721 (H).
- **Meshif** in this quest is the guide actor 576066: its definition references `npcM_Meshif` 1062927, and its
  appearance is `npcM_base_invisible` (M-H). The actors that are literally named Meshif (1798412, 2053597) are
  Nahantu content and unrelated.
- **Visiting order** (H, corrected 2026-10-19): (−477, −32), then (−516, −143), then (−573, 40), then Meshif on the
  palace steps (−672.6, 7.0). The campaign quest `QST_Kehj_EliasPalace_02` (574671, "Flesh from Bone") names one safe
  zone per phase: 452 → (−476.6, −31.7), 571 → (−516.1, −142.7), 698 → (−573.2, 40.2); then 729 a trigger at
  (−656.5, 24.8) and 740 ("Meet Meshif on the palace steps") triggers at (−672.6, 7.0). A player's log (2026-10-19)
  ran 452 → 571 → 698 → 740 in that order. The shortest walk from the quest marker that ends on the palace steps gives
  this order (≈493 units, next best ≈534); without the end it gave (−573, 40) first, which was wrong.
- **Route end:** `QST_Kehj_EliasPalace_03` (574673, "Beneath the Mask") phase 124 ("Meet Meshif at the entrance to the
  Outer Gardens") names exactly one guide marker, (−672.6, 7.0), in `Kehj_Ridge_Subzone_EliasPalace_01` (H). The palace
  dungeon's entrance (`Portal_Dungeon_Generic` → `CSD_Kehj_Magnus_EliasPalace`) is at (−727.9, −44.4) (H, `global_markers`).
- **The guide NPC stands at other quests' places** (H): `QST_Kehj_EliasPalace_01`'s drug den on the Argentek Riverbank
  (−63.0, 93.3, "Find the drug den entrance"), the Forsaken Chapel (−422.9, −72.0, `_02` phases 44/170), the crossing's
  start (−480.3, 43.6) and the palace steps (−672.6, 7.0). It is in the client's live actor list (M: at the drug den the
  0.1.12 route started on it, stopped for being out of the area, and repeated "Shelter guide." every poll). The route
  therefore starts on a quest actor only within 15 units of the quest marker or of a shelter.
- Quest step texts: StringList `Quest_<quest name>`, labels `Phase<uid>OutsideSubzoneHeader` / `Callback<n>Header`
  (enUS read through `SnoStore.Strings`).
- **Safe zone size** (corrected 2026-10-01): the actor bounds say nothing, but the trigger's proximity shape does: a box
  with `wvOBB` (5, 5, 5), most likely half-extents (M). `BCD_Trigger_Sphere_8m` is r 4 (the name gives the diameter).
  Reader and evidence: [dungeon-objectives](dungeon-objectives.md) §10. The route counts a zone reached within 4 units.
- Groups seen: 29 = PowerDefinition (`VGN_Kehj_EliasPalace_SandstormSafezone` 577575 and `…_SandstormDamage`
  577568; no readable radius), 14 = EffectGroupDefinition, 51 = ConditionDefinition (`QST_Kehj_Arc_EliasPalace_Sandstorm*`,
  not decoded).
- The zones are 80–120 units apart, more than the live actor list's reach (~65 units, [client-memory](client-memory.md) §4).
  Actor loops alone can't lead from one zone to the next, so the app guides to their offline positions.

- The destination, Elias's palace, is the DRLG world `CSD_Kehj_Magnus_EliasPalace` (551995, layout 2), so the full
  dungeon assistant runs inside it. Its level areas are `_02`, `_03`, `_EyeRoom`, `_MageHoldout`, `_OverlookFight`,
  `_PalaceFight` and `_InnerGardens` (H, census 2026-09-29). LevelArea `Kehj_EliasPalace` (575414) is probably the
  overworld approach, going by its `Kehj_` region prefix (L).

## 3. Open questions (need a live session in the quest)

- Which of these actors the client's RActor list shows: the guide 576066 (yes, M, see §2), the safe zone 972175, the highwind
  978721, the tornado parent `VGN_Kehj_EliasPalace_Sandstorm_Tornado_Invisible_Parent` 1085730, and
  `environmentVFX_sandstorm_safeZone_wisps` 1362992. The triggers are server-side gizmos (eType 2, gizmo 44) and
  may be invisible (L). The app starts the route on any of them, or on standing at the quest marker.
- Whether the wisps exist only while a storm blows. That would give a "storm now" signal.
- Whether the static player record (client-memory §3) resolves in `Sanctuary_Eastern_Continent`. It carries the
  world SNO, so it should (M), but it has only been seen live in dungeons and the Mercenary den.

## 4. The Lycander crow (secret cow quest chain, Skovos)

Lookup 2026-10-11, build 3.2.2.73764, CoreTOC + `QuestScriptMarkerReader` on the local install. A player asked to follow
the "Large Crow" that flies a route in Lycander and resets if you fall behind (community guides, May 2026).

- The secret chain's assets share the `OWCSMP` token (quests `OWCSMP_Raven`, `_Tracking`, `_Fire`, `_Secret_Ritual`…). The
  crow step is quest `OWCSMP_Raven` (2546728); its MarkerSet is `Skov_Lycander (OWCSMP_Raven)` (2549003) (H).
- The crow is `OWCSMP_Fly` (2547143): a sign gizmo (eType 2, gizmo 98), named by phase 138, placed at
  (2655.3, −260.3, 22.9). `npc_crow` and the `*_Raven_*` actors are unrelated (H).
- Three `Trigger_Sphere_16m` markers: phase 77 at (2627.1, −343.0), phase 141 at (2698.6, −167.4), phase 151 at
  (2651.6, −257.2). They are 85–100 units apart, beyond the ~65-unit actor range (H for positions; L that they are the
  crow's stops).
- Lycander is part of `Sanctuary_Eastern_Continent` (chunks `_S28..S30_W02..W04` cover the route), so the app runs
  there as `WorldKind.Overworld`: beacons and routes, no radar (H).
- Open: the order of the stops (phase links not read), and whether the flying crow is `OWCSMP_Fly` itself moving or
  another actor (needs a live `--debug` session in the step).
