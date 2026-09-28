# UE4SS mod architecture — resolve, don't scan

**Cross-game synthesis.** How to structure a UE4SS mod so it stays fast and stable, derived from two
shipped accessibility mods measured against each other:

| Mod | Engine | UE4SS | Shape |
|---|---|---|---|
| **Dragon Ball Sparking! ZERO** | UE **5.1** | 3.0.1 | Menu-heavy fighting game. Rewrite of an earlier community mod, so two architectures were measured *inside the same game*. |
| **Dragon Ball Z Kakarot** | UE **4** (ini reports 4.20) | 3.0.1 | Open world: menus **plus** navigation radar, quests, battle monitor. |

Two different engines, two different teams-of-one, and they converged on the same five conclusions.
That convergence is the point: these are properties of **UE4SS**, not quirks of one game.

> This doc is the **architecture decision layer**. It does not repeat what is already written:
> the API surface and cost matrix live in [`ue4ss-api-reference.md`](ue4ss-api-reference.md), the
> implementation patterns and code (caching, `on_screen`, debounce, the firehose case study) live in
> [`accessibility-patterns.md`](accessibility-patterns.md), and the live inspection loop lives in
> [`ue4ss-live-workflow.md`](ue4ss-live-workflow.md). Read this one **first**, then those.

---

## 1. The one decision that determines everything

There are two ways to know *what is on screen right now*:

**Scan.** Call `FindAllOf` / `FindFirstOf`. UE4SS walks the entire global object array, linearly.

**Resolve.** Read the properties the game *already maintains*, by name, from a pointer you already
hold. The game knows which option is focused — it has to, it is drawing it. Don't discover it; **ask
for it**.

### Measured cost of a scan

| Game | Cost of one scan | Notes |
|---|---|---|
| Sparking ZERO | **~115 ms, fixed** | Independent of class and result count. `FindFirstOf` returning **one** object still cost 114 ms. Array inflated by UGC content. |
| Kakarot | **~65 ms avg, 272 ms peak** | 1576 scans in a 5.5-minute session = **102 s of game thread = ~30 %**. Its STATUS calls per-class polling "bankrupt" for that game. |

For contrast, on Sparking ZERO **work on objects you already hold is free**: `GetClass():GetFName()`
≈ 0.003 ms, and 1843 × `HasKeyboardFocus` = 19 ms total. The cost is the *walk*, never the per-object
work. `StaticFindObject` by path measured ~6 ms on a single call — cheap by comparison, but still not
free, and still not needed if you hold the pointer.

A 115 ms freeze on the game thread is invisible on a menu and brutal in combat: it only *feels* like a
stutter when the frame budget is already tight, which is exactly when a fight is happening.

### What it buys you

Sparking ZERO's end state, measured: **6 scans in an entire session, all inside the first 90 seconds
of boot, zero during a full match.** Four versions earlier the same session cost 46.

More importantly, the concepts disappeared. Widget discovery, scan cadence, backoff, and
"battle dormancy" were all deleted — every one of them existed only to ration scans that no longer
happen.

---

## 2. The decision ladder

Work down it and **stop at the first rung that answers your question**. Each rung is cheaper and more
robust than the one below it.

**0. Dump the C++ headers before writing any code.**
Sparking ZERO's dump is ~1859 native classes in one file. It is the authority on *what exists*; live
inspection is not. Half an hour here saves weeks. See
[`ue4ss-discovery-tools.md`](ue4ss-discovery-tools.md).

**1. Does the game publish its own UI state?**
Search the dump for a focus/selection member on the menu manager. Sparking ZERO has
`LastFocusedWidget` on its menu-manager class, plus a `Buttons` array for "option 3 of 7" and
`Text` / `CurrentItem` for labels. If that exists, **your design is finished**: persistent root →
named field → zero cost. Everything else in this document is a workaround for not having it.

**2. Is the UI driven from Blueprint?**
If yes, `RegisterHook` is even better than polling: no tick at all. Test this early and cheaply,
because the answer reshapes the whole mod. **Both our games answered no** — see §4.

**3. Acquire actors through a `BeginPlay` hook, not through scans.**
`RegisterBeginPlayPostHook` fires when each actor has finished `BeginPlay` — fully initialised, unlike
construction notifications. Match it against your table of wanted classes by walking the super chain,
and **memoise the match by concrete class name** so it runs once per class rather than once per actor.
Sparking ZERO gets zero-scan, zero-latency acquisition this way.

