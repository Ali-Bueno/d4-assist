# The Graph A11y Kernel — an engine-neutral specification

**Status**: Draft 2 (2026-08-09). Draft 1 (2026-07-17) was distilled from the two source
implementations. Draft 2 incorporates a five-implementation conformance audit: every kernel rule
and host policy was checked against each shipping port, and the text now records what the
implementations actually agree on, which Draft 1 claims were aspirational, and what three new
hosts (two of them C++) needed that Draft 1 never mentioned.

**Implementations** (audit corpus):

| Project | Game | Stack | Kernel | Status |
|---|---|---|---|---|
| **RTAccess** (reference) | WH40K: Rogue Trader | C# / Unity Mono / UMM | `src/Access.Core/Graph/`, 1,623 lines, BCL-only | shipping, 62 screens |
| **WrathAccess** (upstream) | Pathfinder: WotR | C# / Unity Mono / Owlcat native | shared lineage, see Appendix A | shipping, ~30k LOC |
| **CyberAccess** | Cyberpunk 2077 | C++ / RED4ext | 2,362 lines, 92 tests | shipping, 36 screen registrations |
| **Stellaris port** | Stellaris | C++ / DLL injection, closed engine, no modding API | 2,061 lines | in progress, 10 kernel screens |
| **Sunless Sea port** | Sunless Sea | C# / BepInEx / net35 | verbatim copy, 8-line delta | in progress, 1 screen |

Lineage: Factorio Access `key-graph.lua` / `menu.lua` → Tanglebeep (ported with permission) →
WrathAccess → RTAccess → the C++ and net35 ports.

**Conformance suite**: 50 behavioral tests over the kernel alone, identical across the C# ports
and transcribed in the C++ ones. The C# test projects compile `Graph/**` standalone, which is what
mechanically enforces the kernel/host boundary.

---

## 0. What this is

A specification for building a **screen-reader accessibility layer inside a video game** (or any
application whose native UI toolkit cannot be made accessible) by constructing a **mod-owned
parallel UI tree** over the application's live state. It generalizes the architecture shipped in
five production mods across three engine classes (managed Unity, reflection-loader native, closed
native) so it can be re-implemented in any language, for any engine, over any GUI library.

The spec has four layers:

1. **The kernel** (§3–§6) — pure data structures and algorithms, no engine dependency. Port this
   verbatim. Appendix A lists per-project extensions that are NOT part of the canonical kernel.
2. **The host ports** (§2) — the capabilities the kernel needs from a game. Implement these per
   game.
3. **The policies** (§7–§9) — input mapping, speech, screen lifecycle: normative behavior for the
   navigator you build around the kernel.
4. **The porting guide** (§10) — costs, ecosystem notes, and the migration protocol for arriving
   beside an existing legacy layer.

Keywords MUST / SHOULD / MAY are used in the RFC-2119 sense. Where Draft 2 marks a rule with ⚠,
the source implementations deviate from it and the deviation is a known defect to fix in code, not
a license to copy.

---

## 1. Design axioms

These are the load-bearing decisions. Violating one produces a category of bug that the source
projects hit and had to engineer back out.

### A1. Parallel tree, not the game's focus system

The accessibility layer OWNS its navigation model. It MUST NOT ride the application's native focus
ring, gamepad navigation, or hover system. Native focus systems are built for sighted spatial
scanning; they skip non-interactive text, follow visual layout rather than reading order, and
change semantics per screen. The parallel tree reads the game's state and presents its own
traversal.

*Corollaries*: the game's own UI stays live underneath, so every input path into it must be
accounted for (§8) — and where the game's own verbs act on *its* selection, the layer must write
its cursor back into the game (P11).

### A2. Immediate mode — the tree is rebuilt, never mutated

Every screen declares its nodes **fresh from live application state on every render**. Node
contents hold **no view state**: activating a node calls the game's own handler, and a node's
spoken parts read the game's state (a part MAY be a build-time snapshot, since the tree is rebuilt
per operation — but a part marked `Live` MUST be a closure that re-reads state on every call, or
the live watch can never see it change). The only state that survives a render is the cursor
(§3.6).

This kills the universal failure mode of retained-mode accessibility layers: cache invalidation
against a UI you don't own. There is nothing to invalidate. Field experience: against a native
engine that recreates widgets at will and hands you raw pointers, rebuild-and-reconcile is the
*only* cache policy that cannot go stale — the axiom fits closed engines even better than managed
ones.

Two cost rules the axiom implies:

- **Operations MUST rebuild** (§5) — a keypress may trigger several rebuilds (arrow → adjust
  check → move → tree ops), so a screen's `Build` must be cheap enough to run several times per
  keypress. State this budget to everyone writing recipes.
- **The idle cadence MAY be decoupled.** Where per-frame Build is measurably expensive (one C++
  port found it the single largest per-frame cost while a menu was attached), the differ MAY run
  Build on key-operation frames plus a fixed idle cadence (~100 ms shipped). The differ MUST only
  compare after a rebuild+reconcile; frames that skipped the rebuild MUST NOT announce or advance
  the live watch. The cost is bounded staleness of game-driven focus changes.

### A3. Focus persists by identity, not by reference to the tree

Renders are throwaway; focus is reconciled into each new render by a **two-tier identity** (§3.1):
the backing domain object (follows a thing that *moved*), else a structural key (follows a logical
control whose backing object was *rebuilt*), else the nearest survivor in the previous traversal
order. Focus never silently jumps to the top of a screen because the content re-rendered.

### A4. Announce exactly once, from one place

A focus change is spoken **exactly once, no matter what caused it** — a keypress, the screen
moving focus, a content rebuild, the game yanking a VM. This is achieved by a **frame differ**
(§7.3): one code path compares the identity last spoken against the identity now focused and
speaks the delta. The comparison is by **structural key** (§7.3 defines this precisely).
Hand-written announce calls around focus mutations are FORBIDDEN — every one is a future
double-speak or missed-speak.

### A5. Read the game's state; drive the game's handlers

Actions MUST invoke the application's own method/handler for the equivalent UI interaction — even
when that spawns a dialog you then have to make accessible. Never reimplement an application flow
from primitives. Labels MUST mirror what the application visually shows on the control ("label
mirrors the card"); detail that is visually tooltip-only stays behind the tooltip verb. Driving
the application's own handler frequently brings its sound, animation, and observer pipeline for
free — which is why P6 (sound) so often turns out to be a no-op port.

### A6. Parity — never reveal what a sighted user can't currently see

Fog of war, hidden units, undiscovered content: the layer MUST gate its readouts on the same
visibility rules the sighted presentation uses. Convenience reveals are cheating, and they corrupt
the shared vocabulary between blind and sighted players of the same game.

### A7. Interrupt speech by provenance, not timing

**Default policy (SHOULD)**: speech caused by the user's own keypress interrupts what's playing;
passive/event speech and automatic focus changes queue. A keypress response never clips; background
narration never cuts off what's playing. The differ path queues; the direct input paths interrupt
(§7.3).

A host MAY instead adopt **all-queue** as a documented, deliberate policy (one C++ port did).
Understand the cost before choosing it: arrowing quickly through a long list builds a backlog of
queued readouts with no way to skip ahead. Do not choose all-queue on the folk theory that "the
screen reader already interrupts on keypress" — NVDA's keypress interrupt applies to its own focus
events, not to text arriving from an out-of-process controller client, and keys your layer consumes
never reach the screen reader at all. Whatever the choice, it MUST be one policy point in the
navigator, not per-callsite judgment.

### A8. One vocabulary module for everything the layer says

All layer-authored strings (role words, "n of m", "expanded", "no tooltip", screen names) MUST
route through a single vocabulary module — hardcoded English at call sites is forbidden, because
it is unfindable later. Backing that module with a localization table (P7) SHOULD ship but MAY be
deferred; the module is what makes deferral safe. Application content (names, log lines) is
already localized — pass it through, never re-translate. Trap: a host's own localization system
is often *closed* to layer-authored keys (hash lookups into game-shipped data), so P7 usually
cannot be "reuse the host's loc" — plan a self-contained table.

---

## 2. Host ports

The kernel is pure; everything engine-specific enters through the ports. P1–P5 and P10 are
required on every host; P11 is required on most (see its entry); the rest are optional but
field-proven. A game is a viable host if you can implement the required set (checklist in §10.2).

### Required

- **P1 — State read.** Read arbitrary application state on demand, cheaply enough to call from
  every operation (UI view-models, widget trees, entity lists, text). In managed engines this is
  reflection/direct field access; in native engines, a disassembler-derived offset table or a
  scripting API. Three field notes Draft 1 omitted: (a) the state you read may be **mutated from
  other threads** — native hosts needed bounded try-locks, and a skipped read is indistinguishable
  from "absent"; (b) reading "text" often means running the game's own **localization lookup** as
  a read primitive (widgets carry loc keys, not display strings), including re-reading the active
  language on every call; (c) if P1 is expensive, the rebuild cadence carve-out in A2 is your
  relief valve.
- **P2 — Action invoke.** Call the application's own UI handlers (click/submit/toggle
  equivalents). Field note: handler invocation can be **call-site-sensitive** — one closed engine
  throws C++ exceptions as normal control flow during view transitions, caught only inside its own
  dispatch frame, so *where you call from* is part of the contract and may force a permanent
  split input architecture. Verify each handler from the exact context you'll ship.
