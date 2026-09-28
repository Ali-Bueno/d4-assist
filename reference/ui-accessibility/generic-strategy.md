# Generic UI strategy — coverage by default, screens as exceptions

> Read this **before** writing any menu/inventory code. It's the architecture the concrete behavior
> specs ([menus](menus.md), [inventories](inventories.md)) plug into. Trigger phrases:
> **"accessibilize menus"** / **"accesibilizar menús"** and **"accessibilize the inventory"** /
> **"accesibilizar inventario"** both apply this strategy first, then the concrete spec.

> **See also:** [graph-navigator/](graph-navigator/) — a third-party spec (WH40KRTAccess, MIT) built on
> the *opposite* axiom: the mod owns a parallel UI tree instead of riding the game's focus layer. Use it
> when the native focus model fails for a blind player; its folder README compares both approaches.

## The problem

A game has dozens of screens (main menu, options with tabs, save/load, inventory, crafting, shop,
dialogue, map…). Writing bespoke accessibility code per screen is (a) huge, (b) duplicated — most
screens are the same handful of controls rearranged, and (c) fragile — a game update that rebuilds a
panel silently breaks a hardcoded reader.

The failure mode to design against: **keying behavior on screen identity.** If the mod decides
*whether* to read by asking "which screen is this?" (scene name, panel class, registered list), then
every screen not on the list is mute — including screens that are pixel-for-pixel the same widgets as a
covered one, just with different text. That turns the mod into an endless per-screen chore and makes
every game update a coverage regression.

## Why this matters: 100% readable menus in one pass, not screen by screen

The universal goal, whatever the game: menus must end up **100% readable** (all the info a sighted
player gets), reached **as fast as possible** — when a shared pattern exists across the game's menus,
implement it **once for all of them at the same time**, in a way that doesn't break when one screen
differs or changes. How much of the mod this phase represents depends on the genre: in an action or
exploration game, menus are the gate you pass to focus on gameplay; in a point-and-click or visual
novel, the UI *is* the gameplay, so this same architecture simply carries more of the mod. Either way
the approach is identical — this is what makes it a **fixed cost** instead of an open-ended per-screen
chore. The workflow:

1. **Investigate the game's UI code, then build the pipeline once**: find the shared focus/widget
   layer in the game's own code first ([Step 0](#step-0--find-the-games-own-menu-system-in-its-code) —
   ILSpy for Unity, dumps for IL2CPP/Unreal, Ghidra for native), then wire global focus hook → widget
   classification → the archetype readers from [menus.md](menus.md)/[inventories.md](inventories.md) →
   tree-walk fallback. This is the only real menu work in the mod.
2. **Validate on 2–3 representative screens** (e.g. main menu, options-with-tabs, one dialog). If those
   read correctly, every other screen built from the same widgets already works — do *not* walk every
   screen re-implementing the same pattern; that pattern already ran everywhere via classification.
3. **Sweep the critical path**: quickly navigate the screens the player actually needs — in most games
   that's main menu → new/load game → options → pause → inventory; in UI-driven genres (point-and-click,
   visual novel) it extends to the interaction screens themselves. Anything that reads via fallback or
   reads incompletely gets an archetype mapping (preferred) or a thin quirk override — minutes each,
   because the readers already exist.
4. **Definition of done**: every critical-path screen reads name + state + tooltip per the specs, with
   nothing mute — 100% of the on-screen info, in on-screen order. Then move on to the rest of the mod
   and stop touching menus. Remaining unmapped screens keep working through the fallback; fix them only
   if a real playthrough reports one as insufficient.

If working on menus ever feels like re-implementing the same logic for another screen, stop — that's
the signal a mapping or reader is missing at the shared layer, and fixing it there is what keeps this
phase short.

## The core rule: classify widgets, never screens

The mod never asks *"which screen is this?"* to decide **whether or how** to read. It asks *"what kind
of widget just got focus / just changed?"* and delegates to that archetype's reader. Screen identity is
used only to **add** context (announcing the screen title on entry, naming a subsection) or to
**override** a genuine quirk — never as a gate.

