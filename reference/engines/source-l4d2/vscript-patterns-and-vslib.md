# VScript — practical patterns & VSLib

> Copy-paste Squirrel patterns for the things a mod does constantly (iterate players, timers, hook events,
> file I/O, sound) and an assessment of **VSLib**, the community helper library. The API reference is in
> [vscript.md](vscript.md); this is the *how you actually write it* doc.
>
> Sources: [L4D2 Vscript Examples](https://developer.valvesoftware.com/wiki/L4D2_Vscript_Examples),
> [VSLib (wiki)](https://developer.valvesoftware.com/wiki/VSLib), repo
> [L4D2Scripters/vslib](https://github.com/L4D2Scripters/vslib) (MIT per-file header; no repo LICENSE
> file; last push 2022 — unmaintained but the de-facto library).

## Core patterns (official examples)

**Iterate entities / players** — the base `while(FindByClassname…)` loop. Class `"player"` covers
survivors, human infected and bots; filter by team separately:
```squirrel
local player = null
while ( player = Entities.FindByClassname(player, "player") )
    printl( player.GetModelName() )
```

**Periodic loop** — the game auto-calls a function named `Update()` every script tick (no registration);
combine with `Time()` for one-shot or repeating timers:
```squirrel
last_set <- 0
function Update() {
    if ( Time() >= last_set + 5.0 ) {   // every 5 s
        // ... per-interval accessibility poll ...
        last_set = Time()
    }
}
```
This is the **only** timer mechanism the official wiki documents. For named/cancellable timers, use
VSLib's `Timers` (below) or port that pattern.

## VSLib — what it is and how to use it

Community Squirrel library wrapping raw VScript in classes/tables. Install: extract to
`left4dead2/scripts/vscripts/`, then `IncludeScript("VSLib")` (root loads all submodules). Modules:
`Entity`, `Player`, `EasyLogic`, `Timer`, `FileIO`, `Utils`, `HUD`, `ResponseRules`, `RandomItemSpawner`.

| Module | Gives you |
|---|---|
| `Entity` | wrapper: `GetLocation()`, `GetHealth()`, `EmitSound()/StopSound()`, `GetTeam()`, simplified netprops |
| `Player` (extends Entity) | ~150 helpers: `IsAlive/IsIncapacitated/IsDead`, `GetCharacterName()`, `GetSteamID()`, `GetLastAttacker()`, `Revive()/Defib()/Rescue()`, `ShowHint()` |
| `EasyLogic` (~5.7k lines) | wraps ~250 `OnGameEvent_*` into a `Notifications` table with readable names (`OnHurt`, `OnDeath`, `OnIncapacitated`, `OnRoundStart`…); `Players.*` role iteration; per-player state cache |
| `Timer` | delay/repeat/named timers (`AddTimer`, `AddTimerByName`) — internally an `info_target` + `AddThinkToEnt` at 0.1 s |
| `FileIO` | serialize/deserialize Squirrel tables over `StringToFile`/`FileToString` |
| `Utils`/`HUD` | `SayToAll()`, distances; `HUD.Item/Bar/Countdown/Menu` |

### The 3 patterns worth copying (don't take the whole library)

**Player iteration by role** — resolves the team filter once:
```squirrel
function VSLib::EasyLogic::Players::Survivors() {
    local t = {}, ent = null, i = -1
    while (ent = Entities.FindByClassname(ent, "player"))
        if (ent.IsValid()) {
            local p = ::VSLib.Player(ent)
            if (p.GetTeam() == SURVIVORS) t[++i] <- p
        }
    return t
}
// use: foreach (s in ::VSLib.EasyLogic.Players.Survivors()) { ... }
// ready variants: .All() .Infected() .AliveSurvivors() .IncapacitatedSurvivors() .Bots()
```

**Friendly event hook** — raw event → named listener table; subscribe by assigning any key:
```squirrel
::VSLib.EasyLogic.Events.OnGameEvent_player_hurt <- function (params) {
    local e = ::VSLib.EasyLogic.GetPlayersFromEvent(params)
    foreach (fn in ::VSLib.EasyLogic.Notifications.OnHurt) fn(e.entity, e.attacker, params)
}
// subscribe (pattern inferred from source, works because foreach ignores the key):
::VSLib.EasyLogic.Notifications.OnHurt[UniqueString()] <- function(victim, attacker, params) {
    // accessibility logic
}
```

**Auto-precached sound + file table I/O:**
```squirrel
function VSLib::Entity::EmitSound(file) {
    if (!(file in ::EasyLogic.PrecachedSounds)) {
        _ent.PrecacheScriptSound(file); ::EasyLogic.PrecachedSounds[file] <- 1  // precache once
    }
    g_MapScript.EmitSoundOn(file, _ent)
}
// FileIO: SaveTable(name,tbl) → StringToFile(name+".tbl", Serialize(tbl));
//         LoadTable(name) → compilestring("return " + FileToString(name+".tbl"))()
```

### Verdict
**Don't ship VSLib as a dependency** — 5.7k+3.2k lines of gamemode/finale/loot logic we don't need,
unmaintained since 2022, and a broken library breaks the mod if Valve changes something. **Copy** the
three patterns above (role iteration, event→listener wrapper, named timers) with the MIT header preserved.
Note this all only matters **if** we use VScript at all — the native plugin reads the same state directly
(see [README](README.md)); VScript stays the fallback/complement per [vscript.md](vscript.md).