- **P3 — Tick.** A per-frame (or high-frequency) callback on a thread from which P1/P2 are legal.
  Field notes: the only tick you can get may be the application's *input handler* rather than a
  render tick — which is fine, and running everything at input cadence on the engine's one input
  thread is what lets the whole layer be lock-free. Ensure the tick also fires on input-less
  frames (one port had to add that explicitly).
- **P4 — Key input.** Raw key/chord state independent of the application's input consumption,
  plus a way to **arbitrate**: suppress the application's own handling of keys the layer claims
  (§8 — the right chokepoint is host-specific). Field notes: the key path may be on a different
  thread than P3 (queue with a bounded, drop-oldest buffer — replaying seconds of stale input
  presses phantom keys); the host's input may be **edge-triggered** (one event per press), in
  which case the navigator must synthesize its own key repeat (~350 ms delay / 50 ms interval
  shipped) from polled state; and if the host's keymap is modifier-blind, claims must be
  per-(key, modifier-mask) with a release-swallow latch so a swallowed key-down never delivers an
  unpaired key-up.
- **P5 — Speech.** `Speak(text, interrupt)` to a screen reader or TTS, where `interrupt=true`
  cuts current speech and `false` queues. (Windows: NVDA controller client / SAPI, via a
  Tolk-style bridge, SRAL, or a hand-bound native library.) The one-liner hides a real contract:
  backends have **payload ceilings** (one port crashed a screen reader with a ~1 KB Cyrillic
  payload and now chunks at 700 bytes on sentence → whitespace → codepoint boundaries — and only
  the first chunk can honour `interrupt`); newlines may need flattening; backends may not be
  thread-safe (pin calls to one thread); and a screen-reader client DLL may resolve against the
  host process's directory under injection (pre-load by full path).
- **P10 — Text normalization.** Every string the layer speaks — game content and its own — passes
  through one cleaning chokepoint: strip the host's rich-text/markup (both source markup and any
  compiled control bytes; some screen readers silently eat control characters and leave their
  payloads audible as stray ASCII), rewrite internal reference syntax to display text, flatten
  line structure, collapse whitespace, and handle the host's string encoding (SBO/heap layouts,
  UTF-8 vs UTF-16). Draft 1 mentioned markup stripping only for search text; that was backwards —
  the readout needs it first. Every port built this; none of them planned to.

### Conditionally required

- **P11 — Focus write-back.** Write the layer's cursor into the application's own
  selection/hover, at every focus change. **Required wherever the application's own verbs act on
  its own pointer** — otherwise a game-key verb fires at whatever the game's selection happens to
  be, not at what the user is hearing (one port: "a verb fired at a random perk"). This was needed
  in three of the five hosts (69 call sites in one) and doubles as the hook where the engine's
  own hover transition — sprite swap, observer notify, hover *sound* — is driven. Draft 1 listed
  only read (P1) and invoke (P2); this is a distinct capability.

### Optional

- **P6 — Sound.** Draft 1 said "play the application's own hover/click sounds at the navigator
  chokepoints". Field result: on hosts with P11, sound falls out of driving the application's own
  hover/click transition and P6 is not a separate port at all; the kernel-level per-node sound
  slots shipped in one project only and are dead in both C++ ports (Appendix A). Prefer the
  transition-drive framing; add explicit sound plumbing only where the host has no drivable
  transition.
- **P7 — Localization.** The string table behind the A8 vocabulary module. See A8 for the
  closed-host trap.
- **P8 — Settings.** Persisted user preferences; the announcer consults a per-control-type,
  per-part-kind verbosity filter (§6.1). Confirmed genuinely optional — three ports ship without
  it.
- **P9 — Logging.** A speech transcript and focus trace are the single most valuable debugging
  artifacts this architecture has (confirmed by every port; one writes the transcript *inside* the
  speech wrapper so it records exactly what the TTS received, per chunk). Strongly recommended.
