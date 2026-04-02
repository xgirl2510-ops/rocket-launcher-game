# Code Review Report — Final World Class Assessment (Round 3)

**Date:** 2026-04-02
**Reviewer:** code-reviewer subagent
**Previous scores:** Round 1: 6/10 → Round 2: 7.5/10

---

## Scope

- **Runtime scripts (15 files, 2091 lines):** `Assets/Scripts/`
- **Editor scripts (4 files, 829 lines):** `Assets/Editor/`
- **Total:** 19 files, 2920 lines
- Review focus: full codebase, round-3 regression check, world-class bar assessment

---

## Overall Assessment

The codebase has improved meaningfully since Round 2. All six reported regressions are resolved. Architecture is clean, separation of concerns is solid, Unity best practices are largely followed. What remains are a handful of real runtime risks and a few maintainability gaps — none catastrophic, but enough to keep it below 9/10 in two categories.

---

## Per-Category Ratings

### 1. Architecture & Organization — **9/10**

Strong. Event-driven rocket → manager → HUD pipeline is well thought out. `LaunchController` owns input only; `RoundManager` owns round flow; `CameraController` is self-contained. Editor tools are split into logical partial classes. `GameConstants` as single source of truth is correct. `GameRoundTracker` as plain C# (not MonoBehaviour) is the right call.

**One demerit:** `RoundManagerHUD` duplicates `_minLaunchForce`/`_maxLaunchForce` that are already owned by `LaunchController`. These two classes must stay in sync manually — a DRY violation with real maintenance risk.

---

### 2. Code Quality — **8/10**

Generally clean. Null guards are consistent. XML summaries on public API. `TryComputeDrag` naming follows the Try-pattern correctly. `DelayedAction` coroutine is a clean one-liner helper.

**Issues:**

- `RocketTrail.OnDestroy()` destroys `_cachedParticleMaterial` (a static field) from an instance method. If two Rocket instances ever exist (e.g. future pooling, or even the editor running twice without domain reload), the second instance will destroy the material the first is still using — then the field is left `null` but without re-initialization. The `RuntimeInitializeOnLoadMethod` resets it on domain reload, but not on `OnDestroy` at runtime mid-play. This is a latent bug.

  Fix: only destroy in `ResetStaticState`, not in `OnDestroy`. Or use `OnApplicationQuit` instead.

- `GroundScorch.ClearAll()` does NOT call `DestroySprites()` — meaning cached mask textures/sprites accumulate across `ClearAll()` calls within a single play session. Only `ResetStaticState()` (domain reload) destroys them. This is intentional for performance but undocumented — a reader will assume `ClearAll` is a full reset.

- `HandleRestart()` calls `StopAllCoroutines()` on `RoundManager` but does NOT call it on `CameraController`. If a camera coroutine is mid-flight when restart is hit, `StopActiveCoroutine()` on camera is only called indirectly via `PlayIntro()` → `StopActiveCoroutine()`. That path is fine — but the asymmetry is a readability trap.

- `ObstacleSpawner.CalculateTrajectory()` stores `_lastLaunchForce = v` where `v` is the raw physics velocity magnitude — but `LaunchController` uses 5–30 force range (impulse). When `HandleAutoPlay()` calls `_rocket.Launch(dir, force)` with `force = _lastLaunchForce`, it could be far outside the launch force range (e.g. `v` from ballistics formula can exceed 30 easily). This may produce wildly overpowered demo shots.

---

### 3. Unity Best Practices — **8.5/10**

Physics in `FixedUpdate`, input in `Update`, camera in `LateUpdate` — all correct. `Rigidbody2D.MoveRotation` for rocket rotation (not `transform.rotation`) — correct. `CollisionDetectionMode2D.Continuous` on rocket — correct. `Rigidbody2D.Interpolate` for smooth follow — correct. `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` for static reset — correct and the right load type.

**Issues:**

- `Camera.main` is called once in `Awake` and cached — good. But `LaunchController.TryComputeDrag()` calls `_camera.ScreenToWorldPoint(Input.mousePosition)` every frame during drag. `Input.mousePosition` returns `Vector3`; passing it to `ScreenToWorldPoint` without setting `.z` to the camera's near clip or a desired world-Z works in orthographic (Z doesn't matter for position) but is technically incorrect and will silently break in any perspective camera context. Minor but worth noting.

- `ObstacleSpawner` tags all obstacle GameObjects with `GameConstants.TagGround`. This means the rocket's `OnCollisionEnter2D` treats obstacles identically to the ground — which triggers `OnRocketLanded`, not a separate obstacle-hit event. This is a design choice (acceptable for now) but has zero code comment explaining it. Anyone adding obstacle-specific behavior will trip on this.

- `RocketDebris.FixedUpdate()` runs on every piece of debris every fixed frame. With 16+ pieces per impact and multiple impacts before `ClearAll()`, this can grow. Not pool-based. Acceptable for a prototype; worth flagging for production.

