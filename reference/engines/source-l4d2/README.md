# Source engine (Source 1) — Left 4 Dead 2

> Engine-axis doc: how L4D2's modding platform lets us **hook, read game state, and call native code**
> (to reach a screen reader). Technique docs (`audio-navigation/`, `ui-accessibility/`) link here rather
> than repeat these platform facts. Cross-checked against AlliedModders wiki, Valve Developer Community
> (VDC) and community threads — sources inline.

## Docs in this folder

- **README.md** (this file) — engine overview, framework choice, transport, toolchain, running/VAC.
- **[vscript.md](vscript.md)** — the Squirrel VScript API (state reads, events, Director) and its hard
  limits; VScript is a server-side state source, not the voice emitter.
- **[scripting-hooks-and-events.md](scripting-hooks-and-events.md)** — Scripted Mode hooks
  (`AllowTakeDamage`, `UserConsoleCommand`…) and the **full** game-event surface (finale, progress,
  weapons, `explain_*` tutorials, `entity_visible`).
- **[dialogue-and-director.md](dialogue-and-director.md)** — Response System (⚠ no native dialogue-speaking
  hook → cue-pausing must be solved natively) and Director panic/wave/Tank/finale detection via events.
- **[vscript-patterns-and-vslib.md](vscript-patterns-and-vslib.md)** — copy-paste Squirrel patterns
  (player iteration, `Update()` timers, event hooks, file I/O) and the VSLib assessment (copy 3 patterns,
  don't depend on it).
- **[hud-and-state.md](hud-and-state.md)** — EMS custom HUD (write-only, can't read native HUD) and
  `SaveTable`/`RestoreTable` cross-map state persistence.
- **[authoring-tools.md](authoring-tools.md)** — VPK packaging, `addoninfo.txt`, closed-caption
  vocabulary, dev-console test commands, and how the plugin vs optional content each ship.

## Identity

- **Engine:** Source 1 (2009). **Game:** Left 4 Dead 2, Steam **App ID 550**.
- **Architecture:** **32-bit (x86) only** on Windows. No official x64 executable, no x64 beta branch
  ([Source-1-Games #3847](https://github.com/ValveSoftware/Source-1-Games/issues/3847),
  [Steam discussion](https://steamcommunity.com/app/550/discussions/0/1489992713694650406/)). **This is
  the single most constraining fact** — every native DLL we ship (the plugin and the TTS library) must be
  **x86**. It rules out the PRISM prebuilt release (x64/arm64 only) as-is; see *Screen-reader transport*.
- **Scripting:** Squirrel 3.0.4 via **VScript** (`.nut`). **Not Lua** — Lua is Garry's Mod, not stock
  Source. VScript is **server-side only** in L4D2 (no client VScript; that arrived later with Panorama).
- **Not** BepInEx, **not** Unity, **not** REFramework/UE4SS. Do not reuse those flows.

## The listen-server fact (why "server-side" == local, in-process)

Source has two ways the game logic runs:
- **Dedicated server (`srcds`)** — a separate headless process for online play. **Not our case.**
- **Listen server** — when you play single-player or **local co-op**, your own `left4dead2.exe` hosts the
  server internally **and** renders your client, **in the same process, on your machine**. No network.

A blind player's scenario (solo campaign, or local co-op, with bots) is always a **listen server**.
Consequence: the game's authoritative state (health, ammo, position, special infected, the Director)
lives on the *server side* of the code, but on a listen server that server side is **inside the player's
own process**. So a "server-side" native plugin gets real-time local state **and** can `LoadLibrary` a
TTS DLL in-process — no network, no bridge, no latency. The **only** state a server plugin can't see is
purely-client VGUI: which menu is open, the scoreboard, HUD panels (that's `client.dll`).

## Modding frameworks — the three options

| Vía | Game state (health/pos/specials/director) | HUD & menus (client) | Direct native TTS | RE needed | Distribution / VAC |
|---|---|---|---|---|---|
| **VScript** (Squirrel, sandboxed) | Yes (server) | ❌ | ❌ (can only `StringToFile`) | none | Workshop VPK, VAC-safe |
| **MetaMod:Source plugin** (native C++) | Yes, most complete via documented `hl2sdk` interfaces | ❌ | ✅ in-process `LoadLibrary` | little | manual, needs `-insecure` |
| **Injected `client.dll` DLL** (native C++) | Yes (redundant) | ✅ **only way** | ✅ | much (sig-scanning) | manual, needs `-insecure` |

- **VScript**: `StringToFile`/`FileToString` (≤64 MB), read/set health, `GetOrigin`, find entities by
  model, query the Director. **Sandboxed**: no `system()`, no sockets, no native DLL calls, no HUD/menu.
  Can only precache + emit existing sounds, never synthesize speech. → useful as a *state source* that
  writes to a file, never as the voice emitter. [L4D2 Vscripts (VDC)](https://developer.valvesoftware.com/wiki/L4D2_Vscripts),
  [decrypted vscript dump](https://github.com/keyCat/left4dead2-vscripts).
- **SourceMod vs pure MetaMod:Source plugin**: we do **not** want SourcePawn natives, only native C++
  in-process. A **pure MetaMod:Source plugin (`ISmmPlugin`)** is the lighter fit — it drops the SourceMod
  dependency and the whole `IExtensionInterface` layer. A SourceMod *extension* only earns its keep if we
  later want to expose natives to SourcePawn scripts (we don't).
  [Writing Extensions from MM:S Plugins](https://wiki.alliedmods.net/Writing_Extensions_from_Metamod:Source_Plugins).
- **Injected client DLL**: the only way to reach purely-client VGUI/HUD state (open menu, scoreboard,
  chat, HUD hints). More RE (sig-scanning `client.dll`, vtable hooks) — no official client SDK. Reserve
  for the UI/menu phase. Reference precedent (Source family, no TTS): [cs2-accessibility-mod](https://github.com/bfjesso/cs2-accessibility-mod),
  [L4D2 EntityList (GuidedHacking)](https://guidedhacking.com/threads/l4d2-entitylist.9279/).

### Chosen approach for this mod
**Pure MetaMod:Source plugin (`ISmmPlugin`)** as the backbone: documented `hl2sdk` (branch `l4d2`)
interfaces for game state (no blind RE), and load the TTS DLL in-process. Add a light `client.dll` hook
**later** for menu/HUD accessibility (the one thing a server plugin can't see). VScript stays a
fallback/secondary state source only.

## Does MetaMod:Source load on an L4D2 listen server? — **Yes** (with one caveat)

Confirmed working on single-player/listen server
([AlliedModders thread](https://forums.alliedmods.net/showthread.php?t=140816); the MM:S
[VDF generator](https://www.metamodsource.net/vdf) lists "Left 4 Dead 2" officially). **Caveat:** MM:S
plugins do **not** load until a **map is loaded** — at the main menu `meta`/`sm` report "unknown
command". So the plugin's real work must start **post map load** (hook `IServerGameDLL::ServerActivate`),
not at process start. May also require `-insecure`. (SourceMod specifically is the flaky one on listen
servers; the pure MM:S layer we depend on is fine.)

## Screen-reader transport — the x86 problem

Because L4D2 (and therefore the MM:S plugin) is **x86**, and the **PRISM prebuilt has no 32-bit build**
(`prism-windows-x64.zip` / `arm64` only — see [prism-prebuilt.md](../../screen-reader-integration/prism-prebuilt.md)),
we cannot just ship `prism.dll`. Options, in order of pragmatism:

1. **Tolk (x86)** — native C library with a 32-bit build (`Tolk.dll` + `nvdaControllerClient32.dll` +
   SAPI fallback), directly callable from the x86 plugin with zero bridge. Tolk's only downside vs PRISM
   (Windows-only) is irrelevant here — L4D2 is Windows-only anyway. This is exactly the "PRISM prebuilt
   unusable on 32-bit → Tolk fallback" case the playbook allows. **Recommended.**
2. **Compile PRISM for x86 ourselves** — keeps the playbook default, but PRISM (C++23/CMake) has no
   published x86 artifact; building/validating a 32-bit PRISM is extra effort and risk.
3. **External x64 bridge** — plugin writes state to a pipe/shared-mem/file; a separate x64 helper process
   speaks via PRISM. Most moving parts, adds latency; only if 1–2 are ruled out.

Keep the transport behind a single `ScreenReaderService` sink (playbook §9) so Tolk↔PRISM is a one-file
swap if PRISM ever ships x86.

## Toolchain (Windows / Visual Studio 2022)

- **hl2sdk (branch `l4d2`)** — documented client/server interfaces; the authoritative RE source, beats
  blind sig-scanning for anything SourceMod already covers:
  `git clone --mirror https://github.com/alliedmodders/hl2sdk hl2sdk-proxy-repo` then
  `git clone hl2sdk-proxy-repo hl2sdk-l4d2 -b l4d2`.
- **metamod-source** (build against its headers): `git clone --recursive https://github.com/alliedmodders/metamod-source`.
- **Minimal sample = `stub_mm`**: [`metamod-source/samples/s1_stub_mm/`](https://github.com/alliedmodders/metamod-source/tree/master/samples/s1_stub_mm)
  — `stub_mm.cpp/.h/.vdf` + legacy VS projects (`msvc8/9/10/`). `PLUGIN_EXPOSE(...)` generates the
  `CreateInterface` export; `Load()` is where you `GET_V_IFACE_ANY(...)` engine interfaces and add
  SourceHook hooks (and where we'd init the `ScreenReaderService`).
- **Build route:** two options — (a) **legacy `.sln`/`.vcxproj`** from `samples/s1_stub_mm/msvc10/`,
  retarget to the VS2022 x86 toolset, point includes at `hl2sdk-l4d2` + `metamod-source/{core,public}`;
  simplest for a minimal DLL. (b) **AMBuild** (`pip install ./ambuild`, needs Python 3.3+):
  `python ..\configure.py -s l4d2 --hl2sdk-root=<...> --mms_path=<...>` then `ambuild`
  (env vars `HL2SDKL4D2`, `MMSOURCE<ver>`). [Building SourceMod](https://wiki.alliedmods.net/Building_sourcemod),
  [AMBuild](https://wiki.alliedmods.net/AMBuild).
- **Install / verify** (paths under the game install):
  `left4dead2/addons/metamod/` (MM:S binaries), `left4dead2/addons/metamod.vdf` (root, generated at the
  [VDF generator](https://www.metamodsource.net/vdf) with "Left 4 Dead 2" selected),
  `left4dead2/addons/<plugin>.vdf` + `.dll` (ours). Verify in the game console with `meta list` /
  `meta version` — **only after loading a map**.

### Manual downloads the user must obtain
- **MetaMod:Source (L4D2, x86)** — https://www.metamodsource.net/downloads.php?branch=stable
- **VDF generator** for `metamod.vdf` — https://www.metamodsource.net/vdf
- **hl2sdk** (branch `l4d2`) — https://github.com/alliedmodders/hl2sdk
- **metamod-source** (headers) — https://github.com/alliedmodders/metamod-source
- **Visual Studio 2022 Community** + "Desktop development with C++" — https://visualstudio.microsoft.com/
- **AMBuild + Python 3.3+** *(only if not using the legacy `.sln`)* — https://github.com/alliedmodders/ambuild
- **TTS library**: **Tolk** (x86 build) — the DLLs the plugin loads. *(SourceMod is NOT needed for the
  pure-plugin route; PRISM prebuilt is x64-only so it does not apply here.)*

## Running the mod (single-player / local co-op = listen server)

- Launch options: **`-insecure`** (disables VAC for the session — **no ban**, it's Valve's own mechanism
  for server mods; blocks joining VAC-secured servers, irrelevant for local play) and
  `+allow_all_bot_survivor_team 1` if bots are needed in Versus/Scavenge.
- Enable the developer console; `map <name>` (start via console, not the campaign menu, if cheats are
  needed); `sv_cheats 1`; bots via `sb_all_bot_game 1`. This creates the local listen server.
- [Console commands (L4D wiki)](https://left4dead.fandom.com/wiki/Console_commands).

## VAC / anti-cheat

- **VScript / Workshop VPKs**: VAC-safe (sandboxed VM, distributed by Valve).
- **MetaMod / any native DLL**: launch with **`-insecure`**. VAC simply doesn't engage — **no ban**; you
  just can't join VAC-secured servers. Standard and safe for local campaign play
  ([Steam discussion](https://steamcommunity.com/app/550/discussions/0/598536819815128750/)).

## Content source: Closed Captions (free, curated by Valve)

L4D2 ships a full closed-caption system (`resource/closecaption_english.dat`, compiled from `.txt` via
`captioncompiler.exe`) covering incoming attacks, special-infected types, story events and player
actions under "Full Captioning" ([VDC Closed Captions L4D2](https://developer.valvesoftware.com/wiki/Closed_Captions_l4d2)).
Screen readers can't read it off-screen (Source renders via Direct3D, no UI Automation tree), but it's an
excellent **vocabulary/event list** for *what* to announce and when — mine it for the announcement layer,
don't try to read it passively. (`con_logfile` is reported broken in L4D2, so console-tail bridges are
out — [Source-1-Games #3601](https://github.com/ValveSoftware/Source-1-Games/issues/3601).)

## Key references

- [L4D2 Vscripts (VDC)](https://developer.valvesoftware.com/wiki/L4D2_Vscripts) ·
  [Script Functions (VDC)](https://developer.valvesoftware.com/wiki/Left_4_Dead_2/Scripting/Script_Functions)
- [hl2sdk (AlliedModders)](https://github.com/alliedmodders/hl2sdk) ·
  [metamod-source](https://github.com/alliedmodders/metamod-source) ·
  [Sample Plugins (MM:S)](https://wiki.alliedmods.net/Sample_Plugins_(Metamod:Source))
- [Writing extensions](https://wiki.alliedmods.net/Writing_extensions) ·
  [Installing MM:S + SourceMod (Windows)](https://wiki.alliedmods.net/Installing_MetaMod:Source_and_SourceMod_(Windows))
- [source-sdk-2013](https://github.com/ValveSoftware/source-sdk-2013) (VGUI/`CHudElement` reference for
  the client-hook phase) · [GH-Offset-Dumper](https://github.com/guidedhacking/GH-Offset-Dumper)