- **P12 — Input-binding introspection.** Resolve the host's action ids to the player's *current*
  physical keys. Needed wherever extracted UI text embeds key-glyph references ("press
  `<VisionHold>`") — speaking the raw action id is useless. Any game with rebindable keys and
  glyph hints has this; one port generates a 665-line keymap bridge for it.

---

## 3. Kernel data model

Names below are from the reference implementation; ports may rename, but the semantics are
normative. The kernel MUST carry engine-neutral comments — one verbatim copy shipped
reference-game type names and references to deleted subsystems into a foreign codebase.

### 3.1 ControlId — two-tier identity

The identity of a control, designed so focus can be followed across rebuilds even when the world
shifts.

- `Reference` (optional): the backing object the node was derived from. Compared by **reference
  identity** (pointer equality). Prefer the **longest-lived object behind the control** — a domain
  entity (the item, the ability, the save slot) over a view-model that the UI rebuilds; hosts with
  no VM layer at all bind domain objects here and get tier-1 recovery free.
- `StructuralKey` (required): a **value-equatable** key — a string, or a composite such as
  `(pane, row, col)`.

Rules:

- Equality and hashing are defined on `StructuralKey` **alone**, so a ControlId is a stable map
  key. The Reference tier is metadata applied explicitly during reconciliation (§5.2).
- Two controls are "the same" when their References are identical (tier 1 — follows an object
  that MOVED, its structural key having changed) OR their StructuralKeys are equal (tier 2 —
  follows a logical control whose backing object was REBUILT: new instance, same identity).
- Constructors: `Structural(key)`, `Referenced(ref, key)`, `ForObject(ref)` (the object doubles
  as its own structural key).
- Structural keys MUST be stable across rebuilds for as long as the control logically exists.
  **Index-based keys are a last resort**, acceptable only when the collection's order genuinely
  cannot change: under reordering, tier-2 recovery silently teleports focus, and the differ
  (which compares structurally, §7.3) re-announces a control the user never left. Prefer
  persistent content ids (`"item:" + id`).
- **In a non-GC language, `Reference` is an opaque comparison token — NEVER a resource, NEVER
  dereferenced.** The ControlId in the cursor outlives the render and may be compared against a
  freed-and-reallocated address; that rare false tier-1 hit is acceptable because the structural
  key stays authoritative and the §5.2 tie-break prefers structural agreement. A node that needs
  a *usable* host handle (to drive hover, target a tooltip) carries it in the vtable's `HostTag`
  slot (§3.4), not here — one port smuggled widget pointers through `Reference` and a sound slot
  before conceding the point.

### 3.2 NodeAnnouncement — one part of a spoken readout

A control's readout is a list of **parts**:

- `Text`: `() -> string`. Null/empty at speak time = the part stays silent this time. A part MAY
  be a build-time snapshot (the rebuild keeps it fresh) — unless `Live` is set, in which case it
  MUST re-read state on every call.
- `Kind`: an optional well-known string tagging what the part is:
  `label`, `role`, `value`, `selected`, `enabled`, `tooltip`, `position`, `reason`
  (extensible; ports MAY intern kinds as constants/enums so long as extensibility survives).
  Kinds drive per-type speak ordering, node-over-type overriding, and the user's per-kind
  verbosity settings.
  ⚠ **`selected` is semantic, not cosmetic**: it is where initial focus and Tab landings resolve
  (§5.2). Marking a merely-notable node (one port marked the journal's tracked objective) silently
  reroutes Tab past every node declared before it, and the skipped nodes become unreachable.
- `Live`: if true, the part is **watched while its node is focused** — when its resolved text
  changes (an async toggle settling, the game flipping a value), the navigator speaks just that
  part (§7.6). This replaces per-element watcher machinery with one architectural mechanism.
  Field note: `Live` is optional in practice — one port ships ten screens on `StateText` (§7.5)
  alone, which covers every *synchronous* change; reach for `Live` only where values change
  without the user acting.

The **first declared part is the control's label** by convention — search, dedupe, and
path-diffing rely on this (the *spoken* order may differ per the control type's kind order; the
convention binds the declared list).

### 3.3 ControlType — control types as registry values, not classes

A control type ("button", "toggle", "slider") is **data**:

- `Key`: stable settings/registry key.
- `Order`: the announcement kinds in speak order; parts with unknown/absent kinds append after,
  in declaration order.
- `Common`: parts every control of the type shares (the localized role word), resolved per
  compose.

Deriving type identity from implementation classes (the legacy approach in both source projects)
forced artificial class hierarchies; don't repeat it. Two field warnings:

- **Keep one registry per host.** One port accumulated 48 `ControlType` definitions as per-screen
  file statics, including eight distinct `"button"` types with divergent kind orders — the moment
  P8 settings arrive, one "button" toggle silently governs all eight. The `Key` is a settings key;
  treat key collisions across definitions as an error.
- A type that declares `Common` parts but no `Order` speaks **role before label** in the merge
  (§6.1). Give every type an explicit order (the upstream project uses one shared standard order).

### 3.4 NodeVtable — behaviors as data

All of a control's behaviors, as optional slots. **A null slot means the chord is consumed
silently while the control keeps ownership of it** — the focused control's chords never leak to
the game, but only the tooltip verb speaks a "nothing there" fallback (all four shipping
navigators agree; Draft 1 claimed spoken feedback for every null slot and was the outlier). A host
MAY instead re-read the control's full readout on an actionless activate (one port does, for
legacy parity).

Canonical slots:

- `Announcements` (required, ≥1 part): the spoken focus readout.
- `ControlType` (optional): see §3.3.
- `OnActivate` — primary activation (Enter; the left-click equivalent).
- `OnSecondary` — secondary activation (the right-click equivalent).
- `OnActivateShift` / `OnActivateCtrl` — modified activations (the shift-drag / ctrl-drag
  equivalents, e.g. stack splitting). Absent in the upstream project, which models the same jobs
  with a drag state machine instead (Appendix A).
- `OnTooltip` — read/open the control's detail. The action owns the whole behavior so the kernel
  stays application-agnostic.
- `OnAdjust(sign, large)` — horizontal value adjust (sliders). **When set, Left/Right do not
  navigate.**
- `StateText`: `() -> string` — the control's state line, spoken immediately (interrupting) after
  an activation/adjust that changes state. This is the *synchronous* feedback path (survives
  rapid key repeats); *asynchronous* changes ride Live parts instead.
- `SearchText` / `ExcludeFromSearch` — type-ahead matching text (default: the label).
- `HostTag` (opaque, RECOMMENDED where P11 exists) — the per-node host handle the navigator hands
  to focus write-back, tooltip targeting, and transition-driving. Kept opaque so the kernel stays
  dependency-free.
- `OnExpand` / `OnCollapse` — optional overrides for how an expandable group's state changes
  (default: the kernel mutates the persistent expansion set).
- `SpeaksOwnExpansion` / `SpeaksOwnPosition` — set when the node's own parts already include
  that information, so the announcer doesn't append it twice.

Per-project slots that are NOT canonical (sounds, drag, hold, columns) are catalogued in
Appendix A.

### 3.5 GraphNode, edges, and the render

- `GraphNode`: `Id`, `Vtable`, four directional `Transitions` (Up/Right/Down/Left, each an edge
  to a `ControlId` destination with an optional spoken transition label — a "lane change" line),
  plus structural metadata:
  - `Parent` — the node's structural parent *within this render*, or null. The parent chain IS
    the presentation hierarchy the announcer diffs (§7.3). A parent is either a real control (a
    tree group header) or **non-focusable pure structure** (a labeled panel from `PushContext`).
    Pure-structure parents exist **only on parent chains** — they are never entered into the
    render's `Nodes`/`Order`, so traversal never has to filter them out. (A port that instead
    adds them as flagged nodes must then test `Focusable` at every traversal site; don't.)
  - `Focusable` — false for pure-structure parents.
  - `Expandable` / `Expanded` — tree group headers; `Expanded` is stamped at build time from the
    persistent expansion set (or an explicit value).
  - `StopKey` — the Tab-stop this node belongs to (§4.3).
  - `RegionKey` — optional sub-stop region for coarse jumps.
  - `PositionIndex` / `PositionCount` — auto-stamped "n of m" among the siblings arrows actually
    reach (§4.6); 0 = none.
- `GraphRender`: one built snapshot — `Nodes` (map by ControlId), `Order` (declaration order —
  drives stop/region cycling and search scan order), `StartKey` (where focus starts absent any
  prior position). Rebuilt per operation and thrown away.
- **Lifetime note for non-GC ports**: if operation results (`MoveResult`) expose node pointers,
  they point into the render pool and are valid **only until the next operation** — the next
  rebuild frees them. State this on the type; one port's tree ops must carry an id, not a
  pointer, across their internal re-render for exactly this reason.
- Tab-stop cycling and region jumps are **operations over node metadata, not edges** — they carry
  per-stop remembered positions, which a static edge cannot express.

### 3.6 GraphState — the only persistent thing

The cursor that survives between renders. One per live screen:

- `CurKey` — the focused control's id (carrying its Reference for tier-1 recovery).
- `KeyOrder` — the total traversal order computed from the previous render (for
  nearest-survivor recovery).
- `NextSuggestedMove` — a one-shot "focus here next render if present" request (consumed either
  way). *Status: no shipping caller in any port — navigators use their own deferred focus
  requests (§7.3) instead. A port MAY omit it.*
- `StopMemory` — remembered position per Tab-stop (where Tab lands when cycling back in).
- `Expanded` — the set of expanded group ids. **Screens hold no tree state of their own.**

---

## 4. The builder

`GraphBuilder` turns a screen's declarations into a `GraphRender`. Two construction styles,
freely mixable in one build — though field experience says most screens need only the first:
across the three newest ports, raw mode has **zero** production call sites, and the one screen
that looks like a grid (a perk tree) builds in menu mode with keyed rows, letting column
preservation carry the 2-D navigation.

### 4.1 Menu mode

Rows of controls, wired automatically: Left/Right within a row; Up/Down between **consecutive
rows of the same Tab-stop**. Items added outside an explicit row become single-item rows (a plain
vertical menu). Rows sharing a non-null **row key** with an adjacent row get **column-preserving**
vertical navigation (Up/Down keeps the column position when it exists in the target row; otherwise
vertical lands on the row's first item).

**Interleaved raw content BREAKS the vertical chain** (promoted from a Draft 1 parenthetical to a
normative rule here): menu rows separated by raw nodes in the same stop MUST NOT be chained past
the raw block, or the block becomes an unreachable island. This is a builder-pass ordering
constraint; one port needed a whole segment-tracking pass for it.

### 4.2 Raw mode

`AddNode(id, vtable)` + `Connect(from, dir, to, label?)` for arbitrary topologies (computed
adjacency). Edges referencing undeclared nodes are silently dropped at build. An edge may carry a
spoken transition label. Reach for raw mode only when adjacency genuinely cannot be expressed as
keyed rows — see the demotion note at the top of §4.

### 4.3 Tab-stops and regions

`BeginStop(key?)` starts a new Tab-stop; nodes added from here belong to it. Nodes declared before
any `BeginStop` form an implicit first stop. Stop keys MUST be stable across rebuilds (they key
the remembered positions); a null key auto-assigns by index, which is stable when the screen
builds stops in a fixed order. **Arrows never cross a stop** — but note this is enforced only for
*generated* wiring: raw `Connect` edges are not stop-checked, so raw-mode screens can violate it
silently. Tab cycles stops (§7.7 gives the end-of-cycle policy ladder). `SetRegion(key)` tags
following nodes with a region (Ctrl+arrow jump target) within the current stop; regions are a MAY
— several ports wire the chord and never declare a region.

Convention (field-tested): **new multi-zone screens use one stop per zone** — Tab cycles zones —
rather than one giant stop partitioned by regions.

### 4.4 The parent stack: contexts and groups

- `PushContext(label, role?, positions?)` pushes one **non-focusable** level of presentation
  hierarchy ("Difficulty settings, list") onto nodes added until `PopContext()`. Announced only
  when focus enters the subtree from outside. Its synthetic id is label-pathed so cross-render
  chain diffs match up. Two caveats: context nodes bypass builder validation (duplicate context
  ids are legal and silent), and because the id is label-pathed, **two sibling contexts with the
  same label collide** — the announcer then treats them as one level and goes silent when focus
  crosses between them. Empty-label contexts are safe only one-per-parent. `positions: false` is
  the child-position suppression switch (§4.6).
- `BeginGroup(id, vtable, expanded?)` pushes a **focusable, expandable** group header (a tree
  section). Children declared before `EndGroup()` emit **only while the group is expanded**; a
  collapsed ancestor suppresses the whole subtree (the declaration stack stays balanced
  regardless, so screens can declare unconditionally). Expansion state comes from: the explicit
  argument, else the persistent `Expanded` set, else a default.
- `IsExpanded(id)` — the builder exposes the effective expansion state so screens with **lazy
  hierarchies** (child VMs that materialize on first access) can skip even *constructing* a
  collapsed group's children. "Children emit only while expanded" otherwise implies the closures
  still run.
- Nesting recurses arbitrarily.

### 4.5 Mode-boundary stitching

Where one stop mixes menu rows with raw content (filter controls above a grid), the two wiring
systems don't see each other, and the builder stitches the seam at each mode boundary (in
declaration order, same stop). The shipped algorithm — Draft 1 described a more thorough scan
that was never implemented — is:

- **menu→raw**: if the first raw node after the boundary has no Up edge, give the menu row's
  cells Down edges to it and give it an Up edge back to the row's **first** cell. If that node
  already has an Up edge, the seam is left unstitched.
- **raw→menu**: walk backward to the **last** raw node missing a Down edge; wire it Down into the
  menu row, and give every cell of that row missing an Up edge an Up back to it.

Only **missing** edges are filled — raw content's own wiring is never overridden. The two
directions are deliberately asymmetric. *Status: this is the subtlest code in the builder and it
has zero production users outside the two source projects; implement it only when a mixed stop
actually appears* (see §4 head note).

### 4.6 Position stamping

The builder auto-stamps "n of m" positions: a multi-item row's members within their row; a
single-item-row node among the siblings sharing its `(parent, stop)` — i.e., the vertical list
level that arrows actually traverse. Raw/grid nodes get none. Positions announce only when m > 1.

Caveats the numbers depend on:

- **Every declared node counts as a sibling — including decorative labels.** A read-only blurb
  declared inside a context shifts every following "n of m" (one port shipped "2 of 5" over four
  shops). Hoist decorations outside the counted parent, or give ports an explicit
  exclude-from-positions marker.
- Child-position suppression (`PushContext(positions: false)`) applies to **single-item rows
  only**; members of a multi-item row are stamped within their row regardless.
- ⚠ Interleaved raw content breaks the *navigation* chain (§4.1), and the stamping MUST segment
  its sibling runs at the same breaks — otherwise "n of m" spans nodes arrows cannot traverse
  between (the upstream still numbers across the break; fixed in the reference as of Draft 2).
  A multi-item row does not break a run — the vertical chain passes through it.

### 4.7 Build-time validation

Errors: duplicate ControlIds; a node without at least one announcement part; an unclosed row at
build; an **empty row** at `EndRow` (unless explicitly suppressed); `BeginStop` or `BeginGroup`
inside an open row. A build that declared nothing returns null — the caller treats the screen as
"closed/empty" and leaves focus state intact for the next good render. `StartKey` defaults to the
first declared node when unset or dangling.

---

## 5. The engine

`KeyGraph` executes operations against a render callback and a `GraphState`. **Every operation
re-renders first** (`Rerender` → build fresh → `Reconcile`), so it always acts on current reality.
The kernel **never speaks** — every operation returns what happened and the navigator composes
speech.

One scoped exception to "the kernel never touches the host": reconciliation's selected-member
probe (§5.2) resolves announcement closures to test non-emptiness, which reads live application
state from inside the kernel. Implementations guard the probe; a throwing part reads as
not-selected. Ports MUST keep that guard.

### 5.1 The down-right total order

Traversal order for recovery and scanning, computed per render:

```
order = []; seen = {}; downFringe = [StartKey]
for k in downFringe (growing):
    while k not in seen:
        seen.add(k); order.append(k)
        if node(k) has Down edge: downFringe.append(down.dest)
        if node(k) has Right edge: k = right.dest else break
append every declared node not yet seen, in declaration order   # later Tab-stops
```

Visits a planar UI in reading order; the append step keeps the order **total** (stops have no
cross-stop edges).

### 5.2 Focus reconciliation

On every rebuild, move the cursor to a valid control:

```
if NextSuggestedMove set: adopt it as CurKey; consume it            # then fall through
resolved = null
if CurKey != null:
    tier 1: nodes whose Id.Reference IS CurKey.Reference            # object moved
            — if SEVERAL nodes share the Reference, prefer the one
              whose StructuralKey also equals CurKey's; else pick
              deterministically (declaration order)                 # see below
    tier 2: the node at CurKey's StructuralKey                      # object rebuilt
    fallback: from CurKey's index in the PREVIOUS KeyOrder, walk BACKWARD
              to the nearest key that still exists in this render   # nearest survivor
if resolved == null:                                                # first render / all gone
    resolved = the SELECTED member of the start node's stop, else the start node
CurKey = resolved; remember it in StopMemory; KeyOrder = ComputeOrder(render)
```

The tier-1 tie-break is normative in Draft 2. Several nodes legitimately share a backing object
(a row primary and its cells; a card and its label). Without the tie-break, "any node" over a
hash-ordered map either picks nondeterministically (the upstream behavior — a port using an
ordered map will not reproduce it) or pins arbitrarily-but-stably to one node, making every move
away bounce back and the region unwalkable (one port shipped that bug, then the tie-break and a
regression test).

`NextSuggestedMove` (where implemented) is **not terminal** — it is adopted and then re-resolved
through the tiers, so a suggested id carrying a Reference can be re-pointed by tier 1.

"Selected member" = the first node in the stop carrying a non-empty `selected`-kind part — so
initial focus lands on the checked radio/current tab, not the top of a long list. This is the
same rule Tab landings use (§7.7), which is why `selected` is semantic (§3.2).

### 5.3 Operations

All return `MoveResult` (or a typed tree result); "not moved" (at an edge / empty graph) returns
`To == From`. Tree results are the exception: only `Descended`/`Ascended` carry From/To — the
expand/collapse/empty/leaf outcomes carry the result kind alone.

- `Move(dir)` — one step along an edge.
- `MoveToEdge(dir)` — repeat until stuck. (In practice navigators call this vertically only;
  Home/End inside a row is unshipped surface.)
- `MoveStop(±1, wrap)` — cycle Tab-stops in declaration order, landing per `StopLanding`:
  the stop's **remembered position** (validated to still belong to that stop), else its
  **selected member**, else its **first node**. *Status caveat: three of four shipping navigators
  do not call `MoveStop` — they reimplement stop cycling inline, because `MoveStop` cannot
  express two states §7.7 requires: "Tab seats the cursor on an unfocused screen" (reconciliation
  has already auto-seated a phantom cursor by the time the operation runs, so the navigator must
  snapshot unfocusedness before re-rendering) and "Tab past the last stop blurs". A port SHOULD
  treat `MoveStop` as the landing-rule reference and expect the navigator to own the cycle.*
- `MoveRegion(±1)` — jump between regions within the current stop, landing on the region's first
  node.
- `Focus(id)` — programmatic focus (re-renders, then reconciles onto the id).
- `FocusByReference(obj)` — tier-1 programmatic focus. ⚠ Historically read the *previous* render
  instead of re-rendering — contradicting this section's header rule (fixed in the reference as
  of Draft 2; other carriers retain the stale read) — and it has no shipping caller (navigators
  sync game-driven selection through their own deferred-focus path, §7.3). Fix the stale read or
  omit the operation.