- `GroundScorch.BuildMaskSprite()` calls `Mathf.PerlinNoise` + `Mathf.Sqrt` in a nested 64×64 pixel loop (4096 iterations) per crater, synchronously on the main thread. Fine for 1–3 craters; hitches at scale.

- `ExplosionEffect.Spawn()` creates a new `GameObject` with `AddComponent<ParticleSystem>()` on every impact. No pooling. Acceptable for prototype.

---

### 4. Maintainability — **8/10**

Good XML docs on public methods. `GameConstants` centralizes tags and layout values correctly. Editor tool split into 4 partial files is clean. File naming is self-documenting (kebab-case for non-Unity files).

**Issues:**

- `RoundManagerHUD._minLaunchForce` / `_maxLaunchForce` (lines 31–32) are orphaned from `LaunchController`'s equivalent fields. If one changes, the hint display silently shows wrong values. Comment says "for hint display" but doesn't reference the source of truth. Should either inject `LaunchController` reference or expose a property from it.

- `ObstacleSpawner` exposes `SafeLaunchDirection` and `SafeLaunchForce` as public properties but they default to `Vector2.zero` / `0f` until `RespawnObstacles()` is called. `HandleAutoPlay()` guards with `if (dir.sqrMagnitude < 0.01f) return` — correct, but a stale/uninitialized read is silently discarded. A log warning would help in development.

- `RoundManager.HandleRestart()` re-subscribes `OnIntroComplete` with an explicit unsubscribe-then-subscribe pattern (lines 152–153). This is correct but fragile — if any other subscriber also subscribes to `OnIntroComplete`, the pattern doesn't compose. In a single-subscriber game this is fine.

- `CameraController` pan coroutines (`IntroCoroutine`, `PanToTargetCoroutine`, `ReturnToVehicleCoroutine`) all have duplicated `Vector2.Lerp` + `SmoothStep` + `elapsed` loop bodies. Minor DRY violation — a private `PanCoroutine(Vector2 from, Vector2 to, float duration, CameraState state)` helper would unify them cleanly.

---

### 5. Performance — **7.5/10**

Acceptable for a mobile prototype. Main concerns:

- **`GroundScorch.GetGroundY()`** is an O(n) linear scan over all craters, called inside `RocketDebris.FixedUpdate()` for every debris piece every fixed frame. With 3 craters × 16 debris pieces × 50 FPS fixed step = ~2400 calls/sec, each doing a full crater loop. Harmless at 3 craters; degrades at scale.

- **`ObstacleSpawner.IsInSafeZone()`** is called per candidate placement from `SpawnObstaclesAvoidingTrajectory()`. The trajectory array is `_trajectorySteps+1 = 31` points. With `_obstacleCount * 20 = 120` max attempts, worst case = 120 × 31 = 3720 distance checks. This runs once per round, not per frame — not a concern.

- **`BuildMaskSprite()`** 64×64 loop with `Mathf.Sqrt` and `Mathf.PerlinNoise` per pixel. Runs once per crater. At 3 craters per round, 8 variants each = 24 mask sprites built with 24×4096 = ~100K iterations. On a first-generation iPhone this may cause a brief hitch. Not a show-stopper.

- **`RocketDebris` `FixedUpdate()`** per-piece. Already noted above. Object pooling would be the proper fix for production.

- No GC pressure concerns found in hot paths. `Update()`/`LateUpdate()`/`FixedUpdate()` methods allocate nothing. Good.

---

### 6. Robustness — **7.5/10**

The biggest remaining real-world concern is here.

**Issues:**

**[HIGH] Auto-play force mismatch:** `ObstacleSpawner._lastLaunchForce` is set to raw ballistic velocity `v` (physics magnitude). This value is completely unclamped and bears no relationship to `LaunchController._maxLaunchForce = 30`. In practice the ballistic formula can produce `v` well above 30 for distant targets. When `HandleAutoPlay()` calls `_rocket.Launch(dir, v)`, the rocket is launched with raw unclamped physics velocity as an impulse force — far stronger than any player shot. The auto-play demo will fire a rocket noticeably faster/further than normal. Not a crash, but a visible gameplay inconsistency.

  Fix: clamp `_lastLaunchForce` to `[_minLaunchForce, _maxLaunchForce]` OR expose those bounds from `LaunchController` to `RoundManager` for clamping in `HandleAutoPlay()`.

**[MEDIUM] Obstacle collision with ground logic:** Obstacles are tagged `Ground`. If the rocket collides with an obstacle while `_isFlying = true`, `OnCollisionEnter2D` fires, `OnRocketLanded` is dispatched, and `HandleRocketMiss()` counts it as a miss. Correct behavior, but `GroundScorch.Spawn()` checks `transform.position.y < GameConstants.GroundTop + 1.5f` which could be true for a low obstacle, creating a crater floating mid-air. Unlikely in practice due to Y spawn range, but worth noting.

