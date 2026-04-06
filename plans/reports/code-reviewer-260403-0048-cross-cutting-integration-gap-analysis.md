# Code Review — Cross-cutting Concerns, Integration & Missing Features

**Date:** 2026-04-03
**Reviewer:** code-reviewer agent
**Prior score:** 8.0/10 (round 4 baseline)
**Scope:** All 22 C# files — Scripts/, Editor/, Tests/Editor/

---

## Code Review Summary

### Scope
- Files reviewed: 22 C# files across `Assets/Scripts/`, `Assets/Editor/`, `Assets/Tests/Editor/`
- Lines of code analyzed: ~1,700 (runtime ~1,200, editor ~500, tests ~175)
- Review focus: Cross-script integration, edge cases, build safety, configuration rigidity, missing production features
- Updated plans: `plans/reports/roadmap-260403-0010-path-to-10.md` (see bottom)

### Overall Assessment
The codebase is clean, consistent, and well-structured for its scope. The previous four review rounds eliminated the most common Unity pitfalls (texture leaks, coroutine races, missing unsubscribes, magic numbers). What remains blocking 10/10 falls into three categories: **integration correctness bugs** (real defects), **build-platform safety gaps** (will fail or behave wrongly on WebGL/mobile), and **production-quality gaps** (already documented in the roadmap but now with more precise file/line citations).

---

## Critical Issues

### C1 — `CameraScreenShake.GetOffset()` advances time in `LateUpdate` via side-effect
**File:** `Assets/Scripts/Camera/camera-screen-shake.cs` line 28–33
**Issue:** `GetOffset()` is called once per frame from `CameraController.SetCameraXY()`. It mutates `_elapsed` inside a getter. If `SetCameraXY` is ever called more than once per frame (pan coroutines call it every yield, and `LateUpdate` also calls it during Following state), `_elapsed` advances twice per frame, cutting shake duration in half and producing erratic decay.
**Concrete path:** `ReturnToVehicleCoroutine` calls `PanCoroutine` which calls `SetCameraXY` every `yield return null`, and if shake fires during `Returning` state, `LateUpdate` does NOT call `FollowRocket` — so this specific path is currently safe. But if `CameraState.Following` is active and `Shake()` is called simultaneously (e.g., a hypothetical future hit-while-flying), both `FollowRocket` (LateUpdate) and a coroutine frame could both call `SetCameraXY`. The design is fragile and relies on implicit state-machine exclusivity that is not enforced.
**Fix:** Separate time-advance from value-reading. Use an `Update` or `LateUpdate` in `CameraScreenShake` to advance `_elapsed`, and make `GetOffset()` a pure read.

### C2 — `RocketDebris._allDebris` list is never cleared of destroyed entries during gameplay
**File:** `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs` lines 35, 96–97, 127–129
**Issue:** `_allDebris` is a static list. `ClearAll()` is called on restart. `OnDestroy()` removes `gameObject` from the list. However, `Destroy(gameObject, delay)` is NOT used — debris pieces self-destruct only when `_grounded` but there is no `Destroy` call after landing; pieces remain in the scene indefinitely until `ClearAll()`. On a long play session (many misses without restart), the list and scene fill up unboundedly with grounded, invisible debris GameObjects.
**Fix:** Call `Destroy(gameObject, fadeDelay)` after grounding in `FixedUpdate`, or add a fade-and-destroy coroutine. This is the primary unbounded GC/memory growth path in the game.

### C3 — `LaunchController` uses legacy `Input` — broken on WebGL mobile and touch devices
**File:** `Assets/Scripts/Launch/LaunchController.cs` lines 48–53
**Issue:** `Input.GetMouseButtonDown(0)` works on desktop and simulates touch on mobile only when exactly one finger is down. On WebGL (mobile browser), multi-touch scenarios, or when a second finger hits the screen mid-drag, the drag can silently abort or fire a spurious launch. `Input.mousePosition` is not the same as `Input.GetTouch(0).position` — on some Android devices they diverge under certain conditions.
**Fix:** Migrate to `UnityEngine.InputSystem.EnhancedTouch` or at minimum use `Input.touchCount > 0 ? Input.GetTouch(0).position : Input.mousePosition`. This is the primary mobile correctness issue.