- Tree operations (Right/Left semantics for expandable groups):
  - `TreeRight`: on a collapsed group → expand; if expansion yields no children → auto-recollapse
    and report `EmptyGroup` (never leave a silently-empty expanded node). On an expanded group →
    descend to its first child. On an expanded group that *has* no children this render → `Leaf`.
    Elsewhere inside a tree → `Leaf` (consume; nothing to descend).
  - `TreeLeft`: on an expanded group → collapse (focus stays on the header by identity). Elsewhere
    → ascend to the nearest focusable ancestor; with no such ancestor → `Leaf` when inside a tree,
    else `None`. Navigators consume `Leaf` silently.
  - `MoveToSiblingEdge(first)` — first/last node sharing the focused node's **parent and stop**
    (Home/End at the current tree depth). ⚠ Ports MUST filter by `(parent, stop)`: root-level
    nodes all share the null parent, so a parent-only match makes Home/End on a top-level group
    header scan **every root node in every stop**, violating §4.3 (the upstream still does this,
    masked only because its navigator calls the operation solely inside trees; fixed in the
    reference as of Draft 2).
- Behavior invokers: `Activate`, `Secondary`, `ActivateShift`, `ActivateCtrl`, `Tooltip`,
  `TryAdjust(sign, large)` — run the focused node's vtable slot; false = it has none (the
  navigator consumes silently, except the tooltip verb — §3.4).

