# 01 — Coordinate System & Math Primitives

This is the foundation. Every other document assumes these conventions.

---

## 1. Vectors: `V2` / `V3`

`Core/Ian/V2.cs`, `V3.cs` — standard OpenTK-style structs (`public float X, Y[, Z]`). Nothing exotic;
the only members navigation uses heavily:

```csharp
// V2 (Core/Ian/V2.cs)
public float Length        => (float)Math.Sqrt(X*X + Y*Y);
public float LengthSquared => X*X + Y*Y;          // prefer for comparisons
public float LengthFast     // 1f / InverseSqrtFast(...) — used for footstep speed test
public V2 Normalized();     // returns a copy, unit length
public void Normalize();    // in place: num = 1/Length; X*=num; Y*=num;

// THE perpendiculars used to derive Left/Right from Front:
public V2 PerpendicularRight => new V2(Y, -X);   // +90° in screen (Y-down) space
public V2 PerpendicularLeft  => new V2(-Y, X);   // -90°

public static float Dot(V2 a, V2 b)     => a.X*b.X + a.Y*b.Y;
public static float PerpDot(V2 a, V2 b) => a.X*b.Y - a.Y*b.X;   // 2D cross (z of cross)
```

Equality is **exact float equality** (`==` calls `Equals`, compares X and Y with `==`). The radar and
movement code therefore use a separate epsilon comparison (below) for "almost equal".

```csharp
// CoreExtensionMethods.cs
public static bool AlmostEquals(this float f1, float f2) => Math.Abs(f1 - f2) <= 1e-5;
// SoundExtensionMethods.cs
public static bool AlmostEquals(this V2 a, V2 b) => a.X.AlmostEquals(b.X) && a.Y.AlmostEquals(b.Y);
public static bool AlmostEquals(this V3 a, V3 b) => …X &&…Y &&…Z;
// CoreExtensionMethods.cs
public static V3 AsV3(this V2 v) => new V3(v.X, v.Y, 0f);
```

> **Porting note:** in Unity/most engines you'll use `UnityEngine.Vector2`/`Vector3`. The only thing
> you must preserve is the **Y-down, compass-clockwise** convention used by the angle math below, or
> consistently flip it everywhere. Mixing conventions is the #1 source of "left/right is mirrored" bugs.

---

## 2. The compass-angle system

The world uses a **compass heading**: degrees clockwise, `0° = North`, and the world is **Y-down**
(`+Y` = South). Three conversion functions matter.

### Heading (degrees) → direction unit vector — the one you'll use most

```csharp
// MyMath.cs
public static V2 GetUnitVectorFromCompassDegrees(float degrees)
{
    float r = DegreesToRadians(degrees);
    float x =  (float)Math.Sin(r);
    float y = -(float)Math.Cos(r);     // NOTE the minus: Y-down world
    V2 v = new V2(x, y);
    v.Normalize();
    return v;
}
//   0° -> ( 0,-1) N     45° -> ( .707,-.707) NE     90° -> ( 1, 0) E
// 135° -> ( .707,.707) SE    180° -> ( 0, 1) S      270° -> (-1, 0) W
```