This solves something no budget can solve: **"the object does not exist yet."** That install takes
~90 s to build its main menu. No amount of retrying covers a 90-second wait; only an engine
notification does.

**4. Build a directory of persistent roots for whatever rung 1 missed.**
Map classes to named fields reachable from objects that never die — PlayerController, its HUD, the
GameInstance, a scene controller. Kakarot's `ui_directory.lua` does exactly this for ~40 classes, and
its own STATUS calls it the *real* fix for the slowness, with budgeted scanning demoted to fallback.

**5. Only then scan — budgeted.**
Per-tick cap, backoff for classes that come back empty, and a **per-type** allowance (§5). Treat a
scan as a scarce resource you spend deliberately.

**6. If it isn't reflected at all, read memory natively.**
Decompiler for the offset, runtime diff to confirm, a small native bridge to read it. This is the last
rung, but it is legitimate, and sometimes it is the only one — see §6.

---

## 3. Rules with evidence

Each of these cost a real session to learn.

**Never spend a scan testing whether something is PRESENT.**
Violated three times on Sparking ZERO; all three times it silenced the reader. In one session **26 of
44 scans** went to a loading-screen widget that usually did not exist — failing every time, draining
the budget the objects that *did* exist needed. Acquisition is for things reliably present, never for
asking whether something exists.

**A global scan budget also needs a per-type cap.**
With only a global budget, whoever asks first takes everything: **94 of 94 scans in one session went
to a single class**, and the owner listed after it was never acquired once. Adding "priority" just
moves the starvation down one level.

**Actors are safe to cache across ticks; widgets are not.**
Widgets are destroyed and rebuilt constantly (and pooled — see
[`accessibility-patterns.md`](accessibility-patterns.md)). Cache the actor that owns the scene, read
fresh widgets from it every time. That read is free.

**`IsValid()` is not a type check.**
It passes on freed memory that has been recycled, so a rotten pointer can look healthy and abort the
process on the first method call. Every hop into a not-guaranteed type must declare the class it
expects — a mismatch is a skipped read, not a dead game. Kakarot goes further and pre-checks the
memory through its own native bridge before touching the object at all, because `IsValid` itself can
abort. That is the stronger version.

**`pcall` is not a safety net.**
UE4SS binding errors and native aborts are not caught by it. The protection is not wrapping the call;
it is **not making the call**.

**Crash exposure scales with objects touched per tick.**
Resolve expensive things (subtree walks) only for the reading that will actually be spoken.

**Before concluding a lookup is broken, prove the object existed at that moment.**
Two Sparking ZERO theories died this way — including a wrong accusation against the object-array
cache setting. Both times the code was fine and the *timing* of the question was wrong.

**An unset `FName` stringifies to the literal `"None"`.**
It is a non-blank string, so it sails through any "is there text?" check and gets spoken. Reject the
sentinel explicitly.

**A diagnostic that carries its own copy of the model will lie to you.**
A probe with a hand-written object list drifted from the reader it was meant to inspect, and a full
test round was spent believing screens were broken when the probe was. A probe must consume the same
table as the code under test. Corollary: **log what you are about to do**, not what you finished, so
the last line in the log is the thing that killed you rather than the last thing that survived.

---

## 4. What both games rejected, and why

Convergent evidence from two engines. Don't re-derive these.

**`NotifyOnNewObject` — dropped entirely, for two *independent* reasons.**
- *Sparking ZERO:* it fires by inheritance, so registering on a base class (`UserWidget`, `TextBlock`)
  runs for **every** widget — hundreds constructed in a burst when a menu opens. Touching them
  mid-construction aborts natively; `pcall` cannot catch it.
- *Kakarot:* measured that the callback also arrives **on UE4SS's async loading thread** (thread 5744
  vs 38620 for the game thread). Running Lua there corrupts the shared `lua_State` and throws an
  uncatchable C++ exception.

Two different root causes, one final rule: **no construction notifications**. The narrow-registration
variant (a handful of low-volume classes) remains defensible on other games, but neither of ours
needed it. Case study in [`accessibility-patterns.md`](accessibility-patterns.md) §9.

