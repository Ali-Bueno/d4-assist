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