---

## 6. The announcer

### 6.1 Effective announcements

A node's effective parts = the control type's common parts (role word) merged with the node's own,
sorted by the type's kind order with a **stable** sort (unknown/kindless parts keep declaration
order, after the ordered kinds), then filtered by the user's per-type/per-kind settings (P8). This
single list feeds both readouts and the live watch. The merge's exact semantics, because every one
of these bit somebody:

- A node part **overrides** a common part of the same kind — as an all-or-nothing drop, judged
  against the node's *declared* list: if the node declares any part of kind K, every common part
  of kind K is skipped.
- A common part with a **null kind can never be overridden** and always speaks.
- Common parts are appended **before** the node's own; the kind sort runs **only if the type
  declares an `Order`** — so a type with common parts and no order speaks role-before-label
  (§3.3).
- P8 resolution order: per-type override → global per-kind toggle → speak. **Kindless parts
  always speak.** Per-type overrides typically exist only for kinds the type's `Order` lists — a
  kind a node uses but the type doesn't order is silently un-overridable.

### 6.2 Path diffing — the core speech algorithm

The spoken line for a focus change from `from` to `to`:

```
toPath   = ancestors of to (outermost first) + to itself     # via Parent pointers
fromPath = same for from (empty when from == null)
i = length of common prefix, comparing NODE IDENTITY (Id equality) level by level
if i >= len(toPath):        # ascended, or same node
    speak just to's own readout
else:
    speak toPath[i..] outermost-first, each level's readout,
    SKIPPING a level whose LABEL duplicates the next level's LABEL
    (equal, or the next label begins "label,")
prepend the crossed edge's transition label, if any
join with ", "
```

The dedupe comparand is the **label** — each level's first *declared* part (§3.2's convention) —
on both sides. Draft 1 said "next's readout begins 'label,'", which no implementation does: it
would resolve every closure on every path level twice per announcement, and it diverges from the
shipped behavior whenever a control type's order doesn't lead with the label.

Consequences (all load-bearing):

- Entering a group reads its levels outermost-first, then the landing control:
  "Difficulty settings, list, Normal, radio button, selected".
- Sibling moves share the whole prefix and read just the control.
- Descending from a group header onto its own child re-announces **nothing but the child** — the
  group is on the child's chain AND is the from-node, so the prefix swallows it.
- The dedupe rule kills "a 'Game difficulty' section wrapping the 'Game difficulty' control"
  double-reads.

### 6.3 A node's own readout (leaf text)

Effective parts resolved live, non-empty ones joined with ", " — plus, for an expandable group
that doesn't speak its own expansion, the localized expanded/collapsed state word — plus the
auto-stamped "n of m" position (unless the node carries its own position part, or the user's
position toggle is off). Wording for positions and expansion state is pluggable through the A8
vocabulary module (P7).

Implementation note worth copying: the announcer's host hooks (`PartFilter`, `PositionText`,
`ExpandedStateText`) are installed once at boot and null-safe — null means "everything speaks, no
auto positions, no state words", which is what keeps the kernel testable standalone. The
auto-stamped position is routed through `PartFilter` as a synthetic probe part of kind
`position`, so the user's per-kind toggle governs a part that appears in no node's list.

---

## 7. The navigator (host-side policies)

The navigator wires input to kernel operations and owns all speech. Reference implementations run
650–1,300 lines (plus a type-ahead engine where implemented). Its policies are normative:

### 7.1 The master gate

The layer has a single global engage/disengage state (reference name: `FocusMode`) that every
subsystem consults: disengaged, the layer speaks nothing, watches nothing, claims no keys, leaves
the toolkit's selection alone, and the game's native keyboard handling returns in full. Every
shipping port built one (Draft 1 never mentioned it); without it the layer can never hand the
keyboard back. `HasFocus`/"is a layer screen attached" is the *per-screen* predicate (§7.7); the
master gate is above it. Policy while disengaged: the differ MUST NOT speak; whether its memory
tracks silently (a landing during disengage is never spoken) or freezes (spoken on re-engage) is
host policy — the references track silently and rely on the attach reset (§9) to re-announce.

### 7.2 The standard key model

- Arrows — edge navigation. On Left/Right, a focused adjustable control (slider) **adjusts
  instead of navigating**. At an edge, Left/Right get **tree semantics** (expand/collapse/
  descend/ascend) when the focused node is in a tree. A screen MAY enable **arrow wrap-around**
  (jump to the opposite edge at a non-moving arrow) — one port defaults it on for native-UI
  parity. The evaluation order is: adjust → move → tree semantics → wrap.
