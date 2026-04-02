# Code Review Summary — Unity Top-Level Standards (Round 3)

## Scope

- Files reviewed: 16 scripts (12 runtime + 4 editor)
- Lines of code analyzed: ~2,625
- Review focus: Full codebase, all scripts — Unity 6 2D C# best practices
- Plan: none (standalone review)

---

## Overall Assessment

Significant improvement from round 2 (6.6). Domain-reload safety is now universally applied, event cleanup is thorough, and `GameRoundTracker` extraction is clean. Main remaining issues are architectural (static `_allDebris` list with memory leak risk, `FindAnyObjectByType` fallback in `CameraController`, `Invoke` over coroutines for delays, raw string tags, physics applied in wrong context) and a few minor items. No critical security or data-loss issues.

**Score: 7.9 / 10**

---

## Critical Issues

None.

---

## High Priority Findings

### H1 — `RocketDebris._allDebris` static list: leaked Texture2D + Sprite per spawn
**File:Line:** `rocket-debris-shatter-effect.cs:53–59`
**Issue:** Each debris piece creates a `new Texture2D(4,4)` and `Sprite.Create(...)` at runtime, never destroyed. With `ClearAll()` only destroying the `GameObject`, the underlying `Texture2D` and `Sprite` objects leak to the unmanaged heap. After multiple rounds (each spawning 16+ pieces twice), VRAM and managed memory grow unboundedly.
**Fix:** Cache a single shared `Texture2D`/`Sprite` at class level (like `ObstacleSpawner._cachedSquareSprite`) or call `Destroy(tex)` / `Destroy(sprite)` inside `OnDestroy`. Simplest:

```csharp
// Top of RocketDebris
private static Texture2D _sharedTex;
private static Sprite _sharedSprite;

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState()
{
    _allDebris.Clear();
    _sharedTex = null;
    _sharedSprite = null;
}
```

Then reuse `_sharedSprite` inside `Spawn()` instead of creating per-piece.
**Unity Principle:** Memory Management — no per-frame/per-spawn heap allocations for GPU resources.

---

### H2 — `CameraController.Start()`: `FindAnyObjectByType` + `GameObject.Find` fallbacks
**File:Line:** `CameraController.cs:53–66`
**Issue:** `FindAnyObjectByType<Rocket>()` and `GameObject.Find("LauncherVehicle")` / `GameObject.Find("Target")` run in `Start()`. These are O(n) scene traversals and — more importantly — silently succeed with wrong objects if scene is not set up correctly. The editor tool already wires all references via `SerializedObject`; these fallbacks add complexity and hide misconfiguration.
**Fix:** Remove the fallback auto-find block entirely. If references are null, log a clear error and return:

```csharp
private void Start()
{
    if (_rocket == null || _vehicleTransform == null || _targetTransform == null)
    {
        Debug.LogError("[CameraController] Missing references — run Tools > Rocket Launcher > Setup Scene.");
        return;
    }
    // ...
}
```
**Unity Principle:** Performance (no `FindObjectOfType` outside editor tooling), SerializeField usage (references should be wired, not auto-discovered at runtime).

---

### H3 — `LaunchController`: uses `Invoke` for time-delayed logic
**File:Line:** `LaunchController.cs:258, 263`
**Issue:** `Invoke(nameof(ReloadRocket), _reloadDelay)` and `Invoke(nameof(ReloadAfterAutoPlay), _reloadDelay)` use string-keyed reflection. `CancelInvoke()` in `OnDestroy` handles cleanup, but `Invoke` cannot be awaited, cannot pass parameters, and is harder to reason about than a coroutine. `PanToTargetCoroutine` hard-codes `2f` seconds as a magic float on line 222 instead of using a serialised field.
**Fix:** Replace both `Invoke` calls with coroutines. Also extract the `2f` wait:

```csharp
// In LaunchController
[SerializeField] private float _lookTargetPauseDuration = 2f;

private IEnumerator ReloadAfterDelay(float delay)
{
    yield return new WaitForSeconds(delay);
    ReloadRocket();
}
```

And in `CameraController.cs:222`: replace `yield return new WaitForSeconds(2f)` with a `[SerializeField] private float _lookTargetPauseDuration = 2f;`.
**Unity Principle:** Unity API Usage — coroutines are the idiomatic Unity mechanism for time-delayed callbacks.

---

