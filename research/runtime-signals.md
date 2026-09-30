# Diablo IV — non-invasive runtime signals

What the running game exposes **without** reading its memory or injecting code. Studied 2026-09-26
against build 3.2.1.73552 and the community tools listed below (shallow clones, read-only).

## 1. Game screen-reader text

- D4 ships `Tolk.dll` next to `Diablo IV.exe`. Speech is routed through it only when
  Accessibility → **Use Screen Reader** and **Use 3rd-Party Screen Reader** are both on
  (`LocalPrefs.txt`: `UseScreenReader "1"`, `UseThirdPartyReader "1"`; checked in `d4lf/src/app/startup.py:90-97`).
- Tolk picks the first active driver in a fixed order: JAWS → Window-Eyes → **NVDA** → System Access
  → ZoomText → SAPI (`tolk/src/Tolk.cpp:34-42`, sticky while active, `Tolk.cpp:88-109`).
- **Fake `saapi64.dll` route (D4LF, D4Scanner route A):** exports the four SA functions
  (`tolk/src/ScreenReaderDriverSA.cpp:24-27`), `SA_IsRunning` always true. Since season 12 the DLL must
  carry a valid Authenticode signature, so both tools self-sign locally (`d4lf/tts/install_dll.cmd:394-399`).
  It only receives text when **no** real screen reader is running (NVDA outranks SA), and the user
  then hears nothing from it. **Not usable for an NVDA user.**
- **NVDA add-on route (D4Scanner route B):** a global plugin wraps `speech.speech.speak`, records the
  text, then calls the original (`D4Scanner/nvda-addon/globalPlugins/d4scanner.py:52-67`). NVDA keeps
  speaking normally; nothing is placed in the game folder. **This is the route for this project.**

## 2. Dungeon identity and transitions — `FenrisDebug.txt`

The game appends to `<install>\FenrisDebug.txt` (older sessions rotate to `_FenrisDebug-N.txt`).
Line shape (regex from `DiabloDungeonTimer/LogEntry.cs:43`):
`([A-Z])\s(\d{4}(\.\d{2}){2}\s(\d{2}:){2}\d{2}\.\d{6})\s?\d*\t\[(\w+)\]\s(.+)`

| Event | Example payload |
|---|---|
| World entered | `[Game] Client entered world \| world: DGN_Hawe_RuinsofEridu \| CWorldID: 524290` |
| Dungeon portal | `[WarpManager] Intro portal to dungeon Subzone 'PIT_Subzone'` |
| Overworld portal | `[WarpManager] Intro portal to exterior Subzone 'Kehj_Caldeum'` |
| Waypoint | `[WarpManager] Intro waypoint to LevelArea 'Frac_Kyovashad'` |
| Game language (each start) | `[Game] Current Speech Locale: enUS  Current Text Locale: enUS` |
| Transition done | `[WarpManager] Portal completed` |
| Cutscene start | `[Game] ptCutscene->m_eCutsceneState: PRELOADING`, then `... PLAYBACK` |

**Game language before the game runs:** `<install>\Launcher.db` holds just the locale Battle.net will start the
game in (4 bytes, `esES`). The launcher rewrites it when the user changes the language (seen 2026-09-29: the
file changed at 09:50, after a session logged `enUS` at 03:53). It is unknown whether it is the text or the
spoken language. The app reads it at startup, and the log's `Current Text Locale` line wins once the game starts.

`Limbo`, `FrontendStartup` and `Sanctuary_Eastern_Continent` are loading/overworld worlds, not dungeons.

**Warps inside one dungeon** go `<world> → Limbo → <same world>` with the **same CWorldID**, after an
`Intro portal to exterior Subzone '<the dungeon's own subzone>'` line: the Undercity boss-room warp
(`X1_Undercity_SnakeTemple_03`, 524291 twice, 2026-09-29 04:00) and five warps in `S15_Triad_B_Worldstone`
(1048578, 2026-09-29 17:09–17:34). Undercity floors are separate worlds (`_01`/`_02`/`_03`, new CWorldIDs).
A new instance of the same dungeon gets a new CWorldID (524290 then 1048578 for two runs of the Worldstone),
but ids are reused across worlds, so the app treats a return as the same instance only when `Limbo` alone lies
between the two entries (`GameSession.TransitWorldName`): it keeps the map and mutes cues while loading.
DiabloDungeonTimer uses exactly this (FileSystemWatcher + poll, `LogMonitorService.cs:88-89`).
**No log line carries player coordinates**, and there are no quest/objective/death categories.

**Cutscenes:** the exe holds only these two state strings, so **the end of a cutscene is never logged**. As of
2026-09-29, 8 logs showed only the two startup logo videos. Each game launch also logs `CutscenesGfxInit` at startup
and `CutscenesGfxDispose` at exit. No story cutscene had been logged yet. The app pauses its cues on
`PLAYBACK` and resumes when the world changes or the player walks one map cell (`Game/CutscenePause.cs`).
In-engine scripted cutscenes (`ScriptedCutsceneUI` in the exe) may not log at all.
Live 2026-09-30 (Darkened Way, the Lilith blood petal): the story scene after the use was a type-3 conversation
(`Frac_Und_LilithVhenardChat1b`, ~110 s) that the conversation mute covered from start to end ([client-memory](client-memory.md)
§5). No cutscene state was logged, and none of the 292 registered screens opened or closed (`D4MemScan screens`).

## 3. Player position

No non-memory source found. None of D4LF, D4Scanner, DiabloDungeonTimer or d4-map-overlay track the
player (`d4-map-overlay` projects static markers via SIFT/homography on a screenshot). Minimap
computer vision was ruled out for this project (imprecise, per-resolution setup).

## 4. Anti-cheat stance (facts only)

Blizzard confirmed memory-reading overlays such as TurboHUD4 risk account action
([GameSpew](https://www.gamespew.com/2023/07/blizzard-confirms-third-party-tool-turbohud-could-get-you-banned-from-diablo-4/)).
No statement found naming the screen-reader-text or log-tailing techniques; reported bans concern
memory readers.