Consequences:

- Two screens built from the same widgets with different text are **automatically the same code path**.
  Zero per-screen work; they were never two integrations to begin with.
- A screen nobody ever looked at is readable on day one, because its buttons/sliders/lists are the same
  archetypes as everywhere else.
- A game update that renames or rebuilds a panel doesn't silence it: classification is by widget base
  class/component, which survives layout changes far better than screen names do.

## Resolution order (for every focus/change event)

Every normalized event goes through the same pipeline, best match first — and **no step ends in
silence**:

1. **Per-screen override** — *only if one is registered for this screen AND it has a quirk handler for
   this event.* Rare by design (see below).
2. **Widget archetype reader** — the default path. Classify the widget (base class, component,
   interface) → delegate to the one reader for that archetype. This should handle ~95% of all events in
   the game.
3. **Generic tree-walk fallback** — the widget didn't classify. Walk it (or the visual tree under
   focus) and read whatever text/label/value components it exposes, in on-screen order. Partial,
   imperfect coverage beats a mute screen, and the log line it emits tells you which archetype mapping
   is missing.

An "unknown screen" is **not a case** in this pipeline — there is no such thing. Only unknown
*widgets*, which still fall through to step 3.

## Two layers, decoupled

```
                 ┌─────────────────────────────────────────────┐
   engine  ──►   │  Adapter: normalize events + classify widget │   "WHAT changed, WHAT kind"
   events        │  (+ optional thin per-screen overrides)      │
                 └──────────────────┬──────────────────────────┘
                                    │ normalized focus/change event + archetype
                 ┌──────────────────▼──────────────────────────┐
                 │  Widget archetype readers (reusable)         │   "HOW each control sounds"
                 │  Button · List item · TabBar · Slider ·      │
                 │  Checkbox · Dropdown · Grid cell · Dialog …  │
                 └─────────────────────────────────────────────┘
```

- The **bottom layer** never knows which screen it's on. It's a fixed set of readers, one per
  archetype, each implementing the rules already written in [menus.md](menus.md) /
  [inventories.md](inventories.md). Written once, reused everywhere.
- The **top layer** classifies each focused/changed control into an archetype and delegates. No spoken
  text lives here. Per-screen code exists only as optional thin overrides (see below).

