# UI accessibility — behavior specs

Prescriptive **behavior rules** for accessibilizing in-game UI with a screen reader (via PRISM) +
audio cues: how *our* mods must behave, regardless of engine. Unlike the rest of `reference/`, this
is not reverse-engineering knowledge — it's the contract the patches/hooks delegate to.

The playbook that *uses* these specs is [the root CLAUDE.md](../../CLAUDE.md) (§8). Read the relevant
doc before implementing that part of a mod.

## Documents

| Doc | Trigger phrase | Covers |
|-----|----------------|--------|
| [Generic UI strategy](generic-strategy.md) | (read first for either trigger below) | Coverage-by-default architecture: classify **widgets, never screens** — reusable archetype readers cover every screen automatically; per-screen overrides only for genuine quirks; generic tree-walk fallback + fault isolation so a changed screen degrades locally, never breaks the mod |
| [Menus](menus.md) | "accessibilize menus" / "accesibilizar menús" | Entering/returning to a menu/screen (title + focused item only), full-text screens (loading/help/tutorial — the only read-in-full case), tabs/sections, vertical navigation, horizontal controls (sliders/checkboxes/dropdowns), popup & confirmation dialogs |
| [Inventories](inventories.md) | "accessibilize the inventory" / "accesibilizar inventario" | Item readout, subsections (belt/equipment/storage), crafting recipes (have/needed) — builds on the menu rules |
| [Graph navigator (third-party spec)](graph-navigator/) | (read when the game's native focus is unusable) | Verbatim MIT copy of the **WH40KRTAccess graph a11y kernel** spec by blind developers, proven in 5 shipping mods: mod-owned parallel UI tree, path-diff announcer, two-tier control identity, input arbitration, 12 host ports, porting guide. Opposite axiom to our generic strategy — the folder README says when to pick which and which ideas to adopt regardless |

**Read [generic-strategy.md](generic-strategy.md) first**, then the concrete spec: it defines the
default pipeline (widget classification → archetype readers → fallback) that the menu/inventory rules
plug into. Screens are never implemented one by one — a screen only gets its own code when it has a
genuine quirk the generic pipeline can't infer.

## Shared principles (apply to every UI doc here)

- **Context change → `interrupt=true`** (new screen, new tab, dialog opens). Incremental info
  (changing a value) → `interrupt=false` and read **only what changed**.
- **On every menu change (entering *or* going back): title + focused item, nothing more.** Never
  enumerate a menu's items or labels; the rest is discovered by navigating
  ([menus.md](menus.md#entering-leaving-or-returning-to-a-menu--screen)). The **only** text read in full
  is text meant to be read: loading tips, help, tutorials, popup messages — followed by the focused
  button, if any ([menus.md](menus.md#full-text-screens-the-only-exception)).
- **Follow the game's on-screen order.** Never reorder or invent fields; read only what's present.
- **Diff-gate**: detect real changes before announcing; never repeat the same state.
- Centralize spoken strings in a single, translatable layer — no hardcoded text inside patches.

## Planned

Future UI behavior specs (HUD, combat readouts, map/world screens) go here as their own docs, added to
the table above when written.
