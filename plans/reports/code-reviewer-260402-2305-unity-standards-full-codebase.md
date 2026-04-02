# Code Review — Unity Standards Full Codebase

**Date:** 2026-04-02
**Reviewer:** code-reviewer
**Scope:** Full codebase — ALL scripts in `Assets/Scripts/` and `Assets/Editor/`
**Diff:** `d6e7c83` → `decf92f`

---

## Scope

**Files reviewed (14 scripts):**
- `Assets/Scripts/Effects/ground-scorch-mark.cs` (200 LOC)
- `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs` (147 LOC)
- `Assets/Scripts/Rocket/Rocket.cs` (142 LOC)
- `Assets/Scripts/Launch/LaunchController.cs` (385 LOC)
- `Assets/Scripts/Launch/launch-controller-hud-management.cs` (108 LOC)
- `Assets/Scripts/Camera/CameraController.cs` (267 LOC)
- `Assets/Scripts/Audio/AudioManager.cs` (94 LOC)
- `Assets/Scripts/Audio/ProceduralAudioClipGenerator.cs` (169 LOC)
- `Assets/Scripts/Core/GameConstants.cs` (7 LOC)
- `Assets/Scripts/Core/GameRoundTracker.cs` (43 LOC)
- `Assets/Scripts/Obstacles/ObstacleSpawner.cs` (223 LOC)
- `Assets/Scripts/Launch/AimArrow.cs` (52 LOC)
- `Assets/Scripts/Effects/explosion-burst-particle-effect.cs` (108 LOC)
- `Assets/Scripts/Effects/rocket-trail-particle-effect.cs` (112 LOC)
- `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs` (310 LOC)
- `Assets/Editor/rocket-launcher-scene-setup-environment-and-gameplay-objects.cs` (162 LOC)
- `Assets/Editor/rocket-launcher-scene-setup-shared-gameobject-and-sprite-helpers.cs` (143 LOC)
- `Assets/Editor/rocket-launcher-scene-setup-ui-canvas-and-hud-elements.cs` (179 LOC)

---

## Overall Assessment

Architecture is clean, event-driven, well-decomposed. The domain-reload-safe pattern (`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`) is correctly applied to all singletons and static state. No MissingReferenceException traps. No `Update` GetComponent calls. The codebase is above average for a Unity prototype.

**Real bugs found: 2 (one memory leak, one audio null-ref). Real anti-patterns: 4.**

---

## Critical Issues

### 1. TEXTURE LEAK — `BuildMaskSprite()` creates a new `Texture2D` every crater

**File:** `ground-scorch-mark.cs` lines 150–181
**Severity:** Critical (memory leak proportional to craters)

`BuildMaskSprite()` is called once per crater (`mask.sprite = BuildMaskSprite()`). Each call allocates a new 64×64 `Texture2D` and `Sprite`. Neither is ever destroyed. `ClearAll()` destroys the GameObjects but not the textures or sprites they reference. After multiple rounds the GPU VRAM grows unboundedly.

**Fix options:**

Option A (pre-bake N mask variants at startup):
```csharp
private static Sprite[] _maskSprites; // e.g. 8 variants
private static readonly int MaskVariants = 8;

private static void EnsureSprites()
{
    if (_holeSprite) return; // also check masks
    _holeSprite = BuildSolidSprite();
    _maskSprites = new Sprite[MaskVariants];
    for (int i = 0; i < MaskVariants; i++)
        _maskSprites[i] = BuildMaskSprite();
}
```
Then in `Spawn()`: `mask.sprite = _maskSprites[Random.Range(0, MaskVariants)];`

And in `ResetStaticState()` add destruction of textures before clearing:
```csharp
if (_maskSprites != null)
    foreach (var s in _maskSprites) if (s) DestroySprite(s);
_maskSprites = null;
```

Option B (one shared mask sprite): Remove per-crater jaggedness via Perlin, use a single shared mask. Less variety but zero leak.

Note: `_holeSprite` (`BuildSolidSprite`) is cached, so only 1 texture there — that is correct.

---

### 2. AUDIO NULL-REF — `PlayStretch()` / `PlayWin()` can throw if procedural clip generation fails

**File:** `AudioManager.cs` lines 68, 71

```csharp
public void PlayStretch() => _oneShotSource.PlayOneShot(_stretchClip);
public void PlayWin()     => _oneShotSource.PlayOneShot(_winClip);
```

