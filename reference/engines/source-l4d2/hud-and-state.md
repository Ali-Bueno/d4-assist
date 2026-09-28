# VScript — custom HUD (EMS) & cross-map state persistence

> Two EMS appendices. **Key takeaways up front:** the EMS HUD API is **write-only** — it builds your own
> custom HUD, it does **not** read the game's native health/ammo HUD (read those via netprops/entities,
> see [vscript.md](vscript.md)); and `SaveTable`/`RestoreTable` persist a Squirrel table across map/round
> transitions **within a session** (not across game restarts).
>
> Sources: [EMS Appendix: HUD](https://developer.valvesoftware.com/wiki/L4D2_EMS/Appendix:_HUD),
> [EMS Appendix: Table Save Restore](https://developer.valvesoftware.com/wiki/L4D2_EMS/Appendix:_Table_Save_Restore).

## Custom HUD from VScript (EMS) — write-only

**It only writes a custom HUD; it cannot read the native game HUD's values.** For real health/ammo/counts
use player entity netprops, not this API. The HUD API is for putting *your own* text/counters/timers on
screen — e.g. a custom "status line" — which a screen reader still can't read off-screen, so its value to
us is limited (a status HUD you separately narrate, or understanding what EMS exposes).

- `HUDSetLayout(HUDTable)` — hand the layout table to the engine; later table edits reflect on screen.
- `HUDPlace(slot, x, y, w, h)` — reposition a slot (normalized 0.0–1.0).
- Ticker helpers: `Ticker_AddToHud(hudTable, strInit, blink=false)` (uses slot `HUD_TICKER`),
  `Ticker_NewStr(str, timeout=-1)`, `Ticker_Hide()`, `Ticker_SetTimeout/SetBlink/SetBlinkTime`.
- *(Uncertain: `HUDManageTimers`/`HUDReadTimer` are referenced elsewhere but not on this page — likely in
  EMS Appendix: Functions, not verified here.)*

**Layout-entry keys:** `slot` (required), `dataval` (static/auto value), `datafunc` (lambda → dynamic
value), `staticstring` (prefix/suffix), `special` (`HUD_SPECIAL_TIMER0..3`, `HUD_SPECIAL_MAPNAME`,
`HUD_SPECIAL_MODENAME`, `HUD_SPECIAL_COOLDOWN`, `HUD_SPECIAL_ROUNDTIME`), `flags`, `name`,
`PreCallback`/`PostCallback`.

**Slots:** `HUD_LEFT_TOP/LEFT_BOT/MID_TOP/MID_BOT/RIGHT_TOP/RIGHT_BOT`, specials `HUD_TICKER`,
`HUD_MID_BOX`, `HUD_FAR_LEFT/FAR_RIGHT`, score `HUD_SCORE_TITLE`, `HUD_SCORE_1..4`.

**Flags:** align `HUD_FLAG_ALIGN_LEFT/CENTER/RIGHT`; string `HUD_FLAG_PRESTR/POSTSTR`; visual
`HUD_FLAG_BLINK/BEEP/COUNTDOWN_WARN/NOBG`; team `HUD_FLAG_TEAM_SURVIVORS/INFECTED`; time
`HUD_FLAG_SHOW_TIME/ALLOWNEGTIMER`; visibility `HUD_FLAG_NOTVISIBLE`. Field "type" (text/number/timer) is
implied by `special` + `flags` (no explicit type key; no documented progress-bar type — uncertain).

## Cross-map state persistence — `SaveTable` / `RestoreTable`

*(Function signatures/semantics reconstructed from search snippets — Anubis blocked the full page — but
consistent across independent sources; exact size limits unverified.)*

- `SaveTable(string id, table t)` — store table `t` under `id` in a store that survives **level
  transitions, round restarts and team swaps**.
- `RestoreTable(string id, table dest)` — load into `dest`.
- **Persists within the session only** — everything is lost on game quit/restart.
- Serialization is real: an array comes back as a table keyed `0,1,2,…`, not a pure array — don't assume
  array-ness after restore.
- **Pattern:** `::OnSave` (before a transition) and `::OnRestore` (in the new map: `RestoreTable`, modify,
  then `SaveTable` again so the next `OnSave` doesn't clobber it). The `pvprace` gamemode uses this to
  carry race times across levels/team swaps.

**Accessibility use:** persist mod config (verbosity, which cues are enabled) and accumulated player state
(e.g. progress already announced) across the maps of a campaign, under a mod-namespaced `id`, hooked into
the existing `OnSave`/`OnRestore` pattern. For persistence **across sessions**, this won't do — write a
config file via `StringToFile` (or the native plugin's own config), not `SaveTable`.
