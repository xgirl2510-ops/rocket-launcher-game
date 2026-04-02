# Code Review — Rocket Launcher Unity 6 2D

**Date:** 2026-04-02
**Reviewer:** code-reviewer agent
**Scope:** All runtime + editor C# files

---

## Scope

**Files reviewed (14 total, ~1729 runtime + 828 editor = 2557 lines):**

Runtime:
- `Assets/Scripts/Rocket/Rocket.cs` — 126 lines
- `Assets/Scripts/Launch/LaunchController.cs` — 442 lines
- `Assets/Scripts/Launch/AimArrow.cs` — 52 lines
- `Assets/Scripts/Camera/CameraController.cs` — 273 lines
- `Assets/Scripts/Obstacles/ObstacleSpawner.cs` — 207 lines
- `Assets/Scripts/Effects/explosion-burst-particle-effect.cs` — 107 lines
- `Assets/Scripts/Effects/rocket-trail-particle-effect.cs` — 111 lines
- `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs` — 124 lines
- `Assets/Scripts/Audio/AudioManager.cs` — 79 lines
- `Assets/Scripts/Audio/ProceduralAudioClipGenerator.cs` — 208 lines

Editor:
- `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs` — 344 lines
- `Assets/Editor/rocket-launcher-scene-setup-environment-and-gameplay-objects.cs` — 161 lines
- `Assets/Editor/rocket-launcher-scene-setup-ui-canvas-and-hud-elements.cs` — 180 lines
- `Assets/Editor/rocket-launcher-scene-setup-shared-gameobject-and-sprite-helpers.cs` — 143 lines

---

## Overall Assessment

**Score: 7.4 / 10**

Solid prototype-quality code. The architecture is coherent, event wiring is clean, and the scene auto-setup tool is genuinely impressive. The game works. The main weaknesses are: `LaunchController` is a God Object carrying ~8 distinct responsibilities, two magic constants hardcoded in runtime files that are defined authoritatively in an editor-only file (coordination without a shared contract), a static mutable list on `RocketDebris` that persists across scene reloads, uncancelled `Invoke` calls that can cause null-ref crashes, and several minor runtime memory allocations.

---

## Per-Area Scores

| Area | Score | Notes |
|------|-------|-------|
| Architecture / Separation of Concerns | 6/10 | LaunchController is a God Object |
| Maintainability / File size | 7/10 | LaunchController 442 lines, should split |
| Code quality (YAGNI/KISS/DRY) | 8/10 | Very few redundancies; some duplicated drag-vector calc |
| Unity C# conventions | 8/10 | SerializeField, events, FixedUpdate/LateUpdate all correct |
| Potential issues / bugs | 6/10 | Static list leak, uncancelled Invoke, magic constant mismatch risk |

---

## Critical Issues (must fix)

### 1. `RocketDebris._allDebris` — static list leaks across scene reloads

**File:** `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs:18`

```csharp
private static readonly List<GameObject> _allDebris = new List<GameObject>();
```

`static` fields on `MonoBehaviour` are NOT cleared on scene reload (Domain Reload disabled is common in Unity 6). If the scene restarts without calling `ClearAll()`, or if `ClearAll()` is called after the GOs are already destroyed, the list grows unbounded and contains null entries.

**The `OnDestroy` guard (`_allDebris.Remove(gameObject)`)** is correct but depends on `Destroy()` being called before the scene is torn down — not guaranteed.

Fix: Register a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` hook to clear the list, or make it instance-based.

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState() => _allDebris.Clear();
```

---

### 2. Uncancelled `Invoke` calls — null-ref crash on rapid restart

**File:** `Assets/Scripts/Launch/LaunchController.cs:261, 263`

```csharp
Invoke(nameof(ReloadRocket), _reloadDelay);
// and
Invoke(nameof(ReloadAfterAutoPlay), _reloadDelay);
```

If the player clicks Restart before `_reloadDelay` expires, `HandleRestart()` resets the rocket but the pending `Invoke` still fires, calling `ReloadRocket()` which calls `_cameraController.ReturnToVehicle()` and `_rocket.ResetToPosition()` on an already-reset state. This is benign most of the time but:
- Can cause a double `ReturnToVehicle()` call launching two coroutines simultaneously in `CameraController`.
- If `_rocket` is null (e.g. destroyed), it's a null-ref.