---

## High Priority Findings

### H1 — `ObstacleSpawner`: safe trajectory may not match actual rocket flight after force clamp
**File:** `Assets/Scripts/Obstacles/ObstacleSpawner.cs` lines 73–109
**Issue:** `CalculateTrajectory` clamps velocity to `[MinLaunchForce, MaxLaunchForce]` then re-solves the angle. The stored `_lastLaunchForce = vClamped` and `_lastLaunchDir` are correct for the clamped case. However, the trajectory arc is computed with `vx = vClamped * cos(theta)` and `totalTime = |dx| / max(vx, 0.1)`. When `dx` is large (target at X=35) and `vClamped = 30`, `theta` from the high-arc formula can approach 90°, making `vx` near-zero, and `totalTime = dx / 0.1 = 250s`. The trajectory array then spans 250 simulated seconds — all points pile up near the start and the safe zone is incorrectly concentrated at the launch point, not along the actual arc.
**Fix:** Clamp `totalTime` to a reasonable maximum (e.g., `3 * Mathf.Sqrt(2 * peakHeight / g)`), or use the low-arc angle solution when the high-arc becomes degenerate.

### H2 — `GroundScorch.PrepareGround()` uses `GameObject.FindWithTag` every first call — order-sensitive
**File:** `Assets/Scripts/Effects/ground-scorch-mark.cs` lines 54–61
**Issue:** `PrepareGround()` calls `GameObject.FindWithTag("Ground")` at runtime impact time. If Ground is not yet active (e.g., a scene-load race), it silently skips mask setup and `_groundPrepared` is set to `false`... wait, it checks `if (_groundPrepared) return` and only sets it `true` after success. So a failed `FindWithTag` leaves `_groundPrepared = false`, which means subsequent impacts retry — this is actually resilient. **However**, the `maskInteraction` setting mutates the Ground SpriteRenderer state permanently. If `ClearAll()` is called, the craters are removed but the ground's `maskInteraction` is NOT reset to `None`. This means after restart the ground still has `VisibleOutsideMask` set with no active masks, which is harmless visually but inconsistent state. Additionally, `_groundPrepared` is reset by `RuntimeInitializeOnLoadMethod` but NOT by `ClearAll()`. After a restart with no domain reload (Play Mode in Editor), `_groundPrepared = true` is stale, and `PrepareGround()` skips re-finding the Ground if it was re-created. This is fine in production builds (domain reload always happens) but breaks "Setup Scene + Play again" in Editor without closing.
**Fix:** Either reset `_groundPrepared = false` in `ClearAll()`, or undo the maskInteraction on clear.

### H3 — `AudioManager` singleton does not persist across scenes; not `DontDestroyOnLoad`
**File:** `Assets/Scripts/Audio/AudioManager.cs` lines 31–38
**Issue:** `AudioManager` has standard singleton guard but no `DontDestroyOnLoad`. If a scene reload or additive scene load ever happens, a new `AudioManager` Awake would find `Instance != null` (if the old one survived) and destroy itself — or `Instance` would be set to the new one leaving the old thrustSource looping. Currently the game is single-scene so this does not trigger, but it is an architectural assumption that will break if scene management is added.
**Fix:** Add `DontDestroyOnLoad(gameObject)` to `AudioManager.Awake()`, or document the single-scene constraint explicitly. Same issue applies to `RoundManagerHUD`.