Unlike `PlayHitGround()` and `PlayHitTarget()`, these two have no null guard. If `ProceduralAudioClipGenerator.Create*()` throws (e.g., allocation failure), `_stretchClip` / `_winClip` stay null and `PlayOneShot(null)` throws `UnityException`.

**Fix:**
```csharp
public void PlayStretch() { if (_stretchClip != null) _oneShotSource.PlayOneShot(_stretchClip); }
public void PlayWin()     { if (_winClip != null)     _oneShotSource.PlayOneShot(_winClip); }
```

---

## High Priority

### 3. STATIC STATE — `_groundPrepared` not reset on `ClearAll()`

**File:** `ground-scorch-mark.cs` line 139
`ClearAll()` is called on restart but does NOT reset `_groundPrepared`. This is benign currently because the ground is persistent. However, if the ground ever gets destroyed and recreated (e.g., scene reload without `SubsystemRegistration` firing), `PrepareGround()` is a no-op on the new ground because `_groundPrepared == true`.

`ResetStaticState()` does reset it — that fires on domain reload / Play Mode enter. Safe for current design, but fragile for future scene-reload paths.

**Fix:** Add `_groundPrepared = false;` to `ClearAll()`, or document the assumption explicitly.

---

### 4. PHYSICS / COROUTINE RACE — `ReturnToVehicle()` guard is insufficient

**File:** `CameraController.cs` lines 143–173

```csharp
public void ReturnToVehicle()
{
    if (_currentState == CameraState.Returning) return;
    StartCoroutine(ReturnToVehicleCoroutine());
}
```

If `HandleRocketMiss` fires immediately after a `PanToTarget` completes (state = `Idle`), the return coroutine starts correctly. But if `ReturnToVehicle()` is called while state is still `LookingAtTarget` (not `Returning`), a second coroutine starts — two coroutines both writing to `_currentState` and camera position simultaneously.

Similarly, `PlayIntro()` does `StartCoroutine(IntroCoroutine())` with no stop of any in-progress coroutine (only state-set).

**Fix:**
```csharp
private Coroutine _activeCoroutine;

public void ReturnToVehicle()
{
    if (_currentState == CameraState.Returning) return;
    if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
    _activeCoroutine = StartCoroutine(ReturnToVehicleCoroutine());
}
```
Apply same pattern to `PlayIntro()`, `PanToTarget()`.

---

### 5. DEBRIS FADE — debris pieces never fade out; they remain on screen indefinitely

**File:** `rocket-debris-shatter-effect.cs` lines 126–140

The comment in the class doc says "Pieces fade and self-destruct after landing." The `FixedUpdate` sets `_grounded = true` and snaps position — but there is no fade coroutine, no Destroy call, no alpha reduction. Pieces accumulate across shots until `ClearAll()`. Over a long game session with many misses, many debris `SpriteRenderer` draw calls stay active.

If `ClearAll()` were missed (e.g., a future code path), debris leaks permanently.

**Minor:** The `_allDebris` list is never pruned of grounded pieces — `OnDestroy` removes them, but grounded-but-alive pieces accumulate in the list.

**Fix option (minimal):** Destroy piece after landing + short delay:
```csharp
if (transform.position.y <= groundY)
{
    transform.position = new Vector3(transform.position.x, groundY, 0f);
    _grounded = true;
    Destroy(gameObject, Random.Range(3f, 6f)); // natural cleanup
}
```

---

### 6. OBSTACLE `_lastLaunchForce` MISMATCH — auto-play uses physics-bypass force

**File:** `ObstacleSpawner.cs` lines 99–100

```csharp
_lastLaunchDir = new Vector2(vx, vy).normalized;
_lastLaunchForce = v;
```

`v` is the raw speed used in the ballistic formula (can be e.g. 25+). `LaunchController._maxLaunchForce` is 30. But `Rocket.Launch()` applies `direction * force` as impulse — `force` here is `_lastLaunchForce = v`. When `v` > 30 (which happens for far targets), the force exceeds `_maxLaunchForce` the player can achieve. The auto-play demo may therefore look superhuman: the rocket travels further than the player can. Not a crash, but a gameplay UX inconsistency and a misleading hint.

**Fix:** Clamp `_lastLaunchForce` to `_maxLaunchForce`, or expose `_maxLaunchForce` as a parameter to `ObstacleSpawner`.

---

## Medium Priority

### 7. `ObstacleSpawner.CreateSquareSprite()` — second runtime texture creation path

**File:** `ObstacleSpawner.cs` lines 198–221

