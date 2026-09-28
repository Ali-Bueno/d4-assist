# VScript — Scripted Mode hooks & the full game-event surface

> The hook functions the game calls automatically, and the **complete** game-event list — the two ways a
> mod learns "something happened". Extends the basics in [vscript.md](vscript.md) (which has the core
> `OnGameEvent_*` events and state reads). Read that first.
>
> Sources: [Script_Functions #Scripted_Mode_hooks](https://developer.valvesoftware.com/wiki/Left_4_Dead_2/Scripting/Script_Functions#Scripted_Mode_hooks),
> [Game_Events](https://developer.valvesoftware.com/wiki/Left_4_Dead_2/Scripting/Game_Events),
> [L4D2 events (AlliedModders, the source VDC was ported from)](https://wiki.alliedmods.net/Left_4_Dead_2_Events),
> [EMS Game Events](https://developer.valvesoftware.com/wiki/L4D2_EMS/Appendix:_Game_Events).

## Scripted Mode hooks

**What it is:** "Scripted Mode" enables extra Director options and hook callbacks. It activates
automatically when a `<current_mode>.nut` script exists (lives in scope `g_ModeScript`). **Only one addon
can use these at a time** — collision-prone outside mutations. Alternative event registration without
full Scripted Mode: `__CollectGameEventCallbacks(this)`.

| Hook | Signature | When | Return / effect | Accessibility use |
|---|---|---|---|---|
| `OnGameplayStart` | `void OnGameplayStart()` | each time gameplay starts | — | reset accessible state on chapter start |
| `OnActivate` | `void OnActivate()` | right after `OnGameplayStart` | — | init services |
| `OnShutdown` | `void OnShutdown()` | mutation ends (map change, wipe) | read `SessionState.ShutdownReason`, `.NextMap` | announce end/transition |
| **`AllowTakeDamage`** | `bool AllowTakeDamage(table dmg)` | **every** damage event | `false` cancels; `true` applies. Table `ScriptedDamageInfo{Attacker,Victim,Inflictor,DamageDone,DamageType,Location,Weapon}` (C++ reads `DamageDone`/`DamageType` back) | **key hook** — intercept every hit with `Location` + type (`DMG_HEADSHOT/BULLET/BUCKSHOT/MELEE/STUMBLE/BLAST/BURN`) *before* it resolves; richer than `player_hurt` |
| `AllowBash` | `int AllowBash(handle basher, bashee)` | each melee shove | `ALLOW_BASH_ALL/NONE/PUSHONLY` | detect/announce shoves and outcome |
| `CanPickupObject` | `bool CanPickupObject(handle obj)` | evaluating a pickup-able prop | must be `true` for `PickupObject()` | confirm a prop is grabbable before announcing |
| `BotQuery` | `bool BotQuery(int flag, handle ent, bool def)` | validating a survivor-bot decision | `BOT_QUERY_NOTARGET`… | only if the mod drives ally bots |
| `InterceptChat` | `bool InterceptChat(string msg, CTerrorPlayer speaker)` | each chat message | `false` hides it from others | route chat text to the screen reader |
| `UserConsoleCommand` | `void UserConsoleCommand(handle plyScript, arg)` | player runs `scripted_user_func <arg>` | — | **custom keybind → on-demand state readout** (bind a key to the console func) |
| `OnGameEvent_<x>` | `void OnGameEvent_<x>(table params)` | game event `x` fires | — | universal bridge to the event list below |

**Gaps:** no dedicated per-tick/update hook here (use `Update()`+`Time()`, see
[vscript-patterns-and-vslib.md](vscript-patterns-and-vslib.md)), no per-special-spawn hook beyond the
`tank_spawn`/`witch_spawn` events, no item hook beyond `CanPickupObject`.

**In-game discovery:** `developer 1; display_game_events 1` prints every event as it fires — verify what's
actually available on this build/DLC instead of trusting a static list.

## Game events — beyond the basics

The VDC page is a 1:1 port of the AlliedModders list; the EMS "Appendix: Game Events" only documents the
`OnGameEvent_<name>(params)` mechanism and points back here. Below are the events **not** already in
[vscript.md](vscript.md), grouped by accessibility use (name → key params).

**Level progress / orientation (fills gaps the basic list lacked):**
`player_entered_start_area`, `player_left_start_area(playercount)`, `player_left_safe_area(playercount)`,
`player_transitioned`, `player_entered_checkpoint(entityid,door,area,doorname)`,
`player_left_checkpoint(area)`, `area_cleared(area)`, `map_transition`, `relocated`, `respawning`.

**Round / mission / session:** `round_start(objective)`, `round_start_pre_entity`, `round_start_post_nav`,
`round_freeze_end`, `round_end(winner,reason,message,time)`, `mission_lost`,
`difficulty_changed(newDifficulty,oldDifficulty,strDifficulty)`. Player conn:
`player_connect(_full)`, `player_disconnect`, `player_first_spawn(map_name,isbot)`,
`bot_player_replace`/`player_bot_replace` (human↔bot swap), `player_afk`.

**Finale / vehicle / rescue (entirely absent from the basic list):** the sequence to hook is
`finale_start(rushes)` → `finale_rush` / `finale_escape_start` → `finale_vehicle_incoming(campaign)` →
`finale_vehicle_ready(campaign)` → `finale_vehicle_leaving(survivorcount)` →
`finale_win(map_name,difficulty)` / `mission_lost`. Plus `finale_radio_start`,
`finale_radio_damaged(health)`, `gauntlet_finale_start`, `finale_bridge_lowering(subject)`,
`survivor_rescued(rescuer,victim,dooridx)`, `survivor_call_for_help(subject)`,
`dead_survivor_visible(subject,deadplayer)`.

**Weapons / ammo / upgrades (much richer than the basics):** `weapon_fire(weapon,weaponid,count)`,
`weapon_fire_on_empty`, `weapon_reload(manual)`, `weapon_given`, `weapon_drop`,
`weapon_pickup(weaponid,weaponslot,dropped_by_infected)`, `total_ammo_below_40`,
`ammo_pack_used(_fail_*)`, `upgrade_pack_begin/_used/_added(upgradeid)`,
`receive_upgrade`, `upgrade_incendiary_ammo`/`upgrade_explosive_ammo`,
`grenade_bounce`, `hegrenade_detonate(x,y,z)`, `molotov_thrown`.

**Healing / revive / support:** `heal_begin/_success(health_restored)/_interrupted`, `pills_used(_fail)`,
`defibrillator_begin/_used(_fail)/_interrupted`, `adrenaline_used`,
`revive_begin/_success(lastlife,ledge_hang)/_end`, `drag_begin/_end`.

**Player physical state (more granular):** `player_hurt_concise(type,dmg_health)`, `player_falldamage(damage)`,
`player_ledge_grab/_release`, `player_blocked(blocker)`, `player_blind`, `player_shoved(attacker)`,
`friendly_fire(attacker,victim,guilty)`, `melee_kill(ambush)`, `entity_killed(...)`.

**Special infected — extra per-type events** (basics only had `tongue_grab`/`tank_spawn`):
Smoker `tongue_release(distance)`, `tongue_pull_stopped`, `choke_start/_end`, `fatal_vomit`;
Hunter `lunge_pounce(distance,damage)`, `pounce_end/_stopped`, `hunter_punched/_headshot`;
Charger `charger_charge_start/_end`, `charger_carry_start/_end/_kill`, `charger_pummel_start/_end(rescuer)`;
Jockey `jockey_ride`, `jockey_ride_end(rescuer,ride_length)`, `jockey_punched/_killed`;
Boomer `boomer_near`, `boomer_exploded(splashedbile)`; Spitter `spit_burst`, `entered_spit`,
`spitter_killed(has_spit)`; Tank `tank_frustrated`, `tank_rock_killed`; Witch `witch_harasser_set(first)`;
generic `zombie_ignited`, `infected_decapitated`.

**Interaction / visibility (very useful, was missing):** `use_target(targetid,classname,isprop)`,
`player_use(targetid)`, **`entity_visible(subject,classname,entityname)`** — a direct candidate for
narrating interactable objects entering view (pair with the wall-sonification raycast),
`weapon_spawn_visible(weaponname,subtype)`, `give_weapon`.

**Contextual tutorials `explain_*`** (dozens: `explain_pills`, `explain_radio`, `explain_gas_can_panic`,
`explain_witch_instant_kill`…), usually `(userid, subject)` — the game's native hook for contextual
tutorials; **intercept to narrate them** instead of showing only visually. Related:
`set_instructor_group_enabled(group,enabled)`, `gameinstructor_draw/_nodraw`.

**Alt modes:** `scavenge_round_start(round,firsthalf)`, `scavenge_gas_can_destroyed`,
`survival_round_start`, `survival_at_10min/_at_30min`, `versus_round_start`,
`versus_marker_reached(marker)`. **Social:** `player_talking_state(player,istalking)` = human **VOIP**,
not AI dialogue (see [dialogue-and-director.md](dialogue-and-director.md)).

## Recommendation for the mod

- Real-time combat feedback → prefer the **`AllowTakeDamage`** hook over `player_hurt` when you need the
  impact `Location`/type before it resolves.
- Level orientation → `player_entered_checkpoint`/`area_cleared`/`map_transition` fill the gaps.
- Tutorials → intercept `explain_*` and speak them.
- `entity_visible` → narrate interactables entering view.
- Custom "read my status" keybind → `UserConsoleCommand` + `scripted_user_func`.