- Tab / Shift+Tab — cycle Tab-stops (zones). End-of-cycle policy in §7.7.
- Home / End — jump to the edge (in a tree: first/last sibling at the current depth).
- Ctrl+arrows — region jumps where regions exist (MAY).
- Enter — `OnActivate`; Shift+Enter / Ctrl+Enter — the modified activations; a secondary key
  (reference uses Backspace) — `OnSecondary`.
- A tooltip verb — `OnTooltip`. The binding is host policy: Space and/or F1 in the references;
  one port deliberately keeps its pre-existing global tooltip hotkey instead and leaves
  `OnTooltip` unbound. Wherever the verb is bound, a hookless press speaks the localized "no
  tooltip" (§3.4).
- A **"where am I" verb** (SHOULD) — re-speak the focused node's full composed readout with its
  context chain. Every screen-reader user expects one; the kernel's full-compose entry point
  exists for it.
- Escape — the screen's Back action. A host MAY instead leave Escape entirely to the game (one
  port does, deliberately) — but then don't declare per-screen Back hooks that nothing calls.
- Printable characters — type-ahead search (§7.8; SHOULD, not MUST).

### 7.3 The frame differ (announce-once)

Each frame (P3), after the screen manager settles (§9): rebuild + reconcile (subject to the A2
cadence carve-out — compare only on frames that rebuilt); apply any pending deferred focus
request (`FocusNode(id)` / `FocusStop(key)` — the navigator-level mechanism ports actually use
for programmatic and game-driven focus; support an `announce: false` variant that pre-seeds the
differ memory for silent restores); then **if the focused identity differs from the identity last
spoken, speak the path-diffed line and update the memory**.

**The comparison is by ControlId equality — structural key only** (per §3.1, equality ignores
`Reference`). Consequences, both intended: a node whose backing object was swapped under an
unchanged key is *not* re-announced (Live parts cover content changes); a tier-1-recovered node
(same object, new key — it *moved*) *is* re-announced. Under stable content-id keys the second
case fires exactly when something the user cares about happened; under index-based keys it fires
on every reorder, which is the §3.1 trap.

This single path replaces per-callsite announce decisions. Landings that arrive via the differ
are **queued** (they follow the screen name or the keypress feedback that caused them); landings
on the direct input paths (arrows, Tab, search) follow the host's A7 policy — interrupting by
default — and update the differ memory so the differ stays silent.

### 7.4 Claim vs consume — two computations, not one

Draft 1 said "an input handler returns whether it consumed the chord; the arbitration layer
suppresses the application's handling only for consumed chords." Every port that looked closely
had to split that into two different computations, and the spec now does too:

- **Claiming** (does the layer own this chord *right now*?) MUST be a **pure function of current
  state** — screen attached? raw-capture? text field live? modal exclusive? cursor seated? — that
  the arbitration chokepoint (§8) can evaluate whenever the *application* asks, because the
  application's input processing may run before the layer's tick this frame, and on some hosts
  suppression must be decided before any handler runs at all (a static claim set registered with
  an event-source hook).
- **Consuming** (did the handler act on it?) is the dispatch result, computed during the layer's
  own update.

They will disagree at hard edges — a claimed arrow at a list end that bubbles — and that is
correct. Rules that survive across hosts:

- **Nothing focused → don't consume** activation/tooltip/secondary chords, where the host's
  suppression is dynamic enough to honor it: the application keeps its own Enter/Space verbs
  while the layer's cursor is unseated (critical on screens that overlay a live world). Hosts
  with static claim sets approximate it at screen granularity (an announce-only screen stands
  its whole claim down).
- Arrows at a hard edge: consume inside trees; MAY bubble from plain lists (references bubble
  from every plain list, not only on start-unfocused screens — what leaks must be audited, see
  §10.5).
- A screen MAY claim a chord outright as the application's contextual verb before focus gating
  (e.g. Space = the game's own pause/collect-all binding), and MAY declare **its own extra
  keys** — the pattern by which a recipe hands the player the game's native per-screen verbs
  (F/X/V/R…) rather than a mod-authored menu. Give this a first-class type (key + handler +
  spoken hint), and **republish the claim set on every screen swap** — one port shipped a bug
  where a swapped-in screen inherited its predecessor's claims.

### 7.5 Synchronous state feedback

After `Activate`/`Adjust`, if the (possibly re-resolved) focused node declares `StateText`, speak
it **interrupting**, and rebaseline the live watch so the same change isn't spoken twice. This is
what makes rapid key-repeat on a slider or toggle read correctly.

### 7.6 The live watch

While a node stays focused, watch its Live parts: on a value change, speak just that part
(queued). Rebaseline silently whenever focus lands on a new identity — the focus announcement
already spoke the initial state — and whenever the effective part *count* changes (a rebuild that
grew or shrank the list would otherwise mis-pair old and new values).

### 7.7 Focus states, blur, Tab ends

A screen MAY start **unfocused** (an exploration overlay): no cursor until Tab seats one. The
end-of-cycle policy is a ladder (Draft 1 claimed unconditional cycle-or-blur; no implementation
does that):

1. `StartUnfocused` screens: Tab past the last stop **blurs** back to unfocused and speaks the
   screen's unfocused announcement. Blur clears the cursor and differ memory.
2. Otherwise, if the screen sets `Wrap`: Tab cycles.
3. Otherwise: Tab at the ends consumes and stays put.

`HasFocus` (cursor seated?) is the single predicate the rest of the layer consults (chord
claiming, exploration gates). An equally valid architecture for the world-overlay case, shipped
by one port: no unfocused graph state at all — the exploration layer is a separate subsystem
gated on "no key-claiming screen attached". Both designs satisfy A1; pick one and gate
consistently.

### 7.8 Type-ahead search (SHOULD)

Demoted from MUST: two ports ship without it and don't miss it, a third descoped it by user
decision — and then hand-rolled exactly this shape for its one screen with hundreds of entries.
The honest rule: **any Tab-stop that can exceed a few dozen nodes needs it; screens of a handful
of controls don't.**

Where implemented: scope = the focused node's Tab-stop, in declaration order, minus
`ExcludeFromSearch` nodes and (in tabular screens) all but one cell per row. Match against
`SearchText` (default: the label) after P10 normalization. In practice accept **letters** (plus
space once the buffer is non-empty) — punctuation and digits collide with too many verbs. While a
search is live the navigator must **reserve its keys**: swallow any layer action bound to an
unmodified letter/space/arrow/Escape so typing can't fire verbs. Results navigate with Up/Down
(with synthesized key repeat), Home/End jump to first/last result, Escape clears; results also
clear when focus moves off the last landing or the screen changes. Search MUST stand down
entirely while any real text field is live — the layer's own or the application's (see §8's
text-field rule; gating only your own entry screen is a shipped bug).

### 7.9 Fault isolation

Generalized from Draft 1's Build-only rule: **no host callback the kernel or navigator invokes
may leak a throw into the frame tick** — `Build`, `IsActive`, announcement/`StateText`/`Live`
closures, `SearchText`, screen hooks (`OnKey`, cursor write-back), all of it. A throw that
escapes repeats every frame and mutes the layer. Swallow, log once per (screen, exception type) —
or the cheaper once-per-attach, which shipped fine — and render/skip nothing this frame; focus
state survives, so the next good render reconciles back. The announcer's leaf-compose path is the
easiest one to forget and the one that runs on every focus change.

**Native hosts:** the dominant failure is an access violation from a stale engine pointer, which
no language-level catch sees. The field answer is a regime, not a handler: pointer-plausibility
checks before dereference (module-range vtable checks under structured exception handling),
cite-the-disassembly-before-hardcoding discipline, and — deliberately — **letting true AVs reach
the host's crash reporter** with an attribution log of your own, rather than swallowing them into
undefined behavior. Ship your symbols so the host's crash dumps resolve your frames.

---

## 8. Input arbitration

The invariant: **a parallel tree over a live application means several input paths can react to
one keypress; every path must be found, and exactly one model may act on each chord.** Draft 1
enumerated four paths; the audit showed the *number* and the *right chokepoint* are host-shaped,
so Draft 2 states the invariant and catalogues the field-proven mechanisms per host class.

**Managed / in-process mod (Unity et al.) — the reference case, typically four paths:**

1. The layer's own poller (P4).
2. The application's global keymap — suppress per-chord, per-frame, only for chords the layer
   claims this frame. Where the keymap is user-configurable through the application's own
   settings path, prefer **relocating** colliding bindings over suppressing them (the
   application's hint text then auto-updates). A good suppression chokepoint: prefix the game's
   own rebindable-input lookup functions to report "not pressed" for claimed keys.
3. The UI toolkit's own focus/submit path (Unity's EventSystem and equivalents) — the
   application's views stay live beneath the overlay and react to Submit/click on a selected
   widget; chord suppression does not reach this path. **Ownership-gate at the application's
   action method** (a "mine now" flag checked inside the patched handler), or keep the toolkit's
   selection cleared while the layer owns focus.
4. Modal exclusivity — while a layer-owned modal is focused, the layer owns the whole keyboard.

A session-long **blanket mute** of the application's keyboard (the upstream project) is the
degenerate form of path 2: acceptable when the layer genuinely owns the whole keyboard whenever
engaged and the application has no contextual verbs worth preserving — but it forfeits §8.2's
relocation benefits, and per-chord arbitration then still exists *inside* the layer (screen input
categories with priority shadowing: the live set is the union of active screens' categories walked
focus-first, stopping at the first exclusive screen; on a chord collision the higher-priority
category wins).

**Native / own-the-event-source:** subclassing the window procedure or hooking the platform event
poll swallows claimed input **before the application's entire pipeline** — paths 2 and 3 collapse
into one mechanism with no ownership gates. This is the *easiest* arbitration environment, not
the hardest. Its own traps: the application may **poll** key state as well as consume events
(mask the polled array per-key, and read the true state through your own trampoline); the
application's internal "consumed" flag may not be honored by all of its dispatchers (delete the
event, don't mark it); and input may fan out into several internal queues from one source —
hook the source, not the queues.

**Rules that hold everywhere:**

- **A live text field beats every other arbitration decision**: while one is focused (the
  application's or the layer's), the layer neither claims, nor dispatches, nor clears the
  toolkit's selection, and search stands down. Raw-capture screens (a key-binding dialog)
  declare `CapturesRawInput`: the layer stands down and lets the application read the combo.
- Modal exclusivity runs in **both directions**: the layer's modal mutes the application — and
  under the *application's* system modal, the layer stands its claims down so the player can
  answer it with the application's own keys.
- Never bind keys your users' screen reader eats before the application sees them (NVDA: Insert,
  CapsLock). "Free in the application's keymap" ≠ usable.