### H4 — `RocketDebris` physics in `Update` (should use `FixedUpdate`)
**File:Line:** `rocket-debris-shatter-effect.cs:109–113`
**Issue:** Custom gravity (`_velocity.y -= Gravity * Time.deltaTime`) and positional movement (`transform.position += _velocity * Time.deltaTime`) are frame-rate dependent physics updates running in `Update`. On low frame-rate devices the debris will travel further per frame; on high frame-rate it overshoots the ground check differently.
**Fix:** Move the physics update to `FixedUpdate` using `Time.fixedDeltaTime`:

```csharp
private void FixedUpdate()
{
    if (_grounded) return;
    _velocity.y -= Gravity * Time.fixedDeltaTime;
    transform.position += (Vector3)(_velocity * Time.fixedDeltaTime);
    transform.Rotate(0f, 0f, _angularSpeed * Time.fixedDeltaTime);
    if (transform.position.y <= GroundY) { /* ... */ }
}
```

The fade/timer logic can stay in `Update` since it's visual-only.
**Unity Principle:** Physics — physics simulation belongs in `FixedUpdate`.

---

### H5 — Raw string tags (`"Ground"`, `"Target"`) scattered across scripts
**File:Line:** `Rocket.cs:11–12`, `rocket-debris-shatter-effect.cs:23`, `ObstacleSpawner.cs:171`
**Issue:** Tag strings are duplicated: `"Ground"` appears in `Rocket.cs` (serialised field default), `GameConstants` does not include it, `ObstacleSpawner` hard-codes `go.tag = "Ground"` directly. A typo or rename breaks collision detection silently at runtime.
**Fix:** Add constants to `GameConstants`:

```csharp
public static class GameConstants
{
    public const float GroundTop = -5f;
    public const string TagGround = "Ground";
    public const string TagTarget = "Target";
}
```

Then: `go.tag = GameConstants.TagGround;` everywhere. The `[SerializeField] private string _groundTag` in `Rocket.cs` can be removed (or kept if intentional overridability is wanted — but then it should default from the constant).
**Unity Principle:** Code Organisation — single source of truth, no magic strings.

---

## Medium Priority Improvements