### H4 — `Rocket` collision handler fires on `_groundTag` obstacles too
**File:** `Assets/Scripts/Rocket/Rocket.cs` lines 94–112
**Issue:** `OnCollisionEnter2D` checks for `_groundTag` = `"Ground"`. `ObstacleSpawner.CreateObstacle()` sets `go.tag = GameConstants.TagGround` (line 167 of `ObstacleSpawner.cs`). Therefore, hitting any obstacle triggers the miss handler, spawns an explosion, debris, and fires `OnRocketLanded`. This is probably intentional (obstacles kill the rocket), but:
1. `GroundScorch.Spawn` is called with `position.y < GroundTop + 1.5f` — if an obstacle is at Y=5, this check correctly skips the crater, so no visual bug.
2. However, `RocketDebris.SpawnDirtDebris` is only spawned inside `GroundScorch.Spawn`. An obstacle hit at Y=5 creates debris but no dirt chunks, which looks inconsistent vs. a ground hit.
3. The obstacle collision does NOT call `other.gameObject.SetActive(false)` unlike the target hit — the obstacle stays visible while the rocket "passes through" it after `_isFlying = false`. This produces a visual glitch where the rocket sprite hides but the obstacle remains, and subsequent physics can still push debris through it.
**Fix:** Document that obstacles = ground-tag is intentional. Add `collision.gameObject.SetActive(false)` for non-ground-plane obstacles if desired, or differentiate via layer check.