Fix: Use `CancelInvoke(nameof(ReloadRocket))` at the top of `HandleRestart()` and `HandleAutoPlay()`.

---

### 3. Magic constant `GroundY = -5f` duplicated between editor and runtime

**File:** `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs:20`

```csharp
private const float GroundY = -5f; // must match GroundTop in scene setup
```

`GroundTop` is computed in the editor partial class as `CamBottom + GroundVisibleHeight = -7 + 2 = -5`. The runtime constant is hardcoded with a comment saying "must match." This will silently break if the camera layout changes. Same magic constant exists implicitly in `ObstacleSpawner._spawnMinY = -4f` (close but different).

Fix: Export `GroundTop` as a `const` in a shared `GameConstants.cs` file that both editor and runtime reference. Or read it at runtime from the actual `Ground` collider top edge.

---

## Important Issues (should fix)

### 4. `LaunchController` is a God Object — 442 lines, 8+ responsibilities

**File:** `Assets/Scripts/Launch/LaunchController.cs`

Single class handles: input, drag-to-force mapping, rocket launch, miss/hit callbacks, screen shake delegation, auto-play demo mode, hint UI visibility, stats UI, round/game state, target randomization trigger, camera event subscription, audio calls. It should be split:

Suggested split:
- `LaunchInputHandler` — mouse drag → normalized direction + force
- `GameStateManager` — round, shot counts, best score, win/restart flow
- `HintUIController` — angle/force text, autoplay button, miss threshold

This is the single biggest maintainability issue.

---

### 5. Duplicate drag-vector computation in `HandleTouchMoved` and `HandleTouchEnded`

**File:** `Assets/Scripts/Launch/LaunchController.cs:138-204`

Both methods repeat:
```csharp
Vector2 fingerWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
Vector2 spawnPos = _spawnPoint.position;
Vector2 dragVector = spawnPos - fingerWorldPos;
float dragDistance = dragVector.magnitude;
float clampedDistance = Mathf.Min(dragDistance, _maxDragDistance);
float normalizedForce = (clampedDistance - _minDragDistance) / (_maxDragDistance - _minDragDistance);
Vector2 launchDirection = dragVector.normalized;
```

Extract to `ComputeDragState(out Vector2 dir, out float normalizedForce, out float rawDistance)`.

---

### 6. `PanToTargetCoroutine` uses `CameraState.Intro` to block movement — wrong semantic

**File:** `Assets/Scripts/Camera/CameraController.cs:200`

```csharp
_currentState = CameraState.Intro; // block other movement
```

`PanToTarget` reuses `Intro` state to prevent other state transitions. This means if the game logic checks `_currentState == Intro`, it can't distinguish a real intro from a "look at target" pan. Add a `PanToTarget` state variant or a dedicated `CameraState.PanningToTarget` enum value.

---

### 7. `PanToTargetCoroutine` has a hardcoded `panDuration = 1.0f` local variable

**File:** `Assets/Scripts/Camera/CameraController.cs:202`

```csharp
float panDuration = 1.0f;
```

`_introPanDuration` and `_returnDuration` are [SerializeField] but this one is hardcoded. Inconsistent. Expose as `[SerializeField] private float _lookTargetPanDuration = 1.0f`.

---

### 8. `ObstacleSpawner.CreateSquareSprite` creates a new `Texture2D` per call, leaks memory

**File:** `Assets/Scripts/Obstacles/ObstacleSpawner.cs:191-206`

```csharp
private Sprite CreateSquareSprite()
{
    var existing = Resources.Load<Sprite>("ObstacleSquare");
    if (existing != null) return existing;

    Texture2D tex = new Texture2D(4, 4);
    ...
    return Sprite.Create(tex, ...);
}
```

`Resources.Load<Sprite>("ObstacleSquare")` will always return null (no such asset), so every obstacle spawns a new `Texture2D` + `Sprite`. With up to 15 obstacles, that is 15 untracked GPU textures that are never explicitly destroyed.

Fix: Cache the sprite as a `private static Sprite _obstacleSprite` field, create once, reuse.

```csharp
private static Sprite _obstacleSprite;

private Sprite GetOrCreateObstacleSprite()
{
    if (_obstacleSprite != null) return _obstacleSprite;
    // ... create once ...
    _obstacleSprite = Sprite.Create(tex, ...);
    return _obstacleSprite;
}
```

