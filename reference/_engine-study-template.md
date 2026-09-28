# [GAME NAME] — [Subsystem] Reverse-Engineering Study

> Blank template for documenting a new engine/game study. Copy this file into
> `reference/<topic>/<game-slug>/` (one file, or split into numbered docs like `a-heros-call/` if the
> study is large). Delete guidance in brackets as you fill it in.
>
> **Goal of every study:** recover enough of how the game does X that the technique can be
> *re-implemented in our accessibility mod* on this or another engine — not a full decompile, just the
> portable model + the constants + the gotchas.

---

## 0. Identification & tooling

| Field | Value |
|-------|-------|
| Game / build | [name, version] |
| Engine | [Unity Mono / Unity IL2CPP / Unreal / RE Engine / MonoGame / native C++ / VB6 …] |
| Binary / assemblies | [`GameAssembly.dll`, `*-Win64-Shipping.exe`, `Game.exe`, managed DLLs …] |
| RE tool used | [Ghidra (native) / ILSpy (managed) / Il2CppDumper + cpp2il (IL2CPP) …] |
| Modding framework | [BepInEx 5/6 / REFramework / UE4SS / XV Patcher / native hook …] |
| Speech path | [PRISM (default) / Tolk fallback] |
| Source location | [where the recovered source/notes live on disk] |

> Identify the engine **before assuming anything** (see CLAUDE.md "Investigar el Engine"). Note how the
> game's existing modding community works — don't assume BepInEx.

---

## 1. Core data model (the foundation)

[What is the world made of? Tile grid? Continuous 3D + navmesh? Rooms/triggers? What does the player
state look like (position type, heading representation)? What per-cell/per-object data drives the
feature you're studying? This section is what makes everything else portable — get it right first.]

> Confidence: [High / Medium / Inferred] — [how you confirmed it]

---

## 2. Coordinate system & conventions

[Axes (Y-up or Y-down), heading zero & rotation direction, units, angle↔vector conversions, how
left/right are derived from facing. Pin these explicitly — convention mismatches are the #1 source of
mirrored-direction bugs. Include a worked example: "facing X, left points Y".]

---

## 3. [Subsystem A — e.g. Collision / Movement]

[Flow per step, what gates it, what sound/speech it produces, the exact branch logic if recovered.]

> Confidence: […]

## 4. [Subsystem B — e.g. Radar / Scanning]

[…]

## 5. [Subsystem C — e.g. Zones / Beacons / Spatial audio]

[…]

---

## 6. Tunable constants

[Pull the real numbers from the source. These become the sane defaults when porting.]

| Constant | Value | Where (symbol/addr) | Meaning |
|----------|-------|---------------------|---------|
| … | … | … | … |

---

## 7. Porting notes (→ our mod)

[Minimal engine-adapter surface needed (position, heading, raycast, speak, play-sound). Which parts are
pure engine-free logic to copy verbatim vs. which need an engine-specific replacement. The conceptual
shifts (e.g. tile grid → physics raycast; add a Y/elevation axis the original didn't need).]

---

## 8. Not fully recovered / open questions

[Be honest about what's inferred vs. confirmed, oversized functions Ghidra skipped, branches not read
line-by-line, etc. Note exactly where to look if the precise code is needed later.]