---

## 9. Screens and the manager

- A **screen** = an `IsActive()` predicate over application state + a `Layer` (stacking order) +
  `Build(builder)` + lifecycle hooks (`OnPush/OnFocus/OnUnfocus/OnPop/OnUpdate`, plus the P11
  cursor write-back hook and optional per-screen extra keys, §7.4) + policy flags. The full flag
  set shipped implementations needed: `StartUnfocused`, `Wrap` (§7.7), `Exclusive`,
  `CapturesRawInput`, `KeepStateOnPop`, `InitialFocusStop`, `AllowsTypeahead`, `ScreenName` (and
  an unfocused-announcement string where `StartUnfocused` is used). Where screens declare input
  categories (blanket-mute hosts), those too. `InitialFocusStop` is redundant on hosts whose
  screens set a start key in `Build`; keep whichever one form.
- **`IsActive` discipline (the registrar pattern):** activation MUST be re-derived from live
  application state every poll. Where the application hands you an object exactly once (a
  constructor you patch), the patch **records the object only** — it never sets an "open" flag
  that a close path must remember to clear. This discipline is what makes poll-and-diff robust;
  every violation becomes a stuck screen.
- The **manager** runs per tick: poll every registered screen's `IsActive()`
  (exception-isolated, §7.9), diff the active set against the persistent stack (pop screens that
  went inactive; push newly-active ones), then attach the navigator to the top of the stack.
  Poll-and-diff — not event subscription — is what makes the stack robust to the application
  recreating its UI objects at will.
