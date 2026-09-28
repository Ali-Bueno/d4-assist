# Accessibility architecture on BepInEx/Unity

How an accessibility mod is put together on this platform, and the hard-won lessons from three shipped
ones (Core Keeper — Mono; Megabonk and DRG Survivor — IL2CPP). Cross-engine *technique* lives in
[audio-navigation](../../audio-navigation/) and [ui-accessibility](../../ui-accessibility/); this page
is only the BepInEx-specific shape.

---

## 1. Speech transport: C# P/Invoke → PRISM

Unity hosts are managed C#, so there is no Lua bridge problem — `[DllImport]` reaches native directly.

- **Use PRISM**, dynamically loaded. The official .NET binding (`Prismatoid`) targets **.NET 10** and
  will **not** load inside a BepInEx host (net472/net6), so P/Invoke the C API yourself.
- Exports are **undecorated cdecl** on x64. Call sequence: `prism_config_init` → `prism_init` →
  `prism_registry_acquire_best` → `prism_backend_initialize` → `prism_backend_speak(be, txt, interrupt)`
  → `prism_backend_free` + `prism_shutdown`. Take the exact signatures from `include/prism.h` in the
  release — don't guess struct layouts.
- **Ship `prism.dll` next to the mod DLL. Nothing else** — not `tolk.dll`; NVDA/JAWS/SAPI are built in.
- Details and version caveats: [prism-prebuilt.md](../../screen-reader-integration/prism-prebuilt.md).

```csharp
[DllImport("prism.dll", CallingConvention = CallingConvention.Cdecl)]
private static extern bool prism_backend_speak(IntPtr backend, string text, bool interrupt);
```

**Legacy note:** the older mods (Core Keeper, Megabonk, DRG) still use **Tolk** via `TolkDotNet`
(`using DavyKager;` → `Tolk.Load()`, `Tolk.Output(text, interrupt)`, `Tolk.Silence()`,
`Tolk.Unload()`), which needs `Tolk.dll` + `nvdaControllerClient64.dll` in the **game root** and
`TolkDotNet.dll` in the plugins folder. New mods go to PRISM; existing ones migrate behind the adapter
below, which is the point of having an adapter.

### The adapter (identical to every other engine)

```
ScreenReader.Output(text, interrupt) / Silence() / BackendName()
Announcer.Speak / SpeakUnique / SpeakInterrupt   ← dedup + anti-spam, pure logic, ports verbatim
```

One sink, one file. Swapping Tolk→PRISM must be a one-file change; if it isn't, the mod is calling the
backend from too many places.

**Shut down symmetrically** in `Unload()`/`OnDestroy()` — unpatch Harmony *and* stop speech/audio.

---

## 2. Mod layout that has held up

```
Plugin.cs                 BasePlugin: init log, Harmony.PatchAll, ClassInjector registrations
ScreenReader.cs           the single speech sink
Helpers/                  cross-cutting, no game state: text cleaning, localization,
                          player lookup, game-state, input, config persistence, audio direction
Patches/                  one file per UI area; partial classes when a dispatcher grows
Components/              injected MonoBehaviours: one per audio subsystem / reader
localization/*.txt        mod strings, one file per language, shipped next to the DLL
```

Rules that keep it maintainable:

- **Refactor any file past ~200–300 lines.** A button-dispatch patch grows without limit; split it into
  `partial class` files per UI area (class selection, gear, mission, pause…).
- **One shared helper per cross-cutting concern** — text cleaning, game-state, localization, direction
  math. The moment two components each implement "am I in gameplay?", they will disagree
  (see [runtime gotchas §2](bepinex-runtime-gotchas.md#2-cached-game-state-objects-go-stale-without-any-error)).
- **Never hardcode English in source.** All mod strings go through `ModLocalization.Get(key)` backed by
  plain `key=value` text files that users can edit; fall back to English for missing keys, and detect
  the language from the game's own localisation setting so it follows the player's choice at runtime.
- **Prefer the game's own localised text** for game data (stat names, rarities, currency names) via its
  localisation API; only mod-authored phrasing lives in your files.

---

## 3. Reading the UI

- Patch the **specific** button/form classes, not base classes
  ([Harmony rules §1](bepinex-harmony-il2cpp-rules.md)). Route them through one dispatcher that tries
  each concrete type in turn.
- Announce on **selection** (`OnSelect`-equivalent of the game's own button class), and announce
  **results** of actions ("Purchased X", "Cannot afford") — a blind player otherwise can't tell whether
  Enter did anything.
- **Controls the keyboard can't reach are a real category of bug.** Fix them by making the element
  join the game's *own* navigation (set the underlying `Selectable.interactable = true` and
  `Navigation.Mode.Automatic`), not by inventing a keybind. Custom keys are a last resort — users want
  the game's native navigation.
- **Grid navigation jumps unpredictably**; set explicit sequential Up/Down navigation on list items when
  the game's automatic navigation wanders.
- When a screen has no usable focus model at all, take it over: block the `EventSystem`, drive an
  ordered list of collected texts with the arrow keys, restore the `EventSystem` on exit. That's the
  pattern behind the end-screen / pause / milestone readers.
- Stale text and double-announcements are handled by the delay + priority mechanism in
  [runtime gotchas §9](bepinex-runtime-gotchas.md#9-speech-reads-stale-ui-text).

---

## 4. Gameplay audio

- **One `WaveOutEvent` + one `MixingSampleProvider` per subsystem** (NAudio), generators as inputs —
  see [runtime gotchas §8](bepinex-runtime-gotchas.md#8-performance). This is a hard constraint, not a
  style preference.
- Give every cue category a **distinguishable timbre**, not just a different pitch: waveform, envelope
  and rhythm carry identity far better. Beacon-style accelerating beeps for "go here", continuous tones
  for "state".
- Encode direction with **panning + pitch modulation** (higher when the target is ahead, lower when
  behind) through one shared direction helper; a top-down game's "forward" is the camera axis, but a
  vehicle/rotating player needs the *player's* facing instead — take it from the game's own
  facing/move-input API rather than assuming an axis.
- Make everything **configurable and previewable**: per-category volumes, detection ranges, and an
  in-mod menu that plays each cue on demand. Blind users tune this constantly.
- Log a periodic **per-category diagnostic snapshot** during gameplay ("targets: XP=6.2m, crate=18m…").
  It turns "X doesn't sound" reports into a one-log diagnosis.

---

## 5. Bring-up order for a new Unity target

1. Identify runtime (Mono vs IL2CPP) and get BepInEx loading —
   [versions and install](bepinex-versions-and-install.md).
2. IL2CPP: generate + repair interop, decompile `Assembly-CSharp.dll` —
   [interop](bepinex-il2cpp-interop.md).
3. Empty plugin that logs and speaks one line at startup. Prove the speech path before anything else.
4. Probe the per-build API surface (`FindObjectsOfType`, `OnEnable` patches, `NavMesh`, obfuscation) and
   record the answers in the mod's CLAUDE.md — [runtime gotchas §3](bepinex-runtime-gotchas.md).
5. Menus first (the player has to reach gameplay), then HUD/state readers, then spatial audio.
6. Keep a `STATUS.md` from day one (`reference/_mod-status-template.md`).

---

Related: [screen-reader-integration](../../screen-reader-integration/README.md) ·
[audio-navigation](../../audio-navigation/) · [ui-accessibility](../../ui-accessibility/) ·
[README](README.md)
