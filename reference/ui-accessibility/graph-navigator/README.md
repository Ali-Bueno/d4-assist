# Graph navigator — third-party spec (WH40KRTAccess)

> **Provenance:** [`graph-a11y-spec.md`](graph-a11y-spec.md) is a **verbatim copy** of
> `docs/graph-a11y-spec.md` from [alex19EP/WH40KRTAccess](https://github.com/alex19EP/WH40KRTAccess)
> (accessibility mod for *Warhammer 40,000: Rogue Trader* — Unity Mono, Unity Mod Manager, PRISM).
> Author: Alexander Epaneshnikov. License: **MIT** ([LICENSE](LICENSE), kept alongside).
> Copied 2026-09-06 from commit `6e6fc04` (Draft 2, 2026-08-09). Do **not** edit the copy; to refresh
> it, re-download the raw file and update this header. Written by blind developers, distilled from
> **five shipping mods** (Rogue Trader, Pathfinder: WotR, Cyberpunk 2077, Stellaris, Sunless Sea —
> two of them native C++), with a 50-test conformance suite. Lineage: Factorio Access → Tanglebeep →
> WrathAccess → RTAccess.

## What it is

An **engine-neutral kernel** for screen-reader UI navigation: the mod owns a **parallel UI tree
("the graph")** rebuilt from live game state on every render (immediate mode, never mutated), a
generic navigator walks it, and a single **announcer** speaks the *path diff* between the last spoken
node and the focused one. Everything engine-specific enters through 12 named **host ports**
(state read, action invoke, tick, key input, speech, text normalization, focus write-back, …). The
kernel is ~1,700–2,500 lines and is meant to be **ported verbatim**; per-game cost is only the screen
"recipes" (builders).

## How it relates to our [generic UI strategy](../generic-strategy.md)

The two documents start from **opposite axioms** — read both before choosing:

| | Our `generic-strategy.md` | Graph kernel (this spec, axiom A1) |
|---|---|---|
| Source of focus | **Rides the game's own** focus/selection layer; subscribes at the highest shared widget layer and classifies widgets into archetypes | **Owns** navigation: builds its own tree, ignores the native focus ring (writes the cursor back only where the game's verbs need it — port P11) |
| Unseen screens | Covered by default via archetype readers + generic tree-walk fallback | Not covered automatically: every screen declares a builder (menu mode auto-wires rows; raw mode uses explicit `Connect`). A native-focus "fallback narrator" is allowed only as a migration floor |
| Best fit | Games whose UI framework exposes a coherent, reliable focus/widget model (Unity UI `Selectable`/EventSystem, UMG, RE Engine GUI) | Games whose native focus is unusable for a blind player: skips non-interactive text, follows visual layout, changes semantics per screen, or **has no widget tree at all** (native/closed engines, immediate-mode UIs, dense strategy screens) |

Rule of thumb: **try our strategy first** (it is cheaper: coverage by default). Switch a screen — or
the whole mod — to the graph kernel when the native focus model fails the checklist in the spec's
§10.2 (viability), or when reading order must differ from the visual one. The two can coexist during
a migration (§10.5: single ownership gate, one keypress → one model, suppress double-speak at seams).

## Ideas worth adopting regardless of approach

These are engine-agnostic and slot into our existing pipeline without switching to the graph model:

- **Two-tier control identity** (§3): `Reference` (object identity, "it moved") + `StructuralKey`
  (value-equatable, "it was rebuilt"); focus recovery falls back tier 1 → tier 2 → nearest survivor in
  previous order. Fixes the "focus lost when the screen re-instantiates its rows" bug class.
- **Path-diff announcer** (§6): speak the ancestor chain outermost-first, only the part that changed
  since the last spoken node, deduping a level whose label repeats the next. One algorithm replaces
  ad-hoc `Speak` calls and enforces *announce exactly once* (axiom A4). Directly compatible with our
  diff-gating rule.
- **Interrupt policy as one global point** (A7): key-caused speech interrupts, passive focus changes
  queue — the same rule as our "context change → interrupt" principle, but enforced in one place.
- **Claim vs. consume** input arbitration (§8): a pure "do I own this key right now?" query separate
  from "did I act on it?" at dispatch time. Avoids the host timing edge cases where the game and the
  mod both react to a key.
- **Text normalization as a required port** (P10): payload chunking and backend-safety for speech —
  the same concern as our PRISM chunking.
- **Fault isolation** (§7.9): every host callback exception-guarded so one screen degrades locally
  (native hosts add pointer-plausibility checks) — matches our Step 5.
- **Porting notes per ecosystem** (§10.3): Unity Mono/BepInEx-Harmony, IL2CPP, RED4ext/SKSE-style
  reflection loaders, UE4SS, Godot, raw native. Useful cross-check against our `engines/` docs.

## Related docs in the source repo (not copied)

The repo's `docs/` also holds per-screen audits and explorations (tooltip reader, combat/main HUD
parity, ship management, sector/space maps, input-system review). Fetch on demand; they are
game-specific and were not copied.
