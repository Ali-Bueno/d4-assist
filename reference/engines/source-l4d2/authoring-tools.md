# L4D2 Authoring Tools — packaging, install & distribution

> The official SDK and the file pipeline for **packaging, installing and distributing** L4D2 content.
> Our mod is a **native MetaMod:Source plugin + optional scripts**, not an asset-replacement addon — so
> most of these tools (Hammer, Faceposer, studiomdl) don't apply. What *does* matter: `vpk.exe`,
> `addoninfo.txt`, the closed-caption pipeline (as a **vocabulary source**), and the dev-console/test
> commands. Read the [engine overview](README.md) first.
>
> Sources: [L4D2 Authoring Tools (VDC)](https://developer.valvesoftware.com/wiki/Left_4_Dead_2/Authoring_Tools),
> [L4D AddonInfo Authoring](https://developer.valvesoftware.com/wiki/L4D_AddonInfo_Authoring),
> [vpk.exe](https://developer.valvesoftware.com/wiki/Vpk.exe),
> [Closed Captions L4D2](https://developer.valvesoftware.com/wiki/Closed_Captions_l4d2). *(VDC is behind
> an anti-scrape challenge; facts cross-checked via search index + real published addons.)*

## 1. Installing the tools

- Steam → Library → set category filter to **"Tools"** → install **"Left 4 Dead 2 Authoring Tools"**
  (AppID **563**, `steam://install/563`).
- **Run L4D2 once and let it fully update before installing the tools.** ~1 GB free. Repair via
  Properties → Installed Files → "Verify integrity of tool files". The old "Add-on Support" component is
  deprecated — not needed. (Linux/Proton: launch option `_PROTON_USE_WINED3D=1 %command%`.)

## 2. What's in the box (only the relevant bits)

| Tool | Purpose | Us? |
|---|---|---|
| **`vpk.exe`** (`<L4D2>/bin/`) | pack/unpack VPK archives | **yes** — if we ship any scripts/config as a VPK |
| **`captioncompiler.exe`** (`<L4D2>/bin/`) | compile caption `.txt` → `.dat` | **reference** — mine the `.txt` for event vocabulary |
| Workshop Manager | publish to Steam Workshop | only if we distribute via Workshop |
| VScript editor plugins | Squirrel editing support | relevant if we add VScript |
| Hammer, Faceposer, Model Viewer, studiomdl, Maya/SketchUp plugins | maps/models/anim | **no** — we don't touch assets |

## 3. VPK addons

### Layout (mirrors the game's tree exactly — wrong hierarchy = not mounted)
```
<addon_root>/
├── addoninfo.txt      (required — metadata)
├── addonimage.jpg     (optional — 512×512 or 640×360; Workshop rescales to 640×320)
├── scripts/           (VScript .nut, etc.)   ← our likely content, if any
├── resource/          (.res, closecaption_*.dat)
├── materials/ models/ sound/ maps/ vgui/     (asset addons — N/A for us)
```

### `addoninfo.txt` (KeyValues1) — real keys
```
"AddonInfo"
{
    addonversion      "1"                 // ≤31 chars, decimals ok
    addontitle        "L4D2 Accessibility"// ≤127
    addonauthor       "…"                 // ≤119
    addonDescription  "…"                 // ≤1023
    addontagline      "…"                 // ~100 chars
    addonContent_Script  "1"              // our type; all other addonContent_* → "0"
}
```
- Workshop upload overrides title/description from the form, but **`addonContent_*` flags are still read
  from `addoninfo.txt`**.
- Deprecated/ignored keys sometimes seen in examples: `addonSteamAppID`, `addonAuthorSteamID`,
  `addonURL0`. *(Note: sources conflict on whether `addonSteamAppID` still matters — one report says a
  wrong/missing value blocks double-click install. Harmless to omit for a manually-copied VPK; if we ever
  need double-click install, set it to `550` and verify.)*

### Packing with `vpk.exe`
```
vpk <directory>              # pack a folder → <dirname>.vpk  (simplest)
vpk a <file.vpk> <files...>  # add files (or @list.txt response file)
vpk x <file.vpk> <files...>  # extract
vpk l <file.vpk>             # list  (L = detailed)
vpk -?                       # help
```
Or drag the addon-root folder onto `vpk.exe` / right-click → "Send to → L4D2 VPK". Auto-**dropped** for
safety: `.zip .reg .rar .msi .exe .dll .com .cmd .bat` — so a native plugin `.dll` **cannot** live inside
the VPK; it ships the MetaMod way (below).

### Install folders
| Folder | Use |
|---|---|
| `left4dead2/addons/` | **manual** addons — user copies the `.vpk` here (or double-clicks it with the game closed and the engine moves it) |
| `left4dead2/addons/workshop/` | Steam-subscribed addons — auto-managed, don't touch |

Both are mounted; addons toggle in-game under Extras → Add-ons.

## 4. Closed captions = free event vocabulary

- Source `.txt`: `<L4D2>/left4dead2_dlc3/resource/closecaption_english.txt`; compiled runtime file:
  `resource/closecaption_english.dat`; compiler `<L4D2>/bin/captioncompiler.exe` (drag the `.txt` onto it
  **from `bin/`**; the `.txt` must be **UTF-16 LE / UCS-2 LE** or it fails silently).
- Format: `"Tokens" { "Event.Token" "Displayed text" }`, supports inline `<clr:R,G,B>` tags. Enable
  in-game via Options → Captions ("Full Captioning") or console `closecaption 1`.
- **For us:** we do **not** recompile the `.dat`. The official `.txt` is Valve's curated list of which
  sound/story events deserve text (hurts, Tank/Witch alerts, pickups, survivor dialogue) → use it as a
  **checklist of vocabulary** for the announcement layer. (Screen readers can't read it live — Source
  renders via D3D; and `con_logfile` is broken in L4D2 — so it's a content source, not a channel.)

## 5. Dev console & local test commands

| Action | How |
|---|---|
| Enable console | Options → Keyboard/Mouse → "Allow Developer Console"; open with `~` |
| Load local map/server | `map <name>` (e.g. `map c1m1_hotel`) — private local (listen) server, all commands allowed |
| Cheats | `sv_cheats 1` (⚠ flags the session) |
| Add survivor bots | `sb_add` (and `sb_all_bot_game 1` for a full bot team; `+allow_all_bot_survivor_team 1` launch option for Versus/Scavenge) |
| Verbose logs / debug | cvar `developer 1`; launch `-dev` / `-allowdebug` (enables `script_help`) |
| Run / debug VScript | `script_execute <file.nut>`, inline `script <code>`, `script_debug`, `display_game_events 1` |
| **Native plugins (MetaMod)** | launch option **`-insecure`** — VAC doesn't engage, no ban; required for any third-party native DLL. Verify plugin with `meta list` **after loading a map** |

## 6. Distribution — how OUR mod actually ships

The mod is two parts with **different** pipelines:

1. **The native MetaMod:Source plugin (`.dll`)** — ships the **MetaMod way**, NOT via VPK (VPK strips
   `.dll`): drop it under `left4dead2/addons/metamod/` (+ its `.vdf`), alongside the MM:S install and the
   TTS DLL (Tolk x86). Distribute as a GitHub release with copy-in instructions. This is the primary
   deliverable.
2. **Optional VScript/config content** — *if* we add any `.nut` scripts or resource config, pack them as
   a `.vpk` (§3) and either drop in `left4dead2/addons/` or publish to Workshop (Workshop Manager; split
   VPKs >~200 MB into a collection). Not required for the plugin itself.

Third-party helpers if ever needed: **VPKEdit** (maintained GCFScape replacement) to inspect VPKs,
**Crowbar** (models + VPK/GMA), **Notepad++** with a Squirrel syntax definition for VScript editing.

## Relevance summary

We need `vpk.exe` + `addoninfo.txt` **only if** we bundle optional VScript/config content or want the mod
listed in the in-game Add-ons menu. The plugin binary + Tolk go through the MetaMod path (see
[README](README.md) → Toolchain / install). The caption `.txt` is a vocabulary reference, not something
we recompile. Everything asset-related (Hammer/models/materials) is out of scope.