---

### 9. `CameraController.Start` uses `GameObject.Find` for fallback — fragile

**File:** `Assets/Scripts/Camera/CameraController.cs:58-65`

```csharp
var vehicle = GameObject.Find("LauncherVehicle");
var target = GameObject.Find("Target");
```

`GameObject.Find` is O(n) scene traversal and depends on exact string names. Fine for a prototype, but the editor tool already wires these references via `WireCameraController()`. The fallback will silently succeed or fail based on scene naming. If references are always wired, remove the fallback. If they must be optional, at least log a warning.

---

### 10. `AudioManager.PlayHitGround` and `PlayHitTarget` use the same `_boomClip`

**File:** `Assets/Scripts/Audio/AudioManager.cs:52-59`

```csharp
public void PlayHitGround()  { if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip); }
public void PlayHitTarget()  { if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip); }
```

`ProceduralAudioClipGenerator` already has `CreateGroundHit()` and `CreateTargetHit()` — distinct procedural clips. But `AudioManager` uses mp3 files and falls back to a single `_boomClip`. Either use procedural clips for both (DRY), or expose a separate `_targetBoomClip`. Currently the two methods are functionally identical — the distinction is unused.

---

### 11. `SetupSceneBatchMode` duplicates `SetupScene` body — DRY violation

**File:** `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs:67-111`

Both methods repeat 20 identical lines. Extract to a private `RunSetup()` method; `SetupScene` adds the dialog check, `SetupSceneBatchMode` adds TMP import + scene save.

---

## Minor Issues (nice to fix)

### 12. `DisableInput` calls `_aimArrow.Hide()` but `_aimArrow` is never null-checked

**File:** `Assets/Scripts/Launch/LaunchController.cs:438-440`

```csharp
public void DisableInput()
{
    _inputEnabled = false;
    _isDragging = false;
    _aimArrow.Hide(); // NullReferenceException if _aimArrow not assigned
}
```

`_rocket`, `_cameraController`, etc. are all null-checked. `_aimArrow` is not. Add `_aimArrow?.Hide()`.

---

### 13. `RocketDebris.Update` runs every frame for every debris piece

**File:** `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs:87-118`

Default 16 pieces × every frame. The grounded fade-out loop is especially wasteful since the `_grounded` check could be replaced with a `Destroy(gameObject, AutoDestroyDelay)` call and a simple coroutine for the alpha fade. Low priority given debris count.

---

### 14. `CameraController._shakeElapsed` advances in `SetCameraXY` via `Time.deltaTime`, not in `Update`

**File:** `Assets/Scripts/Camera/CameraController.cs:262-271`

`SetCameraXY` is called from coroutines (which also use `yield return null` = one frame) and from `LateUpdate`. The shake counter is advanced inside `SetCameraXY`, so it ticks correctly, but it's a subtle side effect embedded in a setter. Consider advancing shake elapsed in `LateUpdate` and just reading the offset in `SetCameraXY`.

---

### 15. `MissText` created in canvas but never referenced or shown

**File:** `Assets/Editor/rocket-launcher-scene-setup-ui-canvas-and-hud-elements.cs:28`

```csharp
CreateTMPLabel(canvasGO, "MissText", "MISS!", 60, "#FFFFFF", new Vector2(0, 200), new Vector2(600, 110));
```

No field in `LaunchController` for `_missText`. The element is created, stays inactive, and is never shown. Either wire it (show on miss for 0.5s) or remove it — YAGNI.

---

### 16. `ObstacleSpawner` trajectory math uses `dx` positively, breaks for negative X targets

**File:** `Assets/Scripts/Obstacles/ObstacleSpawner.cs:87`

```csharp
theta = Mathf.Atan2(v * v + Mathf.Sqrt(discriminant), g * dx);
```

If the target is to the left of the spawn (`dx < 0`), `g * dx < 0` → the angle calculation gives a negative/wrong result. The game currently always places the target to the right (`_targetMinX = 8f`), so this is latent, not active. Add a guard or comment documenting the assumption.

---

### 17. Naming convention inconsistency: kebab-case file names for runtime scripts

