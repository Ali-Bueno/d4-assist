# Accessibilizing inventories

> When the user says **"accessibilize the inventory"** (or the Spanish **"accesibilizar inventario"**),
> apply exactly this behavior. It builds on the [menu rules](menus.md); the points below are
> inventory-specific. Shared principles live in [the topic README](README.md). The reusable
> architecture (grid-cell archetype, subscriptions, one reader shared by belt/equipment/storage) is in
> [generic-strategy.md](generic-strategy.md).

## Item readout (navigation)

When focusing/landing on an item, read its **name + description + tooltip**, including every stat the
game shows for it (durability, damage, stats, weight, value, etc.):

- **Always follow the exact order the game displays the information.** Mirror the on-screen layout; do
  not reorder or invent fields.
- Read only what's present; skip fields the item doesn't have.

Example (item with stats): `Iron Sword, a sturdy blade, damage 12, durability 40 of 50`.

## Subsections

If the inventory has subsections (e.g. **Belt / hotbar**, **equipment slots**, **crafting**, **storage**),
treat them as **separate groups**:

- **Announce the subsection name when navigation crosses into it** (focus enters a new group), then read
  the focused item as above.
- Keep the subsections clearly separated so the user knows which area they're in.

## Crafting section

When the focus is in a crafting subsection, for each recipe read the **output item name + its
requirements as material counts**, formatted as a **have / needed** ratio per material, in the order the
game lists them:

- Format: `<item>, <material> <have> / <needed>, <material> <have> / <needed>, …`
- Example: `Iron Sword, iron 3 of 5, wood 1 of 2`.
- Make it clear whether the recipe is currently craftable (e.g. say "craftable" / "missing materials")
  if the game conveys that, following its order.