- **Screen composition — two conformant designs.** (Draft 1 said "deepest active screen of the
  top entry" without defining children; both readings shipped.)
  - *Flat stack*: layer-ordered, ties to most-recently-pushed. Sufficient for registries of a
    few dozen screens with explicit layer numbers.
  - *Child-screen tree*: screens may imperatively `PushChild`/`RemoveChild` sub-screens
    (dropdown option lists, confirm modals, drill-in stacks) that are not independently
    registered; the manager attaches to the **deepest active** screen of the top entry and pops
    subtrees deepest-first. Two hard-won guards: a cycle check on the child chain (or the
    manager spins), and reparent-before-adopt (or a focused child orphans). If the manager's
    `OnUpdate` hooks can push/remove children, re-sync focus after running them (the reference
    does poll → diff → sync → update → sync).
- **Native hosts: debounce attach.** Give a newly-active screen a settle window (~10 consecutive
  active frames shipped) before building against it — every crash one port hit lived in a
  scenario-transition window with controllers half-constructed. Managed hosts tolerate a
  half-built VM; native ones fault.
- **Per-screen cursor state**: each live screen keeps its own `GraphState`. A screen *covered* by
  a higher layer keeps its state and restores exactly where the user was when focus returns
  (the differ memory resets on attach, so the restored landing announces itself). A *popped*
  screen's state is dropped — reopening starts fresh — unless it opts out (`KeepStateOnPop`).
- On focus change, the manager speaks the screen's name (queued), then the differ announces the
  landing — standardized first-focus, uniform across eager and lazy-building screens. During a
  migration (§10.5), suppress whichever of the two name sources is redundant.

---

## 10. Porting guide

### 10.1 What ports verbatim vs what you rewrite

- **Verbatim** (the kernel, §3–§6): ~1,700 lines in C#; **budget 2,000–2,500 in a non-GC
  language** (header/impl split, explicit ownership, lifetime documentation). The 50-test
  conformance suite transcribes with it. Both C++ ports transcribed the algorithms without
  redesign; the net35 port is a literal file copy with an 8-line delta. Keep the kernel's
  comments engine-neutral (§3) and, in compiled languages, add a cross-TU ABI guard on the
  kernel headers — a stale object file with an old struct layout produced one port's worst bug,
  and a link-time mismatch error is free.
- **Per-host, shaped by this spec** (§7–§9): navigator, arbitration, screen manager,
  speech/sound bridges. 1–2k lines held exactly across hosts.
- **Per-game, the real cost**: the screen recipes — one `Build` + drive-paths per screen (the
  reference registers 62; the C++ ports run 10–36), plus event narration, world/exploration
  layers if applicable. Two honest numbers from the audited ports: menu recipes alone ran ~38% of
  one project; recipes plus the other per-game reading layers ran ~76–83%. The infrastructure
  this spec covers is **5–10% of a finished project** — that is its value proposition stated
  plainly. On a native host, add the address/offset pipeline (§10.3), which exists *before the
  first node is declared*.

### 10.2 Host viability checklist

For a candidate game, verify you can:

1. read UI state on demand (P1) — and enumerate what backs each visual screen;
2. call its UI handlers from the context you'll ship (P2);
3. run code on a tick where 1–2 are legal (P3);
4. see raw keys AND suppress the game's handling for claimed keys at some chokepoint (P4);
5. reach a screen reader (P5);
6. write the layer's cursor into the game's own selection, if its verbs read it (P11);
7. iterate fast. In a managed host: a REPL/dev-server into the live game (transformed every
   project that had one). In a native host the equivalent is **a symbol-bearing disassembly plus
   a read-only live inspector** — the game's own UI definition files say what exists, the
   disassembly says how it's shaped, live instrumentation says what's true this frame. Check
   which storefront's binary ships a PDB before choosing where to buy.

### 10.3 Ecosystem notes

- **.NET/Mono games** (Unity Mono, SMAPI, tModLoader, RimWorld): the reference kernel drops in
  as-is — field-proven at an 8-line delta even on net35, though the drop-in is
  *compiler*-dependent, not framework-dependent (C#6/7 syntax needs a modern compiler via
  SDK-style projects + reference assemblies; `IReadOnlyList` needs a net45+ profile or an
  `IList` substitution). Harmony provides P2/P4 patching.
- **Unity IL2CPP**: same, via BepInEx/MelonLoader interop.
- **Native game with a mature reflection-based plugin loader** (RED4ext, SKSE/CommonLibSSE, and
  kin) — a category Draft 1 missed, and closer to the .NET row than to raw hooking: P1/P2 are
  name-based reflection calls; the residual hand-RE is the widget-tree layout (a handful of
  offsets), guarded by a game-build verification gate. Budget thread-safe reads (bounded
  try-locks; a skipped read ≠ "absent") and, if P1 is slow, the A2 rebuild cadence.
- **Unreal**: UE4SS gives Lua + UObject reflection (P1/P2) and a tick; port the kernel to Lua.
- **Godot**: scene-tree access covers P1/P2; GDScript or C# port.
- **Native / no modding API**: P1/P2 via a disassembler-derived address table (Ghidra + any
  shipped PDB) and inline hooks (MinHook/Detours); dynamic instrumentation (Frida) is for
  read-only hypothesis checking, not shipping. The kernel ports verbatim and the paradigm holds —
  immediate mode is a *better* fit here than in a managed host, because rebuild-and-reconcile is
  the only cache policy raw pointers can't invalidate. But budget three things the managed rows
  don't have: (1) a **regenerable, versioned address/offset pipeline** — symbol manifests, offset
  generators, a drift auditor — which is per-*game-version* forever, not per-screen, and dwarfs
  the per-screen RE the moment the game updates; (2) **fail-closed version canaries** (image-size
  discriminators, prologue byte checks) so that on an unknown build every module declines to
  install and a game update degrades to silence, not crashes — brittleness is a design choice
  about what stale offsets do; (3) the §7.9 native memory-safety regime, plus P4 delivered at the
  platform event source (§8) — which, once built, is the easiest arbitration model of any host
  class.

### 10.4 Conformance

A port is conformant when it passes translations of the 50-test kernel suite (builder wiring,
reconciliation tiers incl. the shared-Reference tie-break, order computation, tree semantics,
announcer path-diff/dedupe/ordering) and honors the normative policies of §7–§9 at their stated
strength (the MUSTs; the SHOULDs knowingly). The axioms of §1 are the review checklist for
everything above the kernel.

### 10.5 Migrating beside a legacy layer

Every real port after the first arrives inside a mod that already speaks — a retained-mode focus
manager, per-screen narrators, hand-rolled hotkeys. Draft 1 had no story for coexistence; three
ports needed one. The protocol:

- **One ownership gate.** A single predicate ("does a kernel screen own this frame?") consulted
  by every legacy path — the legacy focus reader, legacy Tab handling, the arbitration fast
  path, the fallback narrator. The legacy layer stands down where the gate is true, never
  per-feature.
- **One keypress, one model.** Thread the navigator's consumed-result into the legacy dispatcher
  so a chord handled by the graph can never also fire a legacy handler.
- **A fallback narrator is legitimate** — riding the game's native focus (against A1) as the
  incremental-rollout floor for un-migrated surfaces is how a port ships value from week one.
  Gate it off wherever a recipe owns the surface *or is still settling* (§9), and reset its
  cross-frame caches when it resumes — they are stale across the gap.
- **Suppress double-speak at the seams**: if a legacy patch already announces a screen's name,
  the migrated screen's `ScreenName` returns null until the legacy patch retires.
- **Poll-and-diff has a one-tick rising edge.** A kernel screen that must yield to a *legacy*
  overlay the same tick it appears cannot wait for the polled diff — consult that module's
  active-predicate directly.
- Re-audit what bubbles (§7.4) at every migration step: chords that fell through harmlessly to
  the game start reaching legacy handlers you forgot were listening.

---

## Appendix A — per-project kernel extensions (not canonical)

The canonical kernel is §3–§6. Each shipping project carries extensions; none is required for
conformance. They are catalogued here so ports know what they're seeing when they read the
sources — and which ideas are worth stealing.

- **WrathAccess (upstream)**:
  - *Tabular columns* — `NodeVtable.Column` + `GraphState.LastColumn`: when a focused row
    vanishes, nearest-survivor recovery slides along the landing row's Left/Right edges back to
    the column the user was working in; type-ahead landings do the same, and search results
    dedupe to one hit per row. The best-behaved answer to "my row disappeared" in tabular
    screens; upstream-worthy.
  - *Drag* — `NodeVtable.OnDrag` + a pick-up/place state machine on a dedicated key (inventory
    move). The downstream projects replaced it with `OnActivateShift`/`OnActivateCtrl`.
  - *Row-inherited activation* — the shared sheet factory copies the row primary's
    activate/secondary into every metadata cell so Enter works from any column.
- **RTAccess (reference)**:
  - `OnActivateShift` / `OnActivateCtrl` (canonical in Draft 2, listed here for lineage — the
    upstream lacks them).
  - *Per-node sound slots* (`HoverSound`/`ClickSound`/`ActivateSound`) with the precedence rule:
    click beats activate; both null = play nothing because the game's own driven action already
    sounds. Hover fires only on the interrupting (keypress) paths, never from the differ.
    RT-only; dead in both C++ ports, whose hosts sound for free via P11 — see P6.
  - The `reason` announcement kind (unavailability reasons on action-bar slots).
- **CyberAccess**:
  - *Flyouts* — `BeginFlyout`/`EndFlyout` + enter/leave operations: menu-bar drop-down columns
    where the owner **stays activatable** rather than becoming an expandable container, with no
    persistent expansion state. `BeginGroup` cannot express this; upstream-worthy.
  - `OnActivateHold` + tap-vs-hold gesture parking (~400 ms), because the host's own UI has
    hold-to-confirm verbs (A5). Cost documented honestly: declaring it makes plain Enter fire on
    release.
- **Sunless Sea / Stellaris**: no kernel extensions (verbatim/faithful transcriptions).

## Appendix B — known defects in the source implementations

Recorded so a porter reading the sources doesn't canonize them (⚠ markers in the body point
here):

1. `MoveToSiblingEdge` lacks the `(parent, stop)` filter — root-level Home/End can cross
   Tab-stops (upstream; fixed in the reference 2026-08-09; §5.3).
2. `FocusByReference` reads the previous render instead of re-rendering (upstream and the C++
   port that carries it; fixed in the reference 2026-08-09; §5.3).
3. Position stamping counts across raw-content breaks in the navigation chain (upstream; fixed
   in the reference 2026-08-09; §4.6).
4. The upstream's type-ahead does not stand down for the *game's* text fields — only its own
   raw-capture entry screen (§7.8, §8).
5. Tier-1 resolution iterates a hash-ordered map with no tie-break — nondeterministic under
   shared References (upstream; the reference implements the §5.2 tie-break as of 2026-08-09).
6. One native port speaks a live text tier that bypasses its own P10 markup stripping.
7. One port's per-screen `ControlType` statics violate the single-registry rule (§3.3) —
   latent until P8 arrives.