This creates a runtime `Texture2D` and `Sprite` in a `MonoBehaviour` that also has a `[RuntimeInitializeOnLoadMethod]` resetting `_cachedSquareSprite` to null. The texture/sprite are never explicitly destroyed. Domain reload resets the static ref, orphaning any previously created texture in VRAM. This is lower severity than GroundScorch because it only creates one texture per game session (not per event), but it's still a minor leak across Play Mode enters in the editor.

**Fix:** Wire in the editor-generated `square-100x100` asset through the existing serialized field, or add explicit `OnDestroy` cleanup like `AudioManager` does.

---

### 8. `LaunchController.HandleRestart()` double event subscription guard

**File:** `LaunchController.cs` lines 253–255

```csharp
_cameraController.OnIntroComplete -= OnIntroDone;
_cameraController.OnIntroComplete += OnIntroDone;
```

Pattern is correct (idempotent unsubscribe before subscribe). But `Start()` also subscribes at line 68. If `HandleRestart()` is called while the intro coroutine is still running (user spam-taps), `OnIntroDone` fires after `EnableInput()` is called from the previous restart's intro, potentially double-enabling input. Unlikely in practice but possible.

**Fix:** Guard on `_currentState == Intro` before restarting, or `DisableInput()` inside `OnIntroDone`.

---

### 9. `Camera.main` cached in `Awake()` but `CameraController` component is on the camera

**File:** `LaunchController.cs` line 52
`_camera = Camera.main` — fine. But if scene reload destroys and recreates the Camera between `Awake()` calls, the cached reference would be stale. With current architecture (single persistent scene) this is safe. Document the assumption or use `Camera.main` lazily if the scene is ever reloaded mid-game.

---

### 10. `AimArrow.UpdateArrow()` sets `localScale` every frame during drag

**File:** `AimArrow.cs` lines 48–50

```csharp
transform.localScale = new Vector3(transform.localScale.x, scaleY, 1f);
```

`transform.localScale.x` re-reads the x scale each frame instead of caching it. Tiny cost, but constructing `new Vector3` every `Update` call is an unnecessary allocation (struct, so no GC, but still an avoidable read-modify-write). More importantly: if another system changes `localScale.x` (e.g., the editor tool sets the AimArrow to 0.15f x), this code preserves it correctly — so the pattern is intentional. Fine as-is, just note the dependency.

---

### 11. `CameraController.SetCameraXY` runs shake timer even in coroutines

**File:** `CameraController.cs` lines 253–264

`_shakeElapsed += Time.deltaTime` runs whenever `SetCameraXY` is called, including from coroutines (pan/return). This means a shake triggered during an intro pan will apply shake offset to the pan lerp, which is correct behavior. But the shake state is never cleared when entering a new coroutine — a shake triggered right before `PlayIntro()` will apply shake during the entire 1.5s pan. Visually jarring. Low probability but worth noting.

**Fix:** Reset `_shakeElapsed = _shakeDuration` (or `= 0; _shakeDuration = 0`) in `PlayIntro()`.

---

### 12. `ObstacleSpawner` — obstacles tagged `"Ground"` but treated as obstacles

**File:** `ObstacleSpawner.cs` line 174

```csharp
go.tag = GameConstants.TagGround;
```

Comment says: "Rocket treats obstacle same as ground (stops on hit)." This is by design but has a side effect: `GroundScorch.Spawn()` in `Rocket.OnCollisionEnter2D` checks `transform.position.y < GameConstants.GroundTop + 1.5f` to skip aerial hits. If an obstacle is at y < -3.5 (near ground), a crater will incorrectly spawn on the ground when the rocket hits the obstacle. Test with low-placed obstacles.

---

## Low Priority

### 13. `ProceduralAudioClipGenerator` — `Random.value` in DSP loop

**File:** `ProceduralAudioClipGenerator.cs` lines 26, 86