**UFunction hooks — registered fine, never fired.**
`RegisterHook` detours the UFunction *dispatch* path, so it only fires when the call originates in
Blueprint; C++→C++ calls bypass it entirely. Sparking ZERO installed 6 UMG hooks and observed **zero**
firings while navigating menus. Kakarot tried 5 functions with the same outcome and now ships with
hook installation deliberately disabled. **This is not an argument against hooks** — it is an argument
for testing rung 2 early. Where the UI *is* Blueprint-driven, hooks beat everything else here.

**Map-load hooks — crash both games.**
`RegisterLoadMapPre/PostHook` kills the process (on Kakarot apparently via the Steam packaging leaving
the trampoline misplaced). Use Kakarot's replacement: poll a **world epoch** — compare the address of
the current world, obtained from a persistent root, once per tick; when it changes, flush every cache.
No hooks, no risk. Sparking ZERO uses an `InitGameState` hook for the same purpose.

---

## 5. When the ideal architecture is not available

Sparking ZERO could delete scanning outright because its menu manager publishes `LastFocusedWidget`.
Kakarot cannot, and the reason is structural, not effort: its selection index is **not a `UPROPERTY`**.
The header dump shows tail gaps after the last reflected member — the index is private C++ state.
Reflection cannot reach it by definition, and no directory of persistent roots ever will.

So the honest ladder ends at rung 6: decompiler for the offset, runtime diff to confirm it, native
bridge to read it. Kakarot's own docs put it bluntly — once the decompiler setup is paid for, the old
"reflection first because native is expensive" reflex is stale. Chasing a reflection that does not
exist costs more than reading the offset.

**Design consequence:** find out *early* which of your screens are reflected and which are not. The
answer decides whether you are writing a Lua mod or a Lua mod with a native memory bridge, and that is
not a decision you want to discover halfway through. Keep the bridges separate — speech bridge speaks,
memory bridge reads, no mod logic inside either.

---

## 6. The two mods side by side

| | Sparking ZERO | Kakarot |
|---|---|---|
| Scans in steady state | **zero** | budgeted: 2/tick, with backoff |
| Focus/selection source | reflected `LastFocusedWidget` | not reflected → native memory read |
| Acquisition | `BeginPlay` feed (zero scans, zero latency) | scans + persistent-root directory |
| Level transitions | `InitGameState` hook | world-epoch poll |
| Poll loops | **one**, 100 ms | five: UI 100 ms, pad 20 ms, radar 100 ms, battle 250 ms, quests 300 ms |
| Per-screen adapters | none — generic read over a manager table | ~35 `screen_*.lua` |
| Size | small | 74 Lua files, ~22 370 lines |

**Read the size row fairly.** Kakarot is an open-world game and its mod does things the other never
has to: navigation radar, minimap, quests — the navigation tracker alone is ~4000 lines. Much of that
difference is *scope*, not architecture. The rows that are genuinely architectural are the first four.

**Actionable in both directions:**
- *Kakarot could adopt:* `RegisterBeginPlayPostHook` (untested there — the ini ships with BeginPlay
  hooking off, and it is what took the other mod to zero scans; validate it in isolation first, since
  that game's map-load hooks do crash), and a wider persistent-root directory, which its own STATUS
  already names as the real fix.
- *Sparking ZERO could adopt:* Kakarot's memory pre-check before touching an object (stronger than a
  class check, because `IsValid` can abort), its world-epoch poll, and its dual-bridge separation.

Game-specific companions (in the Kakarot repo, not the playbook):
`reference/dbz-kakarot/notes/dbz-kakarot-perf-architecture.md`, plus each mod's `STATUS.md`
"derived facts" table.

---

## 7. Checklist for a new UE4SS mod

1. Dump the C++ headers. Read them before writing code.
2. Grep the dump for a published focus/selection member. If it's there, you're done designing.
3. Test whether the UI dispatches through Blueprint (register one hook, navigate, check the log).
4. Wire acquisition to `BeginPlay`, not to scans.
5. Build the persistent-root directory; scan only for what it can't reach, with per-tick **and**
   per-type budgets.
6. Class-verify every pointer hop. Cache actors, never widgets.
7. Decide early which screens reflection cannot reach, and budget for a native bridge for those.
8. Keep one regression test that needs no instrumentation: **count `walk:` / scan lines per session.**

**Never, on any Unreal game:** construction notifications, following a pointer without verifying its
class, trusting `pcall` as a safety net, or spending a scan to ask whether something exists.
