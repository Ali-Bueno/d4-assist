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
| Transition done | `[WarpManager] Portal completed` |

`Limbo`, `FrontendStartup` and `Sanctuary_Eastern_Continent` are loading/overworld worlds, not dungeons.
DiabloDungeonTimer uses exactly this (FileSystemWatcher + poll, `LogMonitorService.cs:88-89`).
**No log line carries player coordinates**, and there are no quest/objective/death categories.

## 3. Player position

No non-memory source found. None of D4LF, D4Scanner, DiabloDungeonTimer or d4-map-overlay track the
player (`d4-map-overlay` projects static markers via SIFT/homography on a screenshot). Minimap
computer vision was ruled out for this project (imprecise, per-resolution setup).

## 4. Anti-cheat stance (facts only)

Blizzard confirmed memory-reading overlays such as TurboHUD4 risk account action
([GameSpew](https://www.gamespew.com/2023/07/blizzard-confirms-third-party-tool-turbohud-could-get-you-banned-from-diablo-4/)).
No statement found naming the screen-reader-text or log-tailing techniques; reported bans concern
memory readers.
