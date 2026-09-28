# Accessibilizing menus

> When the user says **"accessibilize menus"** (or the Spanish **"accesibilizar menús"**), apply exactly
> this behavior. Shared principles live in [the topic README](README.md). Build this on top of
> [generic-strategy.md](generic-strategy.md) — the rules below are the canonical read behavior for each
> **widget archetype**, implemented once in the shared archetype readers. They apply to every screen
> automatically via widget classification; never re-implement them per screen.

## Entering, leaving or returning to a menu / screen

This rule fires on **every menu change**, in either direction: opening a menu, going **back** to the
parent menu (Escape/Back, closing an overlay or a dialog), and the very first screen the game shows
(main menu at boot) **before the player has pressed anything**. Whenever the active menu becomes a
different one than the last one spoken, announce, in **one** utterance with `interrupt=true` (the
context changed):

1. The **screen title**, read off the screen (`Main menu`, `Options`, `Load game`). If the screen draws
   no title, use the screen's name as the game knows it, or a short generic name the strings layer
   provides for that screen type. Never skip this: the player must know *where* they are.
2. The **initially focused item**, read exactly as [vertical navigation](#vertical-navigation-up--down)
   would read it: **name, state/value (if any), tooltip/description (if any)**. If the screen has a
   description of its own and no focused item yet, read that instead.

Nothing else. **Never enumerate the menu's items on a menu change.** Reading every option (or every
label on the screen) when a menu opens or is returned to is the single most common mistake with this spec: it is slow, hides the
focused option and forces the player to interrupt. The remaining items are discovered by navigating.

Examples:

- Game boots into the main menu, `New game` focused:
  `Main menu. New game, start a new game`.
- User presses "Options", `Audio` focused: `Options. Audio, adjust the game's audio settings`.
- User presses Back from Options; the main menu regains focus on `Options`:
  `Main menu. Options, change the game's settings`.
- User presses "Load game" on a screen with a description and the first slot focused:
  `Load game. Slot 1, Chapter 3, 2 hours 10 minutes`.

Everything spoken here is **data read off the screen** (title label, focused widget) through the shared
archetype readers — no per-screen strings. If the game moves focus onto the first item a frame or two
*after* the panel appears, wait for the focus event and speak title + item together (diff-gate so the
item is not spoken twice); do not speak the title alone and then the item as a separate late utterance
if you can avoid it.

## Tabs / sections

Many menus are split into **tabs** (e.g. `Video`, `Audio`, `Dialogue`). When the user switches tabs
(left/right shoulder buttons, tab keys, etc.):

- **Announce the tab name** you moved to, nothing else.
- Examples: `Video options`, `Audio`, `Dialogue`.

After announcing the tab, normal vertical navigation reads the focused item inside it as usual. Use
`interrupt=true` on tab change (the context changed).

## Vertical navigation (up / down)

When moving between menu items (arrow up / arrow down), announce **in this order**:

1. The item's **name**.
2. The item's **current state / value** (if any).
3. The **tooltip / description** (if any).

Example: `Language, English, changes the game language`.

## Horizontal navigation and controls (left / right, dropdowns)

For **sliders, checkboxes, toggles and dropdowns**, when changing their value with left/right or when navigating inside an open dropdown:

- **Read ONLY the new value**, nothing else.
- Do **NOT** repeat the label name or the full phrase.

**Correct** (user on "Language: English", presses right):
```
Spanish
```

**Incorrect**:
```
Language: Spanish
```

Rules per control type:

- **Slider** → read only the new value (`75%`, `8`, `High`).
- **Checkbox / Toggle** → read only the new state (`On` / `Off`, `Enabled` / `Disabled`).
- **Dropdown** → on opening or navigating options, read only the value you're on (`Spanish`, `English`, `Japanese`).

## Key distinction

- **Entering an item** (vertical navigation) → name + state + tooltip (full context, because focus changed).
- **Changing an item's value** (horizontal navigation / dropdown) → only the changed value (because the user already knows the context).

This avoids redundant verbosity and keeps reading snappy.

## Submenus, overlays and the menu stack

Games often open a submenu as an **overlay drawn on top of the parent menu without closing it** — the
parent stays active/visible underneath, and sometimes no "screen opened/closed" event fires at all.
Two real failure modes to design against:

- The mod keeps reading the **parent menu underneath** instead of the submenu that's actually on top
  (it never noticed a new layer opened).
- The overlay closes and the mod **doesn't notice**, so it stays stuck on the dead submenu context and
  can't read the parent menu the user is now navigating.

Rules:

1. **Track a menu stack, and always read the topmost interactive layer.** The active reading context
   is *whichever layer currently owns focus/input* — never "the last screen that fired an open event".
   Overlay opens → push + announce it (`interrupt=true`, same as entering a menu). Overlay closes →
   pop + announce the layer that regains focus **the same way** (its title + its focused item —
   [entering/returning rule](#entering-leaving-or-returning-to-a-menu--screen)); going back is a menu
   change like any other.
2. **Don't trust open/close events alone.** If the game leaves the parent active under the overlay,
   detect the active layer by **who actually receives the navigation input / owns focus** (the game
   routes input somewhere — that layer is the one to read). If no reliable signal exists, poll the
   focus owner at a modest cadence and diff-gate.
3. **Never read a covered layer.** While an overlay is on top, focus events bubbling from the parent
   underneath (some engines keep firing them) must be ignored.
4. **Respect on-screen order *within the active layer***: read its widgets in the order the game draws
   them, but only for the topmost layer.

Popup/confirmation dialogs (below) are the simplest case of this same rule — a one-level overlay.

## Full-text screens: the only exception

The **only** UI that is read in full is text the game shows *to be read*: **loading-screen text**
(tips, lore), **help / controls screens**, **tutorial pop-ups**, message boxes and story/notification
panels. When one appears, read its whole body text in the game's on-screen order (`interrupt=true`,
context changed), and then — if it has buttons — the **focused option** only, exactly like a popup
dialog below. This is text, not a menu: there are no items to enumerate, so reading it all is the
correct behavior, and it is the *only* case where it is. A loading tip that changes while the screen is
still up is read again (diff-gate on the text).

## Popup and confirmation dialogs

When a **popup dialog** or a **confirmation dialog** appears (e.g. `Do you want to exit the game?`,
`Apply changes?`), treat it as a context change and accessibilize it:

- On open, announce in **one** utterance with **`interrupt=true`**: the dialog's **message / question**
  (its title and body text, in the order the game shows them) followed by the **focused button /
  option** (e.g. `Yes`, `No`, `Cancel`, `Apply`). The message is read in full; the buttons are **not**
  enumerated — only the focused one.
- When the user moves between the dialog's buttons/options, read **only the focused option** (same rule
  as horizontal navigation).
- On close, return to the underlying menu and announce it as a menu change: its **title + the focused
  item** ([entering/returning rule](#entering-leaving-or-returning-to-a-menu--screen)), `interrupt=true`.
  Diff-gate on the menu identity so a dialog that closes without changing the active menu's focus is
  not followed by a duplicate readout of a menu the user never left — the *menu* changed (dialog →
  parent), so it is announced once.

Example (exit confirmation opens, "Yes" focused): `Do you want to exit the game? Yes`. User presses
right: `No`.