**[MEDIUM] `RocketTrail.OnDestroy()` destroys shared static material:** Described above. If domain reload is disabled (common in Unity 6 test builds), this fires during scene teardown and sets `_cachedParticleMaterial = null` without guard, meaning any subsequent access (e.g. if another scene loads) would call `Shader.Find` again — which can fail in builds if shader stripping removes `Sprites/Default`.

**[LOW] `SetupLayer(6, "Rocket")`** hardcodes physics layer 6 in the editor tool. Unity user layers start at 8; layers 0–7 are Unity built-ins except 3 (Ignore Raycast). Layer 6 is "Ignore Raycast" on some Unity versions, or unassigned on others. Setting layer 6 to "Rocket" may silently conflict with Unity's internal layer usage. Should use layer 8 or above.

**[LOW] `SetupSceneBatchMode()` uses `System.Reflection` to invoke a `NonPublic` internal TMP method.** This is fragile against TMP package updates. If `ImportProjectResourcesMenu` is renamed or removed, `importMethod?.Invoke()` silently does nothing — TMP setup is skipped without warning.

---

### 7. Scalability — **7/10**

The codebase is a tightly-coupled single-scene game, which is appropriate for the scope. The architecture would not scale gracefully to:

- Multiple scenes (no scene transition management)
- Multiple rockets simultaneously (static debris/scorch lists are global, `_cachedParticleMaterial` is shared)
- Mobile input (uses `Input.GetMouseButton` only — no `UnityEngine.InputSystem` or touch abstraction)
- Level progression (target/obstacle randomization is hardcoded in `RoundManager` with magic range constants, not data-driven)

None of these are problems for the current scope, but if the game grows, the static class pattern (`GroundScorch`, `RocketDebris` as static managers) will become the first friction point.

---

## Summary Scores

| Category | Round 1 | Round 2 | Round 3 |
|---|---|---|---|
| Architecture & Organization | 6 | 7.5 | **9.0** |
| Code Quality | 6 | 7.5 | **8.0** |
| Unity Best Practices | 5 | 7 | **8.5** |
| Maintainability | 6 | 7.5 | **8.0** |
| Performance | 6 | 7 | **7.5** |
| Robustness | 5 | 7 | **7.5** |
| Scalability | 5 | 7 | **7.0** |
| **Overall** | **6/10** | **7.5/10** | **8.0/10** |

---

## Remaining Issues by Priority

### High
1. **Auto-play force unclamped** — `_lastLaunchForce` from ballistic formula ≠ impulse force units. Clamp to `[minForce, maxForce]` or normalize to `[0,1]` and `Lerp` like player shots do.

### Medium
2. **`RocketTrail.OnDestroy()` destroys shared static material** — move to `ResetStaticState` only, or add instance guard.
3. **`RoundManagerHUD` duplicates launch force range** — inject from `LaunchController` or expose as public properties.
4. **`GroundScorch.ClearAll()` does not destroy textures** — add comment explaining this is intentional (textures reused within session), so it's not mistaken for a memory leak by future maintainers.

### Low
5. **Editor tool uses layer 6** — use layer 8+ for user-defined physics layers.
6. **`SetupSceneBatchMode()` reflection call has no fallback log on failure.**
7. **`CameraController` pan coroutines are duplicated** — refactor to shared `PanCoroutine` helper.
8. **Obstacle tag == Ground** — add inline comment explaining the collision intent.
9. **`Input.mousePosition.z`** not set before `ScreenToWorldPoint` — harmless in orthographic, cosmetically incorrect.

---

## Positive Observations

- Event unsubscription in `OnDestroy` is clean and complete across all subscribers.
- `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` used correctly everywhere static state exists.
- Coroutine race prevention via `_activeCoroutine` + `StopActiveCoroutine()` is solid.
- `MoveRotation` (not `transform.rotation`) for rocket rotation in `FixedUpdate` — correct.
- Editor tool uses `SerializedObject` for all `[SerializeField]` wiring — correct Unity way.
- `TryComputeDrag` returns early cleanly; no side effects on failure path.
- `GameRoundTracker` as plain C# class is clean data separation.
- `GroundScorch.GetGroundY()` enabling debris to fall into craters is a clever detail.
- All static classes properly handle `null` before `Destroy` calls in cleanup.

---

## Metrics

- Files reviewed: 19 (15 runtime + 4 editor)
- Lines analyzed: ~2920
- Critical issues: 0
- High issues: 1
- Medium issues: 4
- Low issues: 5
- Test coverage: N/A (no test suite present)

---

## Unresolved Questions

1. Is auto-play intended to demonstrate a "perfect" shot at normal player force? If so, the force clamping fix is required for correctness, not just polish.
2. Is there a plan for mobile touch input (`UnityEngine.InputSystem`)? The current `Input.GetMouseButton` approach works on mobile via Unity's mouse simulation but bypasses multi-touch.
3. Is domain reload disabled in this project's Editor settings? If yes, the `RocketTrail.OnDestroy` material destruction risk is active during iterative testing in the Editor.
