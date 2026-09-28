# Unity/IL2CPP runtime gotchas

Traps that live **after** the mod loads: missing API surface, obfuscated members, state that goes stale,
and things that are merely slow. Sorted by how much time each one has cost us.

---

## 1. Never identify a game object by its **name**

`GameObject.Find("Player")` works right up until the game updates and renames the prefab. Then
*everything positional dies silently* while the rest of the mod keeps working — the hardest failure to
diagnose, because nothing errors. This exactly happened on the Unity 6 update: all spatial audio (walls,
enemies, beacons, pickups) went quiet, speech kept working, no exception anywhere.

**Locate by type, from the game's own components:** `FindObjectOfType<Player>()` and cache it. Name
lookup is a *fallback*, never the primary path. Same for scene/tag strings.

## 2. Cached game-state objects go stale without any error

When the game restarts a run in place (a "Retry" that reuses the scene), it swaps the controller object.
The old managed wrapper still points at freed IL2CPP memory and **keeps answering** with stale values —
`TryCast<T>()` doesn't detect it, reading `.State` doesn't throw. Every component that had cached the
controller silently kept reading the dead one.

**Pattern that fixed it:** one shared `GameStateHelper` that

- re-finds the controller on an interval (~2 s) and **compares native pointers** to detect a swap,
- exposes a single `IsInActiveGameplay()` all components call — no per-component state checks,
- increments a `RunGeneration` counter on each swap, so components reset their per-run state by
  comparing generations.

Never let 15 components each decide independently whether the game is running.

## 3. API surface differs per game build — verify, don't assume

Same BepInEx, different games, different answers:

| API | Observed |
|---|---|
| `FindObjectsOfType<T>()` / `Scene.GetRootGameObjects()` | Threw *"Method not found"* on Megabonk (Unity 2023 IL2CPP); works fine on DRG (Unity 6). |
| `OnEnable()` / `OnDisable()` Harmony patches | Never fire on Megabonk's IL2CPP build. |
| `UnityEngine.AI.NavMesh` (`SamplePosition`, `CalculatePath`) | **Native-fatal** on the Unity 6 build — instant uncatchable process death. Replaced with managed A* over the game's own grid data. |
| `is` / pattern-matching type checks on interop types | Unreliable — compare `GetType().Name` (or `GetIl2CppType().Name`) strings. |

Probe each of these once on a new target and write the answer in that mod's CLAUDE.md before building
anything on top.

## 4. IL2CPP field names may be obfuscated — read the UI through Transforms

On obfuscated builds, direct field/property access on generated types produces trampoline errors or
garbage, and the field you want is called `_a3`. The portable workaround: **walk the hierarchy by
child name and pull components**:

```csharp
var label = panel.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
```

Child object names survive obfuscation far more often than field names. Slower than a field read, so
cache the resolved Transform, not the value.

Related trap: **labels are often grandchildren, not siblings** (`Slider → MasterSlider → NameText`).
Use `GetComponentsInChildren<TMP_Text>()` and skip the known value field, rather than guessing a path.

## 5. Custom MonoBehaviours need registration (IL2CPP)

```csharp
ClassInjector.RegisterTypeInIl2Cpp<MyComponent>();          // once, in Load()
…
public class MyComponent : MonoBehaviour
{
    public MyComponent(IntPtr ptr) : base(ptr) { }          // required IL2CPP ctor
}
```

Without registration, `AddComponent<MyComponent>()` fails. A `BasePlugin` isn't a MonoBehaviour, so any
per-frame work has to live in such an injected component (attached to a `DontDestroyOnLoad` object).

## 6. Values that are not what they look like

- **Percentages are stored as fractions** (`0.05` = 5 %). Format through one shared helper, never
  inline `{v:0}%`.
- **UI text multipliers**: a slider can carry a `textMultiplier` that makes it display `6000` for 60.
  Correct the *specific* control by name; other controls use the same field legitimately (volume
  0–1 → 0–100).
- **Unity 6 widened types**: `int`→`long`, `float`→`double` in game APIs. Compiles, then misbehaves.
- **Rich text and localisation junk** (`<color=…>`, serial numbers) must be stripped in one shared text
  cleaner before speaking.
- **Never hardcode a constant you could read.** Damage radii, detection distances, currency names, rank
  thresholds — take them from the game's own config objects. If a literal is truly unavoidable, name it
  and document where the number came from.

## 7. Objects you cannot `FindObjectOfType`

Dependency-injected services (Zenject and friends) are often **not MonoBehaviours**, so scene search
never finds them. Two working escapes:

- Patch a UI/consumer method the game itself injects into, and cache the instance it receives
  (`UIPlayerRankWidget.Inject` → grab the `PlayerRankManager`).
- Reach it through an object that *is* findable (`MapGenerator.RedVineSystem`).

## 8. Performance

- **`FindObjectOfType` in `Update()` is a frame killer.** Cache, and refresh on an interval or on a
  scene/state change.
- Property access on interop types crosses into native each time — hoist chains out of loops, cache the
  component not the path.
- **Audio device handles are finite**: ~30 simultaneous `WaveOutEvent`s (NAudio) stall Windows audio and
  add ~10 s of startup latency before *anything* plays. Use **one `WaveOutEvent` + one
  `MixingSampleProvider`** per subsystem and add generators as mixer inputs, controlling each via its
  own `Volume`/`Pan`. Caveat of the shared mixer: sounds triggered on the same frame merge into one —
  stagger them by a few ms of `delaySamples` so they stay distinguishable.

## 9. Speech reads stale UI text

Patches often fire *before* the UI finishes populating, so you announce the previous screen's text. Two
mechanisms, used together:

- a small **delayed-speech queue** (~150 ms) so the text is read after the frame settles;
- a **specialised-over-generic priority flag** with a short block window (~0.5 s) so a specific handler
  (this exact button type) suppresses the generic one that would otherwise talk over it.

---

Related: [bepinex-harmony-il2cpp-rules.md](bepinex-harmony-il2cpp-rules.md) ·
[accessibility-patterns.md](accessibility-patterns.md) · [README](README.md)
