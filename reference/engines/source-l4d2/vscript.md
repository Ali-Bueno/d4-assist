# VScript (Squirrel) — Left 4 Dead 2

> What VScript can and can't do for an accessibility mod, with the **real** function/class names. Read
> the [engine overview](README.md) first. TL;DR: in L4D2 **VScript is server-side only and sandboxed**
> (no sockets, no native DLL calls, no process spawn) — so it is a **state source**, never the voice
> emitter. Our native MetaMod:Source plugin reads state directly; VScript is a fallback/complement that
> can publish discrete high-level events (Tank spawned, incapacitated, door opened) via `StringToFile`.
>
> Sources: [VScript](https://developer.valvesoftware.com/wiki/VScript),
> [VScript Fundamentals](https://developer.valvesoftware.com/wiki/VScript_Fundamentals),
> [L4D2 Scripting](https://developer.valvesoftware.com/wiki/Left_4_Dead_2/Scripting),
> [Script_Functions](https://developer.valvesoftware.com/wiki/Left_4_Dead_2/Scripting/Script_Functions),
> [Director_Scripts](https://developer.valvesoftware.com/wiki/Left_4_Dead_2/Scripting/Director_Scripts),
> [L4D2 events (AlliedModders)](https://wiki.alliedmods.net/Left_4_dead_2_events),
> [List of L4D2 Netprops](https://developer.valvesoftware.com/wiki/List_of_L4D2_Netprops).

## 1. What it is / the hard constraints

- **Language:** Squirrel **3.0.4** VM embedded in the game. Introduced in L4D2 (later TF2/Portal 2/CS:GO…).
  Source 2 uses Lua; CS2 uses JS — **not** applicable here.
- **Server-side ONLY in L4D2.** VDC: *"Left 4 Dead 2 VScripts are server-side scripts run in an in-game
  virtual machine."* There is **no client-side VScript VM** → no low-latency client HUD reads from
  VScript. This is why the mod's real-time layer is the native plugin, not VScript.
- **Sandbox (no public API for any of these):** no sockets/network, no native-DLL/P-Invoke, no
  `exec`/process spawn. File access is limited to `StringToFile`/`FileToString` in a **dedicated
  directory, no path traversal, ~64 MB write cap**. → VScript cannot talk to a screen reader itself; the
  only escape hatch is writing state to a file for an external/native reader.

## 2. Loading & lifecycle

- **Location:** `left4dead2/scripts/vscripts/` (loose or inside a `.vpk`). Extensions `.nut` (plain) /
  `.nuc` (compiled/encrypted, ICE key `SDhfi878` — for reading official content only, never depend on it).
- **Console:** `script <code>` (inline Squirrel), `script_execute <file>` (loads a `.nut`).
- **Addon scripts that always load without overwriting base ones** (our injection points if we use
  VScript at all): `mapspawn_addon.nut` (once per chapter, root scope), `director_base_addon.nut`
  (twice per round, `DirectorScript` scope), `scriptedmode_addon.nut`, `response_testbed_addon.nut`.
- **Entities:** `logic_script`, `point_script_use_target`, `info_director` inputs (`BeginScript`,
  `EndScript`, `ScriptedPanicEvent`); any entity's "Entity Scripts (vscripts)" keyvalue + inputs
  `RunScriptFile` / `RunScriptCode`. Hot reload: `ent_fire <ent> runscriptfile <path>`.
- **Auto-naming:** `[map]_[mode].nut` autoloads per mode; `[map]_finale.nut` for finales.
- **`IncludeScript(name[, scopeTable])`** merges another `.nut` into a scope. Each script has its own
  **Script Scope** (nested table); `getroottable()` reaches root. Scope vars persist between calls —
  that's where an accumulating accessibility tracker would keep state.
- **Think / polling:** entity keyvalue `thinkfunction`, or **`AddThinkToEnt(ent, "Fn")`** — periodic
  callback (documented example 0.1 s, **not per-frame**). `EntFire` / `DoEntFire` to fire I/O;
  `ConnectOutput(outputName, "Fn")` to bind an entity output to a script function.
- **Scripted Mode** (adding a Mode Script) is what enables game-event callbacks and script hooks and
  lifts the hardcoded `MaxSpecials` cap.

## 3. Reading game state — the part we'd actually use

**Entity lookup — global `Entities` table:** `FindByClassname(prev,"cls")`, `FindByName(prev,"name")`,
`FindByModel(prev,"mdl")`, `FindByClassnameNearest("cls",origin,r)`, `FindByClassnameWithin(prev,"cls",
origin,r)`, `FindInSphere(prev,origin,r)`, `First()` / `Next(prev)`.

**`CBaseEntity` (any entity, incl. players):** `GetOrigin()`, `GetAngles()`, `GetVelocity()`,
`GetForwardVector()`, `GetHealth()`, `GetMaxHealth()`, `GetClassname()`, `GetModelName()`, `GetName()`,
`GetCenter()`, `IsValid()`, `GetEntityIndex()`, `GetOwnerEntity()`, `GetMoveParent()`.

**`CTerrorPlayer` (survivors/infected — the core of narrating the player):**
- Health: `GetHealth()` (permanent), `GetHealthBuffer()` (temp/adrenaline).
- Identity/orientation: `GetPlayerName()`, `GetNetworkIDString()` (SteamID), `GetSurvivorSlot()`,
  `GetZombieType()` (1–8), `EyePosition()`, `EyeAngles()` (heading), `IsSurvivor()`.
- **Incap/death (narrate):** `IsIncapacitated()`, `IsHangingFromLedge()`, `IsGettingUp()`, `IsGhost()`,
  `IsDead()`, `IsDying()`, `IsImmobilized()`.
- **Combat/anim:** `IsFiringWeapon()`, `IsOnFire()`, `IsInCombat()`, `IsStaggering()`.
- **Grabbed by a special (audio-cue critical):** `IsDominatedBySpecialInfected()`,
  `GetSpecialInfectedDominatingMe()`.
- Misc: `GetActiveWeapon()`, `GetAliveDuration()`, `GetButtonMask()`, `GetSenseFlags()`.

**Netprops (fallback when there's no high-level getter):** global **`NetProps`** table —
`GetPropInt/Float/String/Vector/Entity(ent, "m_...")`, `SetProp*`, `HasProp(ent, prop)`. See the
netprops list linked above.

**NavMesh (`CNavMesh` / `TerrorNavArea`) — for navigation cues:** `GetNavArea(origin,beneath)`,
`GetNearestNavArea(origin,maxDist,checkLOS,checkGround)`, `GetNavAreasInRadius(origin,r,out)`,
`NavAreaBuildPath(start,end,goal,maxLen,team,ignoreBlockers)` (route existence),
`NavAreaTravelDistance(start,end,maxLen)`; area: `GetCenter()`, `ContainsOrigin(p)`, `IsBlocked(team,...)`,
`IsCleared(player)`, `GetAttributes()`.

## 4. Game events / hooks

**Mechanism:** in Scripted Mode, a function named `OnGameEvent_<event>(params)` is auto-called when that
event fires. Valid events live in `resource/gameevents.res`, `serverevents.res`, `modevents.res`; run
`display_game_events 1` in console to enumerate what's actually available at runtime (don't assume a
static list). Generic Source registration also exists (`__CollectGameEventCallbacks`,
`RegisterScriptGameEventListener`).

**Events worth narrating** (name → key params; from AlliedModders, verify in-game):

| Event | Params | Accessible use |
|---|---|---|
| `player_hurt` | userid, attacker, health, dmg_health, hitgroup, type | damage taken |
| `player_death` | userid, attacker, weapon, headshot, victimname | death |
| `player_incapacitated(_start)` | userid, attacker, weapon, type | went down |
| `tongue_grab` / `choke_start` | userid, victim (,distance) | Smoker |
| `lunge_pounce` / `pounce_end` | userid, victim, distance, damage | Hunter |
| `jockey_ride(_end)` | userid, victim (,ride_length) | Jockey |
| `charger_carry_start` / `charger_pummel_start` | userid, victim | Charger |
| `fatal_vomit` | userid, victim | Boomer |
| `revive_begin` / `revive_success` | userid, subject | revive |
| `heal_begin` / `heal_success` | userid, subject, health_restored | med-kit |
| `tank_spawn` / `witch_spawn` / `witch_killed` | (tankid / witchid …) | boss present |
| `door_open` / `rescue_door_open` | userid, checkpoint, closed | safe room / rescue |
| `item_pickup` | userid, item | pickup |
| `infected_death` | attacker, headshot, weapon_id | common killed |

## 5. The Director from VScript

A **Director Script** (loaded via `info_director`→`BeginScript`) writes the global **`DirectorOptions`**
table to override AI Director internals. **Only one Director Script runs at a time** — loading a new one
ends the previous and resets its `DirectorOptions`, so mod logic must **merge** with a campaign's script,
not just coexist.

**State queries (global / Director object):** `GetFurthestSurvivorFlow()` (mission progress),
`GetClosestSurvivor(origin,incap,rescue)`, `GetHighestFlowSurvivor()`, `GetAveragedSurvivorSpeed()`,
`GetCommonInfectedCount()`, `GetPendingMobCount()`, `IsTankInPlay()`, `IsFinale()`,
`IsFinaleEscapeInProgress()`, `IsAnySurvivorInCombat()`, `HasAnySurvivorLeftSafeArea()`,
`GetTimeSinceSpecialZombieKilled(type)`, `GetGameMode()`, `GetMapName()`, `GetMapNumber()`,
`IsSinglePlayerGame()`. `DirectorOptions` fields of note: per-special limits (`SmokerLimit`…,
`MaxSpecials`), boss control (`ProhibitBosses`, `TankLimit`, `WitchLimit`), callbacks
(`OnBeginCustomFinaleStage`, `ShouldPlayBossMusic` — can flag incoming Tank music for an audio cue).

## 6. Output / bridging (if VScript is used as a state source)

- **`StringToFile(path, str)` / `FileToString(path)`** — the viable way for server-side VScript to
  *publish* state to an external/native reader (poll the file or watch it with `ReadDirectoryChangesW`),
  given no sockets/native calls. Sandboxed dir, size-capped, no traversal.
- **Console text:** `printl`, `Msg`/`print`, `realPrint`, `ClientPrint(player, dest, msg)` (broadcast if
  player null). *(Console-tail bridge is fragile and `con_logfile` is broken in L4D2 — prefer the file.)*
- **Sound:** `PrecacheScriptSound("Sound.Script")` then `EmitSoundOn("Sound.Script", ent)` — positioned
  playback of an existing soundscript; usable for discrete logic-triggered cues (e.g. "Tank near"), but
  fine-grained proximity/radar cues belong in the native layer.
- **HUD:** `HUDSetLayout/HUDPlace/HUDManageTimers/HUDReadTimer`, plus `env_hudhint`/`game_text` via
  `EntFire` — visual complement, not speech.

## 7. Gotchas

- Server-side only → real-time client reads need the native plugin (or memory/netprop reads), not VScript.
- No network / native DLL / process — everything via Squirrel primitives + exposed game functions.
- File I/O is one sandboxed dir, size-capped, no traversal — can't write to an arbitrary IPC path.
- Only one active Director Script — merge, don't collide.
- Not all events are in `modevents.res`; verify with `display_game_events 1` in-game.
- No true per-frame tick (finest documented think ~0.1 s) → real-time proximity cues belong in native
  code; let VScript emit discrete high-level events only.

## How this maps to our mod

VScript is **plan B / a complement**, not the backbone. The native MetaMod:Source plugin (see
[README](README.md)) reads the same state directly in-process on the listen server — lower latency, and
it can also reach the screen reader. Reserve VScript for cases where a clean high-level event is far
easier to get from Squirrel (`OnGameEvent_*`, Director queries) than from raw memory; publish those via
`StringToFile` for the plugin to consume, or port the same event hooks natively.
