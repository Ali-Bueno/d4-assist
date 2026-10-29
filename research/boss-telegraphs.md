# Boss attack warnings (offline census + live capture; started 2026-10-23)

Goal: a blind player hears that a boss attack is coming, and which way to step, as a sighted player reads the windup and
the warning on the floor. Census tool: a scratch run over the build 3.2.3 data (to become a D4DungeonDump mode); live
capture: `D4MemScan effects`.

## Offline links (build 3.2.3.73883)

- **Boss → attacks:** `ActorDefinition.arAnimSets`@56 (anim sets, group 8) → `AnimSetDefinition.ptPowerEntryList`@32
  (`AnimSetPowerEntry` {snoPower, snoAnim, …}, 12 bytes). The power's name tells the attack
  (`QST_Horadric_Guardian_360_sweep`, `…_Swipe_Right`, `…_Tornadoes`, `…_Wave_Wall`, `…_spiritBomb`, `…_Chain_Tether`,
  `…_Chakram_Throw_v2`); generic entries (`AnimKey_*`, `Knockback`, `GetHit_*`, `CrowdControl_*`) are not attacks.
- **Attack → its effects:** the anim's meta (group 0) holds the effect groups (group 14) its keyframes start, as plain SNO ids:
  `360_sweep` → `QST_Horadric_Guardian_Boss_attk_360_sweep`; `Tornadoes` → `Horadric_Guardian_persistentCast_1hand_wind_large`;
  `Wave_Wall` → `…_damaging_persistentCast_1hand_demonic_large`; `spiritBomb` → `…_persistentCast_1hand_spirits_large`.
- **Not linked:** the power's own meta names no effect actor or warning (only `g_playerClicked`, `g_Thorns`,
  `fxKit_damaging_impactTarget_spirits`), and no effect group names these powers in `EffectGroupDefinition.snoPower`@244.
- **Warnings by name:** effect groups such as `Horadric_Guardian_360_Sweep_Warning` (flDuration 1.0 s),
  `Horadric_Guardian_burstWarning_wind` (2.75 s), `Horadric_Guardian_persistentWarning_spirits`,
  `Horadric_Guardian_harmless_floor_arrow`; and generic kits reused by many bosses: `fxKit_damaging_coneWarning_*`,
  `fxKit_damaging_lineWarning_*`, `fxKit_damaging_persistentWarning_*`, `MonsterAffix_Chaos_WarningRing`, `*_warningRing`.
  798 effect groups have a warning-like name (warning, telegraph, indicator, floor_arrow, ring, _target).
- **Coverage:** of 551 boss-rarity monsters, 542 have attack powers with anim effects; 39 have a warning-named effect in
  an attack anim; 68 have warning effect groups named after them. So named warnings alone cover few bosses: the general signal
  must be the attack's start (its windup), if the live game shows which power a boss is casting.

## Live (to do: `D4MemScan effects --watch 300 --out <file>` in a boss fight)

- Known: the Horadric guardian's barrier is an effect group on its ACD (`Buff_Visual_Effect[<effect group>]`, §11 of
  dungeon-objectives.md). Open: do the warning groups show the same way, and with what lead time; which ACD attribute (if
  any) names the power being cast; do client effect actors (eType 3) for floor warnings reach the actor list.

## Live, the Horadric guardian (2026-10-23, `D4MemScan effects`, 50 ms)

- **No warning on the boss's ACD:** its `Buff_Visual_Effect` carried only the barrier symbols (and other players' effects).
- **Spirit bomb:** `Horadric_Guardian_burstAttractor_spirits_parent` at the boss, 0.2 s later
  `fxKit_damaging_persistentWarning_spirits_*` (swirl ×3, volumeParent, lightParent: one warning) **on the player's spot**,
  lasting 3–4 s; `fxKit_damaging_burstCylindrical_spirits_*` bursts after. Bursts hit the player standing within ~1 unit; one
  6.5 units off missed. Client effects (no ACD), positions and yaw read fine at 50 ms.
- **Effect sizes:** the warning actors' bounds are unit meshes (`wcylBounds` radius 0.1–0.7); the game scales them, so no size
  comes from the data. The app takes a circle as 6 units.
- **Projectiles (eType 10) have an ACD**, so a client-effects-only view misses them:
  - `Horadric_Guardian_wall_demonic`: **the wave wall**, 13 segments ~2 units apart rising together across the room (x 3.5–28 at
    y 24, with `Horadric_Guardian_persistentWall_demonic_wallMeshAlt` client meshes), facing and sweeping along their yaw (−88°)
    at ~2.2 units/s for 5 s, ~12 units. No gap: outrun it along its sweep.
  - `Horadric_Guardian_Tornado_projectile_invisible`: ~4 at a time, drifting ~2 units/s for 5–9 s; their yaw is **not** their
    heading (read it from the motion).
  - `QST_horadric_Guardian_boss_projectile`: the chakram, 10–15 units/s out and back (yaw flips 180°).
  - `fxKit_damaging_projectileLobbed_spirits`: six lobbed spirit orbs in a hexagon.
- The app (`TelegraphWarner`): warning effects by name (circle/cone/line/ring), projectiles `*wall*` as a wave (run on ahead
  of it), `*tornado*` as a mover (step aside when it drifts at the player), with `HAZARD` debug lines for every damaging effect
  and projectile near the player.
- **The spirit bomb's warning is a mark that follows its target** (third run, 2026-10-23): `persistentWarning_spirits_*` kept
  0.0 from the player while they ran (capture rows every 2 units) and burst where they then stood, ~4 s on; marks that rose
  ~6 units off (on Tyrael, Lorath or the mercenary) burst far from the player. The player died 1.6 units from such a burst, a
  wave wall rising beside them at the same moment. Stepping out can't shake it: the app says it follows ("Markiert: …") when the
  circle under the player has moved with them by the next poll, and a way out only for one that stays put.

## Wall wind-up and rise lines (live 2026-10-23/24, five fights, LIFE log)

- `fxKit_damaging_burstAttractor_demonic_parent` (~20 client effects) gathers on the guardian 1.5-2 s before **every** wave wall
  (8 of 8; no wall without it). One wind-up can bring two walls at right angles.
- Walls rise 7.5 units off the guardian (16,16) along the room's axes, square to them, and sweep away from the near edge:
  first seen x = 6.2-6.7 (yaw 3, +X), y = 8.6-9.0 (yaw 90, +Y), y = 22.5-23.7 (yaw -88, -Y).
- A wall rising on the player kills from near-full life at once (2026-10-24 23:59:38: 96 % -> 0 in 0.3 s, wall 1.2-1.6 away;
  same shape in the 23:45:57 death). Behind a wall is safe: it sweeps away.
- Player life: ACD `Hitpoints_Cur` / `Hitpoints_Max_Total` on the player's ACD (full life reads ~2).
- Implemented (1158296): wind-up -> `DodgePlanner.PlanWallRise` (band 2.5 either side of a line, step outward); a wall risen
  within 2.5 of the player -> step back behind it.