### M1 — `LaunchController` file is 469 lines — exceeds 200-line guideline
**File:Line:** `LaunchController.cs`
**Issue:** The file handles input, UI state, round tracking display, auto-play logic, and camera coordination. Per the project's own `development-rules.md`, files should stay under 200 lines.
**Fix:** Extract UI state management into a `LaunchUIController` helper (plain C# class or small MonoBehaviour) and auto-play logic into a separate method group or small class. The existing `GameRoundTracker` extraction pattern is the right model.

---

### M2 — `ObstacleSpawner._cachedSquareSprite`: static field survives domain reload but is non-null
**File:Line:** `ObstacleSpawner.cs:30–33`
**Issue:** `[RuntimeInitializeOnLoadMethod]` resets `_cachedSquareSprite = null` correctly. However, the sprite was created at runtime via `Sprite.Create` on a `Texture2D` that is also runtime-created (line 209). After a domain reload in the editor, the texture reference is garbage-collected but the static sprite reference (now null after reset) points to nothing — correct. But in a Play→Edit→Play cycle without domain reload (Unity 6 fast-enter-play), the stale `Texture2D` object may be collected while `_cachedSquareSprite` is still non-null (because `[RuntimeInitializeOnLoadMethod]` only fires on domain reload). The guard `if (_cachedSquareSprite != null)` on line 198 then returns a dead reference.
**Fix:** Use the same guard Unity recommends — check if the cached object is still valid:

```csharp
if (_cachedSquareSprite != null && _cachedSquareSprite)  // Unity null-check (operator== handles destroyed objects)
    return _cachedSquareSprite;
```

This works because Unity's `Object.operator==` returns `true` for destroyed/collected native objects.

---

### M3 — `CameraController.PanToTargetCoroutine` reuses `CameraState.Intro` to block input
**File:Line:** `CameraController.cs:200`
**Issue:** `_currentState = CameraState.Intro` is semantically wrong — it is a "LookTarget pan", not an intro. The state machine has no `LookingAtTarget` state, so `Intro` is hijacked. This makes `LateUpdate`'s switch statement misleading and would break if intro-specific behaviour is ever added.
**Fix:** Add `CameraState.LookingAtTarget` to the enum:

```csharp
public enum CameraState { Intro, Idle, Following, Landed, Returning, LookingAtTarget }
```

Then use it in `PanToTargetCoroutine`.

---

### M4 — `AudioManager`: procedural clips not `Destroy`-ed on `OnDestroy`
**File:Line:** `AudioManager.cs:46–49`
**Issue:** `_winClip`, `_stretchClip`, `_clickClip`, `_groundHitClip` are `AudioClip` objects created at runtime. They are not destroyed when the `AudioManager` is destroyed (scene change, editor stop). They will be garbage-collected eventually, but explicitly destroying them is cleaner and ensures no audio resources linger.
**Fix:**

```csharp
private void OnDestroy()
{
    Destroy(_winClip);
    Destroy(_stretchClip);
    Destroy(_clickClip);
    Destroy(_groundHitClip);
}
```

---

### M5 — `ProceduralAudioClipGenerator`: `Random.value` used in static class (non-deterministic)
**File:Line:** `ProceduralAudioClipGenerator.cs:25, 86`
**Issue:** `UnityEngine.Random.value` is called inside `CreateLaunchWhoosh()` and `CreateGroundHit()`. These are static methods with no seed control. This is fine at runtime but means the audio is different every play session. If deterministic audio is ever needed for testing, this is a blocker. Also, `Random.value` is `UnityEngine.Random`, not `System.Random`, which is acceptable here — just worth documenting.
**Fix (minor):** Add XML comment noting non-determinism. If determinism is needed in the future, accept a `System.Random rng` parameter.

---

### M6 — `Rocket.SetSpritesVisible` uses `GetComponentsInChildren` on every call
**File:Line:** `Rocket.cs:70`
**Issue:** `GetComponentsInChildren<SpriteRenderer>()` allocates a new array on every call. It is called in `ResetToPosition` and `OnCollisionEnter2D`/`OnTriggerEnter2D` — not per-frame, so not a hot-path issue. Still worth caching.
**Fix:** Cache in `Awake`:

```csharp
private SpriteRenderer[] _spriteRenderers;

private void Awake()
{
    _rb = GetComponent<Rigidbody2D>();
    _trail = GetComponent<RocketTrail>();
    _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
}
```

---

### M7 — `ObstacleSpawner.IsInSafeZone`: O(n*m) linear scan per candidate
**File:Line:** `ObstacleSpawner.cs:155–165`
**Issue:** For each of the `_obstacleCount * 20` candidate attempts, `IsInSafeZone` iterates all `_trajectorySteps` (30) trajectory points with `Vector2.Distance` (involves `Mathf.Sqrt`). That's up to `6 * 20 * 30 = 3600` sqrt operations per `RespawnObstacles` call. Acceptable for 6 obstacles at 30 steps, but worth noting if counts increase.
**Fix:** Use `sqrMagnitude` instead of `Distance` for comparison:

```csharp
float safeRadiusSq = _safeRadius * _safeRadius;
foreach (var tp in _safeTrajectory)
    if ((point - tp).sqrMagnitude < safeRadiusSq) return true;
```

---

## Low Priority / Nitpick

### L1 — `CameraController`: `Debug.Log` left in production path
**File:Line:** `CameraController.cs:119`
**Issue:** `Debug.Log("[CameraController] Intro complete — camera at vehicle.")` fires every round restart. In a shipped build, `Debug.Log` is compiled away if `DEVELOPMENT_BUILD` is not set — but this logs every round during development, cluttering the console.
**Fix:** Wrap in `#if UNITY_EDITOR` or remove.

---

### L2 — `rocket-launcher-scene-auto-setup-editor-tool.cs`: `SetupSceneBatchMode` duplicates `SetupScene` body
**File:Line:** `rocket-launcher-scene-auto-setup-editor-tool.cs:66–111`
**Issue:** `SetupSceneBatchMode` is a near-copy of `SetupScene` (lines 31–63) with 3 differences: no dialog, no `Undo.IncrementCurrentGroup`, adds `EditorSceneManager.SaveScene`. Violates DRY.
**Fix:** Extract the shared setup steps into a private `RunSetup()` method called by both public methods.

---

### L3 — `ObstacleSpawner.CreateObstacle`: `go.tag = "Ground"` hard-coded string
**File:Line:** `ObstacleSpawner.cs:171`
(Also covered in H5 above — listed here as a separate concrete call site for tracking.)

---

### L4 — `LaunchController.HandleRestart`: `CancelInvoke()` cancels all invocations including future ones
**File:Line:** `LaunchController.cs:287`
**Issue:** `CancelInvoke()` with no argument cancels all pending `Invoke` calls on this MonoBehaviour. This is correct behaviour here, but if more `Invoke` calls are added later (e.g., a different timed effect), they would be silently cancelled on restart. Using coroutines (H3 fix) eliminates this footgun entirely.

---

### L5 — `AimArrow.cs` file is in `Assets/Scripts/Launch/` but the review request listed it under `Assets/Scripts/Rocket/`
**Issue:** Minor organisation mismatch — `AimArrow` is a launch-input visual concern, so `Launch/` is actually more correct. Just a documentation discrepancy in the review spec.

---

### L6 — `ProceduralAudioClipGenerator.CreateWinJingle`: comment says "3 ascending tones" but 4 frequencies are used
**File:Line:** `ProceduralAudioClipGenerator.cs:101, 109`
**Issue:** Comment says "3 ascending sine tones" but `freqs` has 4 elements (C5, E5, G5, C6). Minor doc drift.
**Fix:** Update comment to "4 ascending sine tones (C5–C6 arpeggio)".

---

## Positive Observations

- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` applied to every static field — domain reload safety is now complete and consistent across `AudioManager`, `RocketDebris`, `ObstacleSpawner`.
- Event subscribe/unsubscribe is clean: all `OnDestroy` cleanups present in `LaunchController`, `CameraController`; `Button.onClick` listeners removed; no dangling delegate references.
- `GameRoundTracker` extraction to a plain C# class is textbook SRP — no MonoBehaviour overhead for pure data logic.
- `Rocket.cs` uses `MoveRotation` (not `transform.rotation =`) in `FixedUpdate` for physics-correct rotation — correct Unity physics pattern.
- Camera in `LateUpdate` with `SmoothDamp` — correct Unity camera pattern.
- Coroutines used correctly for timed camera pans (intro, return, look-target) — yield + elapsed pattern is clean.
- `SerializedObject` wiring in editor tools for private `[SerializeField]` fields — correct pattern for editor tooling.
- `Undo.RegisterCreatedObjectUndo` on every `CreateEmpty`/`CreateSprite` call — Undo registered properly.
- `GetComponentsInChildren` used with `AddComponent<>` result stored locally — no stale component lookups in the hot path.
- `GameConstants.GroundTop` used as single source of truth between runtime and editor tool — DRY respected.
- `ObstacleSpawner._cachedSquareSprite` with `RuntimeInitializeOnLoadMethod` — correct pattern for static sprite caching.
- Input handling is clean: `Update` for input, no physics calls in `Update` on the rocket itself.
- No public fields anywhere — all exposed via `[SerializeField] private`.

---

## Recommended Actions (Prioritised)

1. **[H1]** Fix `RocketDebris` Texture2D/Sprite leak — cache shared sprite, destroy on cleanup.
2. **[H4]** Move `RocketDebris` physics update to `FixedUpdate`.
3. **[H5]** Add `TagGround`/`TagTarget` constants to `GameConstants`, replace all magic tag strings.
4. **[H2]** Remove `FindAnyObjectByType` / `GameObject.Find` fallbacks from `CameraController.Start()`.
5. **[H3]** Replace both `Invoke` calls in `LaunchController` with coroutines; extract magic `2f` in `CameraController` to a `[SerializeField]`.
6. **[M1]** Split `LaunchController.cs` (469 lines) — extract UI state management.
7. **[M3]** Add `CameraState.LookingAtTarget` enum value; stop hijacking `CameraState.Intro`.
8. **[M4]** Add `OnDestroy` to `AudioManager` to destroy procedural clips.
9. **[M6]** Cache `GetComponentsInChildren<SpriteRenderer>()` in `Rocket.Awake`.
10. **[M7]** Replace `Vector2.Distance` with `sqrMagnitude` in `IsInSafeZone`.
11. **[L2]** Refactor `SetupScene`/`SetupSceneBatchMode` to share a private `RunSetup()` method.

---

## Metrics

- Type Coverage: 100% — all fields typed, no `var` misuse, no `object` casting
- Test Coverage: N/A (game project, no automated tests in scope)
- Linting Issues: 0 syntax errors, 0 compile-blocking issues
- Files over 200-line guideline: `LaunchController.cs` (469), `CameraController.cs` (287), `rocket-launcher-scene-auto-setup-editor-tool.cs` (344) — editor tool is intentionally large; `CameraController` is borderline acceptable; `LaunchController` is the clear outlier

---

## Unresolved Questions

1. Is there an intention to add `DontDestroyOnLoad` to `AudioManager`? Currently the singleton is scene-scoped, which is fine for a single-scene game — but if a loading scene is ever added, this will break.
2. `ObstacleSpawner._lastLaunchForce` stores the raw speed `v` (not normalised to `_maxLaunchForce`). When `HandleAutoPlay` passes it directly to `Rocket.Launch()`, the force can significantly exceed the inspector's `_maxLaunchForce` (30). Is this intentional for the auto-play demo?
3. `CameraController.Shake` accumulates shake by resetting `_shakeElapsed = 0` on every call. If `Shake` is called twice in quick succession (miss + another event), the first shake is overwritten. Is this intentional?