`VectorHelpers.cs` has the same math in three flavours (pick by your engine's Y direction):

```csharp
public static V3 FromCompassDegrees(float d)         // y = +cos  (Y-up compass)
public static V3 FromFlippedYCompassDegrees(float d) // y = -cos  (Y-down — what the game uses)
public static V3 FromMathDegrees(float d)            // standard math: x=cos, y=sin, 0=East CCW
```

### Direction unit vector → heading (degrees) — `GetCompassAngle` is NOT the inverse you want

```csharp
// VectorExtensions.cs
public static float GetCompassAngle(this V3 v)
{
    v.Z = 0f; v.Normalize();
    float deg = RadiansToDegrees((float)Math.Acos(v.Y));   // angle from +Y (north) axis
    return (Math.Asin(v.X) < 0.0) ? (360f - deg) : deg;    // disambiguate hemisphere by sign of X
}
```

> **Correction:** this function treats `+Y = North` — i.e. it's **Y-up**, the odd one out. It is the
> inverse of `VectorHelpers.FromCompassDegrees` (the Y-up flavour, `y = +cos`), **not** of the Y-down
> `GetUnitVectorFromCompassDegrees`/`FromFlippedYCompassDegrees` the rest of the game (and this doc)
> uses. Feed it a Y-down direction vector and you get a **mirrored** angle. Evidence:
> `VectorExtensions.cs:7-14` vs `MyMath.cs:35-43`. In practice this is harmless: `GetCompassAngle`'s
> **sole caller** is `VectorHelpers.MyTransform` (`VectorHelpers.cs:38`), which is itself used **only in
> sound-relative transforms** (`FPLogic.cs:234` `MakeRelativeToPlayerFacingAndPosition`;
> `MapSoundHandler.cs:216`) — **never** on the movement/heading path. The actual bearing readout (below)
> computes `atan2` directly instead of calling this function.
> If you need a real "vector → Y-down compass heading" inverse when porting, derive it yourself from
> `atan2`, don't reuse `GetCompassAngle`.

There is also `GetMathAngleInDegrees` (angle from +X, CCW) if you ever need standard math angles.

### The beacon bearing (used to say "45, NorthEast")

`FPExploring` computes the bearing from a delta vector with `atan2` directly:

```csharp
// vector pointing FROM target TO player (player - target)
double a   = Math.Atan2(toBeacon.X, -toBeacon.Y);          // X first, -Y second -> compass
float dir  = MyMath.RadiansToDegrees((float)a) + 180f;     // shift into [0,360)
RoughDirection rd = DirectionHelper.GetRoughDirectionEnum(dir);   // 8-point bin (Doc 07)
```

> **Why `atan2(X, -Y)`?** Standard `atan2(y, x)` gives the math angle (0=East, CCW). Passing
> `(X, -Y)` swaps/negates so that 0 means North and the angle grows clockwise — i.e. a compass
> bearing — for a Y-down world. Memorize this; you will reuse it for any "which way is target" readout.

---

## 3. Degrees / radians helpers

```csharp
// MyMath.cs
public static float DegreesToRadians(float d) => (float)(d * Math.PI / 180.0);
public static float RadiansToDegrees(float r) => (float)(r * 180f / Math.PI);
```

---

## 4. The eight direction vectors (player-relative)

`Sound/Ian/MapAndPlayer.cs` — the per-frame bundle handed to the radar. `Front` is precomputed from
the heading; the rest are derived. **This is the exact basis the radar and scans use.**

```csharp
public readonly V2 Front;     // = GetUnitVectorFromCompassDegrees(Facing)
public V2 Left      => Front.PerpendicularRight;          // (Front.Y, -Front.X)
public V2 Right     => Front.PerpendicularLeft;           // (-Front.Y, Front.X)
public V2 Back      => Front * -1f;
public V2 FrontLeft  => V2.Normalize(Front + Left);
public V2 FrontRight => V2.Normalize(Front + Right);
public V2 BackLeft   => V2.Normalize(Back + Left);
public V2 BackRight  => V2.Normalize(Back + Right);
```

Worked example, facing **East** (`Facing = 90°`, `Front = (1,0)`):
- `Left  = PerpendicularRight(1,0) = (0,-1)` → North. ✓ (East-facing player's left hand points North)
- `Right = PerpendicularLeft(1,0)  = (0, 1)` → South. ✓
- `FrontLeft = normalize((1,0)+(0,-1)) = (.707,-.707)` → NorthEast. ✓

> **Two derivations of the same basis (don't let it confuse you).** The literal `Perpendicular*`
> properties above are what the **sound module** uses (`Sound/Ian/MapAndPlayer.cs:19,23` —
> `Left => Front.PerpendicularRight`, `Right => Front.PerpendicularLeft`). The **navigation /
> first-person side** computes the identical basis via **cross products** instead:
> `Left => Cross(Front, Up).Xy`, `Right => Cross(Up, Front).Xy`
> (`FirstPerson/Ian/FPClientState.cs:39,43`, with `Up = (0,0,1)`). With that up vector,
> `Cross(Front, Up).Xy == (Front.Y, -Front.X) == PerpendicularRight` and
> `Cross(Up, Front).Xy == (-Front.Y, Front.X) == PerpendicularLeft` — algebraically the same result.
> They are two spellings of one convention, not two conventions; port either.

> If your engine is Y-up, either negate the Y of every direction vector once at the adapter boundary,
> or swap `Left`/`Right` definitions. Verify with the worked example above in your own frame before
> trusting any radar output.

---

## 5. World grid: tile size, storage & padding (missing from earlier pass — added)

- **Tile size = 1.0 world unit.** Tile `(i,j)` occupies `[i,i+1) × [j,j+1)`; `Center=(i+0.5, j+0.5)`,
  `Bounds=RectangleF(X,Y,1,1)` (`FPTile.cs:14-21`). `playerTile = (floor(X), floor(Y))`
  (`FPMapLogic.GetTile`, `FPMapLogic.cs:31`).
- **Tile storage is column-major**, not row-major: `TileCollection<T>`'s indexer is
  `this[x,y] => m_tiles[x*Height + y]` (`GameEngine/Ian/TileCollection.cs:21,28`). Only matters if you
  ever touch the backing array directly instead of `Get(x,y)`/`Set(x,y,..)`.
- **The map is padded, and every coordinate in the game lives in the padded frame.** `PaddedWidth =
  Width+2`, `PaddedHeight = Height+2` (`FPMapType.cs:357-359`). At load, a 1-tile **border ring** is
  force-set to a terrain looked up **by name** `"map boundary"` from the game's terrain data
  (`FPMapType.SetupTiles`, `FPMapType.cs:130-132`; padding at `:357-359`). **Its blocking behaviour is
  data-defined, not a hardcoded flag:** whether that ring is `Wall`/`Impassable` comes from the
  `"map boundary"` terrain's own data (see the Doc 03 §latent-bug note — a ray only terminates cleanly at
  the ring because that terrain happens to carry the `Wall` flag).
  The logical/authored map is tiles `[1..Width]×[1..Height]`; the map-review grid iterates `1..Width`
  and calls that "the edge of the map" (`FPMapGridBase.cs:60-108`). **All raycasters and bounds checks
  (Doc 03) test against the Padded dimensions**, so a ray or the player naturally stops at that border
  tile rather than at `Width`/`Height`. When porting, either mirror the padding (simplest — a ray always
  terminates cleanly) or make sure your own bounds checks account for the +1 offset this implies for any
  coordinate recovered from the source.

---

## 6. `Line` — analytic line / segment geometry

`Sound/Ian/Line.cs`. The raycaster and the radar's "open vs closed" test use this. A line is stored
in implicit form `A·x + B·y = C` plus its two defining points and a normalized `Slope` (a direction).

```csharp
public float A, B, C;          // implicit form A*x + B*y = C
public V3 Point1, Point2, Slope;   // Slope = normalize(Point2 - Point1)
public bool IsHorizontal => Slope.Y == 0f;
public bool IsVertical   => Slope.X == 0f;

public static Line FromPoints(V3 p1, V3 p2, bool isSegment)
{
    A = p2.Y - p1.Y;
    B = p1.X - p2.X;
    C = A*p1.X + B*p1.Y;
    Slope = V3.Normalize(p2 - p1);
}

public float SolveForX(float y) => (C - B*y) / A;   // x on the line at given y
public float SolveForY(float x) => (C - A*x) / B;   // y on the line at given x

// Sign test: is point c strictly left of the directed line P1->P2 ?  (2D cross product sign)
public bool IsPointLeftOfLine(V3 c)
    => (Point2.X-Point1.X)*(c.Y-Point1.Y) - (Point2.Y-Point1.Y)*(c.X-Point1.X) > 0f;

// Are two (infinite) lines collinear/parallel in either direction?
public bool Equivalent(Line o) => Slope.AlmostEquals(o.Slope) || Slope.AlmostEquals(o.Slope * -1f);
```

Two members are the workhorses elsewhere:
- **`SolveForX` / `SolveForY`** drive the grid raycaster (Doc 03): step across each integer gridline and
  solve for the crossing on the other axis.
- **`IsPointLeftOfLine`** drives the radar's open/closed decision (Doc 02): compare which side of the
  previous wall-edge line the player vs the new collision point fall on.

`DistanceToPoint`, `GetIntersection`, and segment-range clamping also exist but navigation doesn't use
them on the hot path.

---

## 7. Quick reference card

```
World:    tile (i,j) = [i,i+1)x[j,j+1) ; center (i+0.5, j+0.5) ; playerTile = (floor X, floor Y)
Storage:  column-major, m_tiles[x*Height + y]
Padding:  PaddedWidth/Height = Width/Height + 2 ; 1-tile "map boundary" border ring (terrain looked up
          by name; Wall/Impassable come from DATA, not hardcoded) ; ALL positions/indices in padded frame
Axes:     +X East, +Y South (Y-DOWN)
Heading:  compass degrees, clockwise, 0=N 90=E 180=S 270=W
deg->vec: ( sin, -cos ).normalize()                         [GetUnitVectorFromCompassDegrees]
vec->deg: acos(Y); if asin(X)<0 -> 360-that   [GetCompassAngle — Y-up odd-one-out, off hot path, NOT
                                                the inverse of the (Y-down) heading->vector fn above]
bearing:  deg = RadToDeg(atan2(dX, -dY)) + 180               [beacon / "which way" readout]
Left  = Front.PerpRight = (Front.Y, -Front.X)
Right = Front.PerpLeft  = (-Front.Y, Front.X)
Cardinals: N(0,-1) E(1,0) S(0,1) W(-1,0)
eps = 1e-5
```