This is the [engine-adapter principle](../../CLAUDE.md#6-code-architecture-and-best-practices) applied
to UI: accessibility logic is written against normalized archetypes, not against concrete engine widget
classes and not against concrete screens.

## Step 0 — Find the game's own menu system in its code

The whole strategy depends on locating the **single shared layer the game's own UI passes through**
(its focus manager, base widget class, or navigation component). That is found by **investigating the
game's code first** — never by trial-and-error per screen. Identify the engine
([CLAUDE.md §2](../../CLAUDE.md#2-investigate-the-engine-before-assuming-anything)), then use the
matching route:

- **Unity (Mono)** — the easy case: open `Assembly-CSharp.dll` in ILSpy and read the UI code directly.
  Look for the custom menu/widget base classes, who drives selection (Unity's `EventSystem` vs. a
  game-own "current index" manager), and where labels/values/tooltips live on the widget.
- **Unity (IL2CPP)** — same questions, but dump names first (Il2CppDumper / cpp2il over
  `GameAssembly.dll` + `global-metadata.dat`), then read the recovered API. Use proper IL2CPP
  reflection when hooking (wrong names crash).
- **Unreal Engine** — dump the UMG reflection data (UE4SS object dumper or equivalent): find the
  `UUserWidget` subclasses, the common button/list widget blueprints, and the focus/navigation path
  (Slate focus, `SetKeyboardFocus`, gamepad nav config). The shared base widget classes are the
  classification targets.
- **Native (C/C++/VB6…)** — Ghidra (with RTTI analyzer + demangler enabled so vtables/classes get real
  names). Practical entry points for menu behavior: search the binary's **menu label strings** and
  follow xrefs to the code that draws/uses them; find the **"current selection" variable** by looking
  for small integers written on up/down input and used to index those strings; the function that
  writes it is the game's navigation handler — your hook point. One state machine usually drives every
  menu in the game, which is exactly the shared layer this strategy needs.

What Step 0 must produce (the deliverables the rest of the steps consume):

1. The **focus/selection signal** — the one method/variable that changes when the user navigates.
2. The **widget type inventory** — the concrete classes/structures to map onto archetypes in Step 1.
3. **Where the text lives** — how to get name/state/value/tooltip from a focused widget.

If the game turns out to have several unrelated UI systems (e.g. HUD vs. menus, or an in-house system
plus engine UI), that's fine — it's *N* shared layers instead of one, each hooked once. It's still not
per-screen work.

## Step 1 — Normalize the game's widgets into archetypes

Map every concrete UI class the game uses onto **one** normalized archetype. Do the classification in
the adapter (by base class, component, prefab name, or interface), never by branching inside a reader.

| Archetype | Matches (examples) | Canonical read behavior |
|---|---|---|
| **Label / static text** | title, header, tooltip, body text | **Never read on its own.** Static text reaches the player only three ways: the screen **title**, spoken once on every menu change (before the focused item); a widget's **tooltip/description**, spoken as the last part of that widget's readout when it gets focus; and the **body of a full-text screen** (loading tip, help, tutorial, popup message), read in full when it appears ([menus.md](menus.md#full-text-screens-the-only-exception)). Any other label is not spoken. Enumerating labels is what makes a mod read the whole menu |
| **Button / action item** | menu entry, "Play", "Back", list row | name + tooltip on focus |
| **List / vertical menu item** | options list, save slots | name + state + tooltip ([menus.md vertical nav](menus.md#vertical-navigation-up--down)) |
| **Tab bar / section** | Video/Audio/Dialogue tabs | tab name only on switch ([menus.md tabs](menus.md#tabs--sections)) |
| **Slider** | volume, brightness, numeric | only the new value on change |
| **Checkbox / toggle** | on/off setting | only the new state on change |
| **Dropdown / selector** | language, resolution, cycler | only the value you land on |
| **Grid cell / slot** | inventory, hotbar, equipment, shop | item name + stats ([inventories.md](inventories.md)) |
| **Dialog / popup** | confirm, message box | message then focused button ([menus.md dialogs](menus.md#popup-and-confirmation-dialogs)) |
| **Stat / progress bar** | HP, XP, load %, cooldown | label + value/ratio when it changes |
| **Text input** | search box, rename field | label on focus, echo committed value |

If a control doesn't fit, **add an archetype** (once) rather than special-casing a screen. Two screens
that share a base widget class / prefab / navigation component already share a reader; don't write a
second one.

## Step 2 — Subscribe at the highest shared layer, not per screen

Hook events **once**, as high in the UI stack as the engine allows, so one subscription covers every
screen — present and future:

1. **Global focus/selection system** — the primary signal, and usually game-wide (Unity: the
   EventSystem's selection; native: the engine's single "current index"/focus manager). One hook here
   covers *all* screens' navigation at once.
2. **Screen opened / closed** — often also observable globally (panel activation, scene/state change).
   On open announce the screen title **followed by the focused widget only** (`interrupt=true`, one
   utterance — see [menus.md — entering/returning to a menu](menus.md#entering-leaving-or-returning-to-a-menu--screen)); never the whole
   screen. On close, the screen that regains focus is announced **the same way** (title + focused
   widget): returning to a parent menu is a menu change too. The title is just data read off the
   screen — it needs no per-screen registration.
   **Beware overlays**: many games open a submenu as an overlay *without* deactivating the parent (and
   close it without any screen-change signal), so open/close events alone are not a reliable source of
   "which menu is active". Keep a **menu stack** and resolve the active layer by **focus/input
   ownership**, per [menus.md — submenus, overlays and the menu stack](menus.md#submenus-overlays-and-the-menu-stack);
   ignore focus events bubbling from layers underneath the topmost one.
3. **UI updated / rebuilt / data-bound** — when a panel refreshes (list repopulated, value pushed from
   game logic, layout swapped by an update), re-read the currently focused widget so it never goes
   stale. Diff-gate so a rebuild that didn't change the focused content stays silent.

Only when a screen's events are **not** reachable from a shared hook (a custom controller that bypasses
the common focus system) does it get its own subscription — and that subscription still feeds the same
normalized pipeline. If the engine gives no usable event at all, poll the focused-widget state at a
modest cadence and **diff-gate**: announce only on real change, never per frame. Fallback, not norm.

## Step 3 — Central dispatcher (kill duplication)

One dispatcher routes every normalized event through the [resolution order](#resolution-order-for-every-focuschange-event)
to the right archetype reader. Consequences:

- A new screen appearing in the game needs **no code** in the common case — its widgets already
  classify.
- A reader fix (e.g. how sliders announce) lands **once** and every screen benefits.
- Spoken strings stay in the single centralized, translatable string layer — never inline in an
  adapter or override.

## Step 4 — Per-screen overrides: the exception, and only ever additive

A per-screen override is justified **only** for genuine quirks the generic pipeline can't infer:

- A widget whose on-screen reading order differs from its tree order.
- Screen-specific context worth announcing (e.g. "craftable" logic that lives outside the widget).
- A control that misclassifies and needs a forced archetype.

Rules for overrides:

- They **add or replace behavior for specific events**; everything they don't handle falls through to
  the default pipeline. An override never disables the generic path for the rest of its screen.
- Before writing one, check whether the "quirk" is actually a shared base class / prefab / navigation
  component also used elsewhere — if two screens would need the same override, it belongs in the
  archetype reader or the classifier, not in two overrides.
- Rule of thumb: **if you're about to write per-screen reading code, you're usually one missing
  archetype mapping away from not needing it.**

## Step 5 — Fault isolation: one broken screen must never break the mod

Screens change across game updates; that must degrade coverage **locally**, never globally:

- Wrap each per-screen override and each archetype reader invocation so an exception is caught at the
  dispatcher: **log once** (which screen/widget/reader failed) and **fall through to the next step of
  the resolution order** — a broken override degrades to the archetype reader; a broken classification
  degrades to the tree-walk fallback. The user hears *something* and the log tells you what to fix.
- Never cache widget/screen references across rebuilds without revalidating them (stale references are
  the #1 post-update crash). The event-driven re-read in Step 2.3 is what keeps this safe.
- A classification miss is **data** (one widget reads generically, log line emitted), a thrown
  exception is **contained** (that event falls back), and neither ever silences other screens.

## Anti-duplication / robustness checklist

- [ ] Step 0 was done first: the shared focus/selection layer and the widget type inventory were found
      by reading the game's code (ILSpy / IL2CPP dump / UE dump / Ghidra), not guessed per screen.
- [ ] The mod never gates reading on screen identity — no "is this screen registered?" check anywhere
      in the read path. Unknown screens don't exist; only unknown widgets (which still read via
      fallback).
- [ ] Every concrete widget class is mapped to an archetype in the adapter, not branched on inside a
      reader.
- [ ] Focus/update subscriptions hook the highest shared layer (global focus system) — one hook for
      all screens; per-screen subscriptions only where a screen bypasses it.
- [ ] Per-screen overrides are additive quirk-handlers only; everything else falls through to the
      generic pipeline. No spoken text and no duplicated read logic in overrides.
- [ ] Exceptions in overrides/readers are caught at the dispatcher and degrade down the resolution
      order — one broken screen never crashes or silences the mod.
- [ ] UI rebuild/update events re-read focus (diff-gated), so dynamic changes never leave stale state
      or stale references.
- [ ] Unclassified widgets hit the generic tree-walk fallback and emit a log line naming the missing
      mapping.
- [ ] Every archetype reader obeys the concrete rules in [menus.md](menus.md) /
      [inventories.md](inventories.md) (context change → `interrupt=true`; incremental → only what
      changed).