`Random.value` (Unity's `UnityEngine.Random`) is used inside the DSP sample generation loop. This is fine because these clips are generated once at startup (not in real-time audio callbacks). But `System.Random` would be semantically clearer for non-gameplay noise generation, and avoids any future confusion with Unity's game random state.

---

### 14. `GameRoundTracker.TryUpdateBest` — first-round edge case

**File:** `GameRoundTracker.cs` line 28

```csharp
if (_bestScore == 0 || shots < _bestScore)
```

If a player somehow hits the target in 0 shots (impossible given current flow, but future-proofing), `_bestScore == 0` would be overwritten with 0, making `best` display "0" which `GetStatsText()` would correctly format. The `> 0` guard for display (`_bestScore > 0 ? ... : "--"`) handles it. Actually fine.

---

### 15. Editor tool — `GetTagManager()` creates new `SerializedObject` each call

**File:** `rocket-launcher-scene-auto-setup-editor-tool.cs` line 266–267

Called multiple times (EnsureTag loop, SetupSortingLayers, SetupLayer). Each call does `AssetDatabase.LoadAllAssetsAtPath` + `new SerializedObject`. Editor-only so no runtime impact, but these should share one `SerializedObject` per `RunCoreSetup()` call to avoid redundant disk reads.

---

### 16. `GroundScorch.OnDestroy` removes `gameObject` from `_allCraters` — but only MonoBehaviour instances call this

**File:** `ground-scorch-mark.cs` lines 195–198

The `GroundScorch` `MonoBehaviour` is never actually added as a component to any GameObject — `Spawn()` creates a bare `new GameObject("Crater")` and does NOT do `parent.AddComponent<GroundScorch>()`. So `OnDestroy()` **never fires**. This means the `OnDestroy` cleanup guard is dead code. The cleanup is handled correctly by `ClearAll()` instead, so there's no actual bug — but the dead `OnDestroy` is misleading.

**Fix:** Remove `OnDestroy()` from `GroundScorch`, or add `parent.AddComponent<GroundScorch>()` in `Spawn()` (which would make `OnDestroy` live). The same pattern exists in `RocketDebris.OnDestroy()` — but `RocketDebris` IS added as a component (line 88: `go.AddComponent<RocketDebris>()`), so that one is correct.

---

## Positive Observations

- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` applied correctly to every static field holder — proper domain reload safety.
- Event subscribe/unsubscribe paired in `Start`/`OnDestroy` throughout — no event memory leaks.
- `Camera.main` cached once in `Awake`, not called per frame.
- `_rb.MoveRotation()` for kinematic rotation in `FixedUpdate` — correct pattern.
- Physics in `FixedUpdate`, input in `Update`, camera in `LateUpdate` — all correct.
- `TryComputeDrag` out-parameter pattern is clean and avoids repeated math.
- Partial class split of `LaunchController` / `SceneSetupTool` is clean separation of concerns.
- `AudioManager.OnDestroy` explicitly `Destroy`s procedural `AudioClip`s — correct.
- `SerializedObject` + `FindProperty` used correctly in editor tools for `[SerializeField] private` fields.
- `GetComponentsInChildren` cached in `Awake` — no per-frame GetComponent.
- `ObstacleSpawner` uses `sqrMagnitude` for distance comparison in the overlap check — correct.

---

## Recommended Actions (Priority Order)

1. **[Critical]** Fix texture leak in `BuildMaskSprite()` — pre-generate N mask variants at startup and cache them in `_maskSprites[]`. Add destruction in `ResetStaticState()`.
2. **[Critical]** Add null guards to `PlayStretch()` and `PlayWin()` in `AudioManager`.
3. **[High]** Track active coroutine reference in `CameraController` — use `StopCoroutine` before starting new pan/return to prevent concurrent coroutine writes.
4. **[High]** Add auto-destruct to debris pieces on landing (e.g., `Destroy(gameObject, 4f)`).
5. **[High]** Clamp `_lastLaunchForce` in `ObstacleSpawner` to player's `_maxLaunchForce` range.
6. **[Medium]** Remove dead `OnDestroy()` from `GroundScorch` (it never fires — `GroundScorch` is never added as component).
7. **[Medium]** Clear shake state in `CameraController.PlayIntro()`.
8. **[Low]** Clean up `ObstacleSpawner._cachedSquareSprite` texture on domain reload (add Destroy call in `ResetStaticState`).

---

## Metrics

- **Scripts reviewed:** 18 files, ~2,550 LOC
- **Real bugs:** 2 (texture leak, audio null-ref)
- **Anti-patterns:** 4 (coroutine race, dead OnDestroy, force mismatch, debris no fade)
- **Dead code:** 1 (`GroundScorch.OnDestroy`)
- **Type safety issues:** 0
- **Missing event unsubscriptions:** 0
- **GetComponent in Update/FixedUpdate:** 0

---

## Unresolved Questions

1. Is multi-round session (many craters before restart) tested? The texture leak will be most visible there.
2. Is the auto-play force intentionally superhuman, or should it be capped to player-achievable force?
3. Future plan for mobile input (touch)? `Input.mousePosition` works for mouse and single touch but not multi-touch. If mobile is the target, `Input.GetTouch(0)` is preferred.