### H5 — `RoundManager.Start()` subscribes to `OnIntroComplete` but partial class also re-subscribes in `HandleRestart()`
**File:** `Assets/Scripts/Core/RoundManager.cs` line 62 + `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs` lines 39–41
**Issue:** `Start()` subscribes `_cameraController.OnIntroComplete += OnIntroDone`. `HandleRestart()` does `OnIntroComplete -= OnIntroDone` then `+= OnIntroDone` (defensive pattern, correct). `OnDestroy()` unsubscribes `OnIntroComplete -= OnIntroDone` AND `OnLookTargetComplete -= OnLookTargetDone`. **But `OnLookTargetComplete` is only subscribed in `HandleLookTarget()`, never in `Start()`** — so `OnDestroy` blindly unsubscribes an event that may never have been subscribed, which is harmless (C# -= on a null delegate is a no-op) but indicates inconsistent subscription management. More importantly: if `HandleLookTarget()` fires, then `OnDestroy` fires before `OnLookTargetDone` is called (e.g., rapid scene destroy during pan), `OnLookTargetComplete` is unsubscribed in `OnDestroy` — this is actually correct and safe. **Real gap:** `HandleAutoPlay()` calls `_launchController.DisableInput()` but does NOT re-subscribe `OnIntroComplete`. After autoplay completes, `ReloadAfterAutoPlay()` calls `_cameraController.ReturnToVehicle()` and directly `_launchController.EnableInput()` — no intro pan. This is intentional by design (autoplay resets without intro). Documented here for completeness rather than a bug.

### H6 — No `Camera.main` null-check after scene operations
**File:** `Assets/Scripts/Launch/LaunchController.cs` line 32
**Issue:** `_camera = Camera.main` is called in `Awake()`. `Camera.main` is a relatively expensive `FindObjectWithTag` call. More importantly, it is cached once and never refreshed. If the camera is destroyed and recreated (which happens in the Editor after "Setup Scene"), the cached reference is a destroyed object. Any subsequent `_camera.ScreenToWorldPoint` throws `MissingReferenceException`. In production this is fine (no camera recreation at runtime), but in the editor workflow this is a footgun.
**Fix:** Cache with a null check in `Update` or use `[SerializeField] private Camera _camera` wired by editor tool.

---

## Medium Priority Improvements

### M1 — `ExplosionEffect` and `RocketDebris` `Spawn()` allocate new `GameObject` every call — unbounded GC
**Files:** `explosion-burst-particle-effect.cs` line 29, `rocket-debris-shatter-effect.cs` line 76
**Issue:** Each impact spawns 30 debris GameObjects + 1 explosion GameObject with a new `ParticleSystem`. `new GameObject()` triggers GC pressure. On a slow device with rapid misses, this stacks. `ExplosionEffect` self-destructs after `_particleLifetime + 0.2f` which is fine. `RocketDebris` does NOT self-destruct (see C2).
**Fix:** Pool both `ExplosionEffect` and `RocketDebris`. A `Stack<GameObject>` pool in each static class is sufficient.

### M2 — `RocketTrail.CreateTrailParticleSystem()` creates a child `GameObject` but has no corresponding teardown
**File:** `Assets/Scripts/Effects/rocket-trail-particle-effect.cs` line 52
**Issue:** `TrailParticles` child is created in `Awake()` if no existing `ParticleSystem` found. The child is parented to the Rocket, so it is destroyed when Rocket is destroyed — no leak. However, `ResetToPosition()` in `Rocket.cs` calls `_trail.ClearTrail()` which stops and clears particles but does NOT destroy the trail child. This is correct, but if `CreateTrailParticleSystem()` is called on a pre-existing Rocket that already has a `TrailParticles` child from a previous run (Editor: "Setup Scene" without full scene clear), it won't find an existing PS via `GetComponentInChildren` because... actually it will find it. This path is safe. Low risk, documented for completeness.

### M3 — `ObstacleSpawner` layer omission (known from roadmap)
**File:** `Assets/Scripts/Obstacles/ObstacleSpawner.cs` line 165
**Issue:** `CreateObstacle()` does not set `go.layer`. Obstacles are on layer 0 (Default). The Rocket is on layer 8 ("Rocket"). If Physics 2D Layer Collision Matrix is configured to ignore Default↔Rocket, rockets would pass through obstacles silently. Currently Default↔Rocket collision is enabled (no explicit ignore), so it works. But it is fragile.
**Fix:** Set `go.layer = LayerMask.NameToLayer("Ground")` or dedicate a layer. Already in roadmap.

### M4 — `CameraController.SetCameraXY` is called with shake offset applied, but ortho zoom is NOT shake-corrected
**File:** `Assets/Scripts/Camera/CameraController.cs` lines 246–252
**Issue:** `SetCameraXY` adds `_shake.GetOffset()` to position. `FollowRocket()` also updates `_camera.orthographicSize` (zoom). The zoom is not affected by shake, which is correct. However, during `PanCoroutine` the orthographic size is lerped via `_camera.orthographicSize = Mathf.Lerp(...)` independently of `SetCameraXY`. If `Shake()` is called while a pan coroutine is running (e.g., hit during intro pan — which cannot currently happen, but could in future), `GetOffset()` is called from `SetCameraXY` inside `PanCoroutine`, advancing `_elapsed` in the middle of a pan. See C1.

### M5 — `GameRoundTracker.TryUpdateBest` is called with `_roundTracker.RoundShots` in `HandleTargetHit`, not with the total shot count
**File:** `Assets/Scripts/Core/RoundManager.cs` line 92
**Issue:** `_roundTracker.TryUpdateBest(_roundTracker.RoundShots)` — this passes the current round's shot count, which is correct for "fewest shots to win." However, `RoundShots` at the moment of `HandleTargetHit` reflects shots fired this round, and `OnShotFired()` is called BEFORE `Launch()` in `LaunchController.HandleTouchEnded()` (line 101–104). So the winning shot is counted before the win event fires — `RoundShots` is correct. No bug, but the asymmetry (shot counted before event, best evaluated inside event) is a latent ordering dependency.

### M6 — `ProceduralAudioClipGenerator` uses `Random.value` inside audio generation
**File:** `Assets/Scripts/Audio/ProceduralAudioClipGenerator.cs` line 34
**Issue:** `CreateGroundHit()` uses `Random.value` (Unity's `UnityEngine.Random`) to generate white noise. This is called in `AudioManager.Awake()` during scene initialization. While harmless in most cases, it consumes the Unity random stream, which can affect reproducibility of tests and gameplay if any code relies on `Random.state` being deterministic.
**Fix:** Use `System.Random` for audio generation, or use a seeded `System.Random` instance. Minor concern for a game this scale.

### M7 — `GroundScorch.BuildMaskSprite()` allocates 8 × 64×64 Texture2D on first crater — on main thread
**File:** `Assets/Scripts/Effects/ground-scorch-mark.cs` lines 164–193
**Issue:** `EnsureMaskVariants()` builds 8 textures of 4096 pixels each (64×64) on first crater spawn, all on the main thread with `SetPixel` per-pixel loops (4096 iterations × 8 = 32,768 `SetPixel` calls). On mobile this causes a frame hitch on the first impact. Subsequent impacts are fast (cached).
**Fix:** Pre-generate masks at scene load in a coroutine, or replace the per-pixel loop with `GetRawTextureData` + `NativeArray` fill.

### M8 — `HandleRestart` calls `StopAllCoroutines()` which also stops `DelayedAction` mid-flight
**File:** `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs` line 16
**Issue:** If the player clicks Restart while the `_reloadDelay` coroutine (`DelayedAction`) is running, `StopAllCoroutines` cancels it. The next line re-activates the rocket and target and resets state — this is intentional and correct. No bug. However, if `ReloadRocket` or `ReloadAfterAutoPlay` was cancelled mid-execution (not mid-delay), state could be inconsistent. Since `DelayedAction` waits first and then calls the action, and `StopAllCoroutines` stops the wait, the action never fires — correct behavior.

### M9 — `sorting layer "Gameplay"` used by obstacles but not listed in SetupSortingLayers
**File:** `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs` line 298
**Issue:** `SetupSortingLayers()` creates: `Background`, `Environment`, `Gameplay`, `Projectile`. `ObstacleSpawner.CreateObstacle()` assigns `sr.sortingLayerName = "Gameplay"`. This is correct and consistent. No bug. But the `RoundManagerHUD` places itself into the `--- INPUT ---` parent GameObject — a naming inconsistency (HUD is UI, not input). Low impact.

---

## Low Priority Suggestions

### L1 — `RoundManagerHUD.Instance` accessed via `?.` null-conditional throughout codebase — hides wiring errors
**Files:** `RoundManager.cs` lines 94–95, 129, `LaunchController.cs` line 85, `round-manager-auto-play-restart-and-target.cs` lines 22–23
**Issue:** Every call to `RoundManagerHUD.Instance` uses `?.` which silently no-ops if HUD is missing. This is defensive and correct for production but means a missing HUD wiring produces no console error and the HUD simply doesn't update. Should log a warning at least once if Instance is null when first accessed.

### L2 — `CameraController._defaultZ` is set in `Awake` but scene setup always places camera at Z=-10
**File:** `Assets/Scripts/Camera/CameraController.cs` line 61
**Issue:** `_defaultZ = transform.position.z` captures the camera Z at Awake time. If the editor tool places the camera at `z=-10`, this always works. But the value is hardcoded nowhere — it relies on the GameObject's inspector Z. This is correct design (reads actual position) but is fragile if someone accidentally moves the camera in Z.
**Fix:** Add `GameConstants.CameraDefaultZ = -10f` and validate in `OnValidate`.

### L3 — `ObstacleSpawner` trajectory calculation uses `Physics2D.gravity.magnitude` but gravity could be changed
**File:** `Assets/Scripts/Obstacles/ObstacleSpawner.cs` line 65
**Issue:** `Physics2D.gravity.magnitude` is dynamic — if gravity changes (e.g., a future "moon level"), the trajectory calculation updates correctly. `Rocket.cs` uses `Rigidbody2D` which also reads `Physics2D.gravity`, so they stay in sync. This is actually correct design. Noted positively.

### L4 — `WinJingle` uses per-note `t` normalized to note duration, not to total time
**File:** `Assets/Scripts/Audio/ProceduralAudioClipGenerator.cs` lines 64–68
**Issue:** `float t = (float)i / noteSamples` resets to 0 at each note start, so the sine phase also resets to 0. This creates a clean note-start transient (click) on each note transition. For 4 quarter-notes this is audible as a slight pop. Minor audio quality issue.
**Fix:** Accumulate `phase` continuously or use `(float)(n * noteSamples + i) / samples` for `t`.

### L5 — `AimArrow.UpdateArrow` modifies `transform.localScale.x` by reading current X
**File:** `Assets/Scripts/Launch/AimArrow.cs` line 49–50
**Issue:** `new Vector3(transform.localScale.x, scaleY, 1f)` reads the current X scale. If X was accidentally changed externally, it propagates. Consider caching the initial X scale in `Awake`.

### L6 — `RoundManagerHUD` Parent is `--- INPUT ---` but it manages UI, not input
**File:** `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs` lines 79–83
Naming inconsistency. The `inputSep` separator contains `RoundManager`, `LaunchController`, and `RoundManagerHUD` — mixing concerns under one label. Low impact on functionality.

### L7 — Test naming does not follow `test_[system]_[scenario]_[expected]` pattern in some tests
**Files:** `game-round-tracker-tests.cs`, `game-constants-validation-tests.cs`
**Issue:** Test methods like `MultipleRounds_TrackCorrectly` and `InitialState_RoundOneZeroShots` use PascalCase `[Scenario]_[Expected]` rather than the project's `test_[system]_[scenario]_[expected]` convention from `test-standards.md`. Consistent naming aids CI output readability.

---

## Build Safety Assessment

### WebGL
- **Legacy Input API** (`Input.GetMouseButtonDown`): Works on WebGL desktop, unreliable on WebGL mobile. **Actual risk.**
- **`Shader.Find("Sprites/Default")`** in `RuntimeSpriteFactory`: Marked "Always Included Shaders" in error message. If the project's Graphics settings do not include it, particles go pink on WebGL build. **Must verify in ProjectSettings/GraphicsSettings.asset.**
- **`ProceduralAudioClipGenerator`**: AudioClip.Create with `stream=false` works on WebGL. No issue.
- **`System.Reflection`** in `SetupSceneBatchMode` (TMP import): Editor-only, not in runtime build. No issue.
- **`StandaloneInputModule`**: Added by scene setup tool. WebGL uses this module; correct for desktop WebGL. Should be `TouchInputModule` or the newer `InputSystemUIInputModule` for mobile WebGL.

### Mobile (iOS/Android)
- `Input.mousePosition` on touch: works for single touch on both platforms via Unity's touch-to-mouse emulation, but breaks with multi-touch or when the UI EventSystem captures touches first.
- `Camera.main` in `LaunchController.Awake()`: fine at runtime.
- `GroundScorch.BuildMaskSprite()` main-thread texture build: will hitch on first impact on older Android devices (see M7).
- No `Application.targetFrameRate` setting: will run at device vsync (60/120Hz). Debris `FixedUpdate` uses `Time.fixedDeltaTime` so physics is frame-rate independent. Fine.

### Standalone
No issues specific to standalone detected.

---

## Positive Observations

1. **Event unsubscription** is complete in all `OnDestroy` methods across `RoundManager`, `CameraController`. No event leaks.
2. **`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`** on all statics (`AudioManager`, `RoundManagerHUD`, `RuntimeSpriteFactory`, `RocketDebris`, `GroundScorch`) — correct domain reload handling.
3. **`_activeCoroutine` pattern** in `CameraController` prevents coroutine races cleanly.
4. **`RuntimeSpriteFactory` shared sprite/material** eliminates per-object texture duplication.
5. **`partial class RoundManager`** split is clean — `round-manager-auto-play-restart-and-target.cs` only accesses fields defined in the main file. No circular partial dependency.
6. **`GameConstants` as static SSOT** — `LaunchController`, `HUD`, `Rocket`, `ObstacleSpawner` all read from it. No duplication.
7. **Assembly definitions** are correct: Runtime, Editor, Tests.Editor are properly isolated. Tests can only reference Runtime, not Editor.
8. **CI pipeline exists** (`unity-ci-build-and-test.yml`) with EditMode tests + WebGL build on main push.
9. **Existing unit tests** cover `GameRoundTracker` (12 tests) and `GameConstants` (6 tests) with proper Arrange/Act/Assert structure.
10. **`ObstacleSpawner` safe zone** uses correct clamped velocity before angle solve (M3 from prior roadmap was fixed).

---

## Recommended Actions

Priority order (highest impact first):

1. **[HIGH] Fix C2** — Add `Destroy(gameObject, 2f)` after `_grounded = true` in `RocketDebris.FixedUpdate`. This is the only unbounded memory growth path in the game.
2. **[HIGH] Fix C1** — Separate `_elapsed` advance from `GetOffset()` in `CameraScreenShake`. Move time advance to `Update()`.
3. **[HIGH] Fix C3 / H6** — Replace `Input.GetMouseButton*` with `Input.touchSupported` guard or migrate to Input System. Minimum: use `Input.touchCount > 0 ? Input.GetTouch(0).position : Input.mousePosition`.
4. **[HIGH] Fix H1** — Clamp `totalTime` in `ObstacleSpawner.CalculateTrajectory` to prevent degenerate near-vertical arc trajectories.
5. **[MEDIUM] Fix H3** — Add `DontDestroyOnLoad` to `AudioManager` and `RoundManagerHUD` or document single-scene constraint.
6. **[MEDIUM] Fix M7** — Pre-warm `GroundScorch` mask variants at scene start, not on first impact.
7. **[MEDIUM] Fix H2** — Reset `_groundPrepared = false` in `GroundScorch.ClearAll()` for Editor workflow safety.
8. **[LOW] Fix L4** — Accumulate audio phase continuously in `CreateWinJingle` to eliminate note-start click.
9. **[LOW] Fix L7** — Rename test methods to match `test_[system]_[scenario]_[expected]` convention.
10. **[INFRA] Verify** `Sprites/Default` is in Project Settings > Graphics > Always Included Shaders before WebGL ship.

---

## Metrics

- Type Coverage: 100% (all public APIs are typed; no `dynamic` or `object` casts in runtime code)
- Test Coverage: ~15% of runtime classes have direct unit tests (2/13 runtime classes: `GameRoundTracker`, `GameConstants`). Missing: `ObstacleSpawner`, `AudioManager`, `Rocket`, `LaunchController`, `CameraController`, `GroundScorch`, `RocketDebris`.
- Linting Issues: 0 syntax errors detected. ~3 naming convention violations (test methods).
- Known Defects Found This Review: 3 Critical, 6 High, 9 Medium, 7 Low = 25 total findings.

---

## Roadmap Update

The following items from `roadmap-260403-0010-path-to-10.md` are now resolved or partially addressed:

| Roadmap Item | Status | Notes |
|---|---|---|
| 1. Unit tests | PARTIAL — 2 test files exist (GameRoundTracker, GameConstants) | Need tests for ObstacleSpawner, AudioManager, Rocket |
| 2. Input System migration | OPEN | C3 above — confirmed critical for mobile/WebGL |
| 3. Scene wiring validation | PARTIAL — OnValidate exists in RoundManager, LaunchController, CameraController | Obstacle layer gap (M3) still open |
| 4. Object pooling | OPEN | C2 (debris no-destroy) is a real bug; pooling would fix it |
| 5. ScriptableObject config | OPEN | GameConstants is still a static class |
| 6. CI/CD pipeline | DONE — .github/workflows/unity-ci-build-and-test.yml present | |
| 7. Audio clip auto-wiring | OPEN | Editor tool attempts AssetDatabase.LoadAssetAtPath but only if files exist |
| 8. Obstacle layer assignment | OPEN | M3 — confirmed |
| 9. Trajectory accuracy | OPEN — H1 above is a new, more precise description of the bug | |
| 10. Localization | OPEN | |

**New items found this review (not in prior roadmap):**
- C1: CameraScreenShake time-advance side-effect in getter
- C2: RocketDebris never self-destructs — unbounded list growth
- H2: GroundScorch `_groundPrepared` stale after ClearAll in Editor
- H4: Obstacle rocket collision visual glitch (obstacle stays visible)
- M4: Note-start click in win jingle audio

---

## Unresolved Questions

1. Is hitting an obstacle intentionally terminal for the rocket (same as hitting ground), or should it be deflectable? The current implementation treats obstacles as "ground" via shared tag, but there is no design doc confirming this.
2. Is the game intended for mobile release? If yes, C3 (Input System) and M7 (texture hitch) move to Critical.
3. `SetupSceneBatchMode` uses `System.Reflection` to invoke TMP import — has this been tested on the CI runner (Ubuntu)? The path `Assets/TextMesh Pro/Resources/TMP Settings.asset` uses a space in the directory name which can cause issues on some CI setups.
