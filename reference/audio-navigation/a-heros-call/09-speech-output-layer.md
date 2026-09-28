# 09 — Speech / Screen-Reader Output Layer

Source: `GameEngine/Ian/SpeechContext.cs`, `Translate/Ian/TranslateSO.cs` + `TranslateAPI.cs`,
`Core/Ian/DiagSO.cs`. This is the channel that turns "entering Armory", scan results, examine text,
and beacon readouts into actual screen-reader / TTS output.

---

## 1. Call chain (top to bottom)

> **MAJOR CORRECTION (verified against the decompiled source):** an earlier
> pass hedged that "in many builds `Diag.Write == log only`". This is **wrong for the real game**.
> `Core/Ian/Diag.cs:7` defaults `Diag.Write` to `Console.WriteLine`, but
> `RPG/Ian/RPGStartup.cs:235` — inside `ConfigureSystems`, called unconditionally at real startup —
> **rebinds it**: `Diag.Write = ent.Speech.SpeakAsync;` (`RPGStartup.cs:235`). So in the shipped game,
> **every** `DiagSO.Say(...)`/`DSay(...)`/`ESay(...)` call anywhere in the codebase (all funnel through
> `DiagSO.cs:12-55`; including error paths like `"Error loading the {0} sound"`,
> `"unable to find radar sound " + name`) ends up **spoken through the mode-routed sink** —
> `SpeechContext.SpeakAsync` → `InnerSpeakAsync`, i.e. whatever backend `SpeechMode` selects (SAPI *or*
> Tolk *or* window title), **not raw SAPI** — the exact same sink as player-facing `TranslateSO` text. There is **no separate silent debug-log channel** in the shipped build. `Console.WriteLine` is
> only the pre-`ConfigureSystems` default (dev/editor-time only). **Pitfall for porting this pattern:**
> any `ESay`/`DSay` call sitting in a hot path (e.g. per-frame code) will spam TTS in production, not
> just a log file.

```
gameplay code
  TranslateSO.TSay("entering {0}", region.FriendlyName)   // localized, with substitutions
      -> TranslateAPI.TSay(base, args)
          -> Diag.Write(GetTranslatedText(ts))            // Diag.Write is the single sink
              -> SpeechContext.InnerSpeakAsync(text)      // routed to the active output backend
DiagSO.Say/DSay/ESay(text)  // NON-localized convenience -> Diag.Write(text)  [SAME sink as above,
                            // once RPGStartup.ConfigureSystems has run — IS spoken via TTS, not log-only]
```

- **`TranslateSO.TSay` / `TranslateSO._`** is the localized path (looks up a base string + fills
  `{0},{1}…`). Use it for anything player-facing.
- **`DiagSO.Say/DSay/ESay`** are protected helpers on the `DiagSO` base (`Core/Ian/DiagSO.cs:12-55`);
  in the real game they forward to `Diag.Write`, which **is** `SpeechContext.SpeakAsync` once startup has
  run — they are spoken, not merely logged. Player-facing speech goes through the `Translate` layer and
  `SpeechContext`, funneling into the **same** sink.

---

## 2. `SpeechContext` — the backend router

```csharp
public sealed class SpeechContext : GESO {
    public Action<string> InterceptSpeech;   // test/override hook: if set, receives text and returns
    public MySAPI SAPI { get; }              // Windows SAPI5 TTS
    private readonly BlockingCollection<Action> mCommands;   // background speech queue

    public void SpeakAsync(string text)        => mCommands.Add(() => InnerSpeakAsync(text));
    public void SpeakLowPriority(string text)  => mCommands.Add(() => InnerSpeakAsync(text));
    public void SAPICancelAndSay(string text)  => mCommands.Add(() => { SAPI.CancelAll(); if(!text.IsBlank()){ SAPI.SpeakAsync(text); SpeechLog.Log(text);} });
    public void CancelAllSpeech()              => mCommands.Add(() => { /* per-mode interrupt, below */ });
}

private void InnerSpeakAsyncWithoutLogging(string text) {
    if ((G.GameConfig?.NoSpeech ?? false) || text.IsBlank()) return;
    if (InterceptSpeech != null) { InterceptSpeech(text); return; }   // hook wins
    SpeechMode mode = m_world.UserSettings.SpeechMode;
    switch ((int)mode) {
        case 3: break;                          // Silent
        case 2: SetFormText(text); break;       // show in window/form text
        case 1: Tolk.Output(text, false); break;// Screen reader (NVDA/JAWS/Narrator) via Tolk
        default: SAPI.SpeakAsync(text); break;  // 0: SAPI TTS
    }
}
```

### SpeechMode