The project instructions require kebab-case for file names. `Rocket.cs`, `LaunchController.cs`, `AimArrow.cs`, `AudioManager.cs`, `CameraController.cs` use PascalCase (matching Unity class name requirement). The `effects/` and `audio/` subdirectory files use kebab-case (`rocket-trail-particle-effect.cs`). Unity **requires** the filename to match the class name for `MonoBehaviour` scripts — so `Rocket.cs` is correct. The kebab-case files work because their class names differ from the file names (e.g. `public class RocketTrail` in `rocket-trail-particle-effect.cs`). This is technically fine but inconsistent. Recommend PascalCase matching class name for all MonoBehaviour scripts.

---

## Positive Observations

- **Event-driven rocket lifecycle** (`OnRocketLaunched`, `OnRocketLanded`, `OnTargetHit`) is clean. Subscribers don't poll; state is pushed correctly.
- **`Rocket.cs`** is exemplary: 126 lines, single responsibility, correct physics lifecycle (Kinematic→Dynamic→Kinematic), `FixedUpdate` for physics, `MoveRotation` for interpolated rotation.
- **Editor partial class split** is well-executed: separate files for environment, UI, helpers. Layout constants defined once in `environment` partial and referenced by `SetupCamera()` — good single source of truth within the editor.
- **`ObstacleSpawner` trajectory math** — computing a genuine parabolic arc and safe zone is far above prototype quality.
- **Procedural audio** — `ProceduralAudioClipGenerator` is a nice self-contained static utility, no MonoBehaviour overhead, clean frequency math.
- **`AimArrow`** — 52 lines, does exactly one thing.
- **`RocketTrail`** — self-healing (`CreateTrailParticleSystem` fallback if no PS found) without being over-engineered.
- **`CameraController` state machine** — explicit enum, correct use of `LateUpdate` for follow, coroutines only for timed transitions. Clean.
- **No `Update` polling in event receivers** — audio, shake, camera transitions all fire once on event, not polled.

---

## Recommended Actions (Priority Order)

1. **Fix static `_allDebris` leak** — add `RuntimeInitializeOnLoadMethod` reset hook (Rocket.cs/RocketDebris.cs, ~3 lines).
2. **Cancel pending Invoke in HandleRestart/HandleAutoPlay** — prevents double-coroutine camera bug.
3. **Share `GroundTop` constant** — create `GameConstants.cs` with `GroundTop = -5f`; reference from `RocketDebris` and `ObstacleSpawner`.
4. **Fix `ObstacleSpawner.CreateSquareSprite` texture leak** — static cache, create once.
5. **Split `LaunchController`** — extract `GameStateManager` or at minimum inline hint/stats into a `HintUIController` helper component.
6. **Extract `ComputeDragState`** — remove repeated drag-vector code in Moved/Ended.
7. **Add `CameraState.PanningToTarget`** — remove semantic reuse of `Intro`.
8. **Expose `_lookTargetPanDuration` as SerializeField** — remove hardcoded 1.0f.
9. **Remove `MissText`** or wire it — YAGNI.
10. **Null-check `_aimArrow` in `DisableInput`**.
11. **Deduplicate `SetupScene` / `SetupSceneBatchMode`** — extract shared body to `RunSetup()`.

---

## Verdict: Is This "World Class"?

**No — but it's well above average for a solo prototype.**

What's missing for "world class":
- `LaunchController` must be decomposed. A 442-line God Object in a game this small is the clearest architectural debt.
- The `GroundY = -5f` runtime hardcode and missing `CancelInvoke` are actual bugs waiting to manifest, not stylistic issues.
- No unit tests (expected for game code, but worth noting).
- The static debris list leak is a correctness issue, not a style preference.

Fix issues 1-4 above and the code earns 8.5/10.

---

## Unresolved Questions

1. Is Domain Reload disabled in this project's Editor settings? If yes, the static `_allDebris` issue manifests on every Play press — critical priority.
2. `PlayHitGround` and `PlayHitTarget` both use `_boomClip`. Is this intentional (same sound for both events)?
3. `ObstacleSpawner._lastLaunchForce` stores raw velocity magnitude `v`, not a normalized 0-1 force. `HandleAutoPlay` passes it directly to `_rocket.Launch(dir, force)`. The `LaunchController._maxLaunchForce = 30f` but `v` can be much larger (fallback `sqrt(100)=10`, or higher for far targets). Does `Launch()` clamp force? — it does not. So auto-play may over-fire relative to player max force. Verify intended behavior.