> **CORRECTION:** the real enum (`GameEngine/Ian/SpeechMode.cs:5-18`, a `StringEnum` with its own
> display strings) labels mode 2 **"NVDA with custom braille support"**, not a generic visual fallback.
> `SetFormText` → `Window.SetTitleText` (`SpeechContext.cs:197-212,219-223`) is the correct mechanism,
> but its *purpose* per the game's own label is feeding **NVDA's braille display via the window title**
> — not "show text for sighted testers."
>
> **Title/caption commit timing (relevant to mode 2).** The window-title text isn't pushed the instant
> it's set — it's **batched**. `GameLoopLogic.RunLoops` calls `Speech.CommitTitleChange(ent)` only once
> the pending-speech count **stabilizes** (`pendingMessages == Speech.PendingMessages`) **or exceeds 10**
> (or a `ForceTitleCommit` is requested) (`GameLoopLogic.cs:47-49`); `CommitTitleChange` then does the
> actual `Window.SetTitleText(NextFormText)` and resets the counter (`SpeechContext.cs:214-223`). So the
> mode-2 braille/title output updates on message-queue quiescence, not per call — worth knowing if you
> port the window-title transport and wonder why updates coalesce.

| Mode | Const name | Backend | Notes |
|------|------------|---------|-------|
| 0 | `SAPI` | **SAPI5** TTS (`MySAPI` over SpVoice COM) | default self-voicing |
| 1 | `Tolk` | **Tolk** → NVDA / JAWS / Narrator | the screen-reader path |
| 2 | `CustomNVDA` | Form/window-title text | **"NVDA with custom braille support"** (game's own label) — feeds a braille display via the window title, not a sighted-tester fallback |
| 3 | `NoSpeech` | Silent | speech off |

> **The backends are mutually EXCLUSIVE — exactly one per mode, never "screen reader AND SAPI".**
> `InnerSpeakAsyncWithoutLogging` (`SpeechContext.cs:134-167`) is a plain `switch` on the saved mode: 0 →
> `SAPI.SpeakAsync` (SpVoice COM), 1 → `Tolk.Output` (the **only** screen-reader interop — `DavyKager.Tolk`),
> 2 → `SetFormText` (window-title/braille), 3 → nothing. **`SpeechMode` is a *saved user setting*, not
> autodetected** — the game never probes for a running screen reader; the player picks the mode. Matching
> that, **`Tolk.Load()` is lazy**: it's only ever called when the saved mode reads back as `1`
> (`SpeechMode.LoadFromDisk`, `SpeechMode.cs:29-33`), so Tolk isn't even initialized unless the user chose
> the screen-reader mode. (Enum + display strings: `SpeechMode.cs:11,16`.)

`SpeakLowPriority` (`SpeechContext.cs:72-78`) is **not actually lower priority** — its body is
byte-for-byte identical to `SpeakAsync` (`SpeechContext.cs:109-115`): both are
`mCommands.Add(() => InnerSpeakAsync(text))`. Same FIFO queue, no distinct priority lane; treat the name
as a misnomer/vestigial distinction, not a real feature to port. Speech runs on **its own command
thread** (`SpeechContext.StartThread`), **separate from the audio thread** (Doc 08 §7) — the two never
share a queue.

### Interruption (`CancelAllSpeech`) — per mode

`CancelAllSpeech` (`SpeechContext.cs:80-101`) cancels **only the currently active backend**, mirroring the
speak switch — it never blindly cancels all three:

```csharp
if (mode == 2 && FormIsFocused) Platform.SendBogusCancelSpeechKey();
else if (mode == 1 && FormIsFocused) Tolk.Silence();     // screen-reader stop
else SAPI.CancelAll();                                    // TTS stop
```

`HandleRegion` (Doc 04 §7) calls `CancelAllSpeech()` before announcing a new region so the latest
area interrupts whatever was being read — the right UX for navigation.

### Threading

A background thread loops on `mCommands.TryTake(out item, 10ms)` and executes each queued speech
action (`Perf.TimeIt(... item)`). So speech never blocks the game loop; ordering is FIFO with
`CancelAllSpeech` enqueued like any other command (it cancels whatever the backend is currently
uttering when it runs).

---

## 3. Third speech pathway (missing from earlier pass): SAPI synthesized to a positioned 3D FMOD sound (combat TTS)

Beyond the `SpeechContext`/`Diag.Write` sink above (menu/world speech) and Tolk/screen-reader routing,
combat narration uses a separate, hybrid speech+3D-audio pipeline:

- `MySAPI.SpeakToMemoryStream(text)` (`Speech/Ian/MySAPI.cs:118-132`) redirects the SAPI voice's
  `AudioOutputStream` to an in-memory COM stream, speaks synchronously, and hand-builds a WAV header
  (mono, 16-bit, 22000 Hz hardcoded) around the raw PCM.
- `FModSoundContext.Get3DSoundFromStream` (`FModSoundContext.cs:523-557`) creates an FMOD low-level
  `Sound` from that WAV byte buffer (`MODE._3D | CREATESAMPLE | OPENMEMORY`) and routes it to the
  channel group of bus **`"bus:/Radar and Beacon MASTER/Accessibility Sounds/Combat TTS"`**
  (`FModSoundContext.cs:547`) — a bus that exists specifically for **accessibility** combat narration,
  mixed under the Radar/Beacon bus.
- `CombatEvent.HandleTimelineMarker` actually has **TWO distinct speech routes**, worth keeping apart:
  - **Route 1 — per-target positional TTS** (the `"TTS{n}"` markers, `CombatEvent.cs:289-311`): for each
    combat target with per-target text it translates the string, `SpeakToMemoryStream` → `Get3DSoundFromStream`
    → low-level `playSound`, calls `set3DAttributes` at the **actor's `(X,Y)`**, then immediately
    `set3DMinMaxDistance(1000f, 1000f)` (line 303) — so the voice is **positioned/panned by direction but
    effectively NOT distance-attenuated** (always full volume). This is the spatialized enemy-narration path.
  - **Route 2 — the summary** (the `"TTSSummary"` marker, `CombatEvent.cs:261-268`): just
    `DiagSO.DSay(translatedText)`, i.e. **plain mode-routed speech** through the §1 sink (spoken via
    whatever `SpeechMode` selects) — **not** positioned, no 3D sound.
- Driven by **FMOD Studio timeline markers** authored per combat animation event (`AttackVocal`,
  `Hit{n}`, `Critical{n}`, `Reflect{n}`, `Parry{n}`, `Effect{n}`, `ImpactVocal{n}`, `TTS{n}`,
  `TTSSummary`, `Advance`, `TravelStart`) via FMOD's native `EVENT_CALLBACK_TYPE.TIMELINE_MARKER`
  callback (`CombatEvent.cs:84,107-126`).
- **Third combat audio path (not speech, but same file):** combat also fires a **positional 3D footstep**
  via `mSC.Play3DFootstep(..., heardThroughWalls: true, ...)` (`CombatEvent.cs:366`) — a footstep code
  path distinct from the exploration footsteps (Doc 08 §7), notably flagged to be **heard through walls**.
- Related but distinct: `FModSoundContext.GetVoiceProgrammerSoundEvent`/`FModCallbackWrapper`
  (`FModSoundContext.cs:834-877`) implement FMOD's "programmer sound" callback for pre-recorded VO clips
  (`event:/VO/2D Dialogue`, `event:/VO/Dialogue Sequence`) — **not** SAPI, a fourth, unrelated mechanism;
  don't conflate it with the combat-TTS path above.

**Reusable pattern for our mods**: synthesizing TTS to PCM and injecting it into the game's own audio
engine as a positioned (but non-attenuated) sound is a legitimate way to get positional speech cues
without a separate audio-mixing layer — worth considering wherever a mod needs directional narration
(e.g. "enemy to your left") layered on top of an existing FMOD/Wwise/audio-engine mix.

---

## 4. Relevance to your stack: Tolk vs PRISM

The game uses **Tolk** (`Tolk.Output(text, interrupt)`, `Tolk.Silence()`) for the screen-reader path —
exactly the legacy .NET pattern. Your projects standardize on **PRISM** for native work and keep Tolk
only as a fallback for legacy .NET/BepInEx mods. The mapping is 1:1:

| AHC (Tolk) | PRISM equivalent |
|------------|------------------|
| `Tolk.Output(text, false)` | `prism_backend_speak(be, text, /*interrupt=*/false)` |
| `Tolk.Output(text, true)` / then speak | `prism_backend_speak(be, text, /*interrupt=*/true)` |
| `Tolk.Silence()` | speak empty with interrupt, or backend stop |
| SpeechMode routing | `prism_registry_acquire_best` picks NVDA/JAWS/SAPI/VoiceOver/etc. automatically |

So when porting AHC-style announcements into your mods: replace the `SpeechContext` backend switch with
a single PRISM call (`interrupt=true` on context changes like region/zone, `interrupt=false` for
additive info), or Tolk via `DavyKager` in legacy .NET mods.

---

## 5. Design rules worth copying

- **One sink, many backends.** All speech funnels through a single method that picks the backend; this
  is what lets a mod swap Tolk↔PRISM↔SAPI in one place. (Note per §1: in AHC this sink also swallows all
  diagnostic `DiagSO` calls — decide deliberately in your own mod whether debug/error strings should be
  spoken or kept on a genuinely separate log channel.)
- **Async queue + explicit interrupt.** Don't block the game; expose `Speak(text, interrupt)` and call
  `interrupt=true` exactly on context changes (new region/zone/menu), `false` for incremental info.
- **Localized templates** (`TSay("entering {0}", name)`) keep strings translatable and out of logic.
- **Diff-gating lives upstream** (Doc 05): the speech layer just speaks; the *decision* not to repeat
  unchanged info is made by the navigation code.
- **Positioned TTS-as-audio** (§3): for directional narration, consider synthesizing speech to PCM and
  playing it through your audio engine's positional mixer, independent from your main screen-reader sink.
