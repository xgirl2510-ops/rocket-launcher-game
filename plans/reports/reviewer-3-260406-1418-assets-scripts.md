# Code Review: Test Coverage & Edge Cases Analysis
**Reviewer:** reviewer-3 | **Date:** 2026-04-06 14:18 | **Scope:** Assets/Scripts/ (19 files) + Assets/Tests/Editor/ (7 files)

---

## Executive Summary

**Test Coverage: ~30% of codebase**  
- 7 test files exist, covering 4-5 core classes
- 14 of 19 runtime scripts have **ZERO** test coverage
- Critical game state machines (RoundManager, LaunchController, CameraController) completely untested
- 38 untested public methods identified
- Collision event handling in Rocket not covered (high-risk)
- Drag computation edge cases in LaunchController unexplored

**Key Risk:** State-dependent gameplay systems (round flow, input, camera) have no regression tests.

---

## Scope Summary
- **Runtime scripts:** 19 total (Assets/Scripts/)
- **Test files:** 7 total (Assets/Tests/Editor/)
- **Lines analyzed:** ~4,200 runtime LOC + ~1,100 test LOC
- **Review focus:** Public method coverage, edge case testing, state transition validation

---

## Critical Issues

### [CRITICAL] Rocket.cs — Collision Events Not Tested
**Evidence:** Rocket.cs:94-127 (OnCollisionEnter2D, OnTriggerEnter2D) vs rocket-physics-tests.cs
- OnCollisionEnter2D (line 94) sets IsFlying=false, zeros velocity, fires OnImpact — **ZERO test coverage**
- OnTriggerEnter2D (line 110) target hit path with RocketDebris.SpawnTargetDebris — **ZERO test coverage**
- Missing: What if collision fires while already landed? What if target collides while _isFlying=false?
- Missing: Event firing order validation (OnImpact before OnTargetHit?)
- Missing: Sprite visibility state after collision

**Recommendation:** Add tests:
1. `test_rocket_collision_with_ground_sets_is_flying_false` — verify ground collision stops flight
2. `test_rocket_trigger_target_fires_on_impact_event` — verify target hit event fires
3. `test_rocket_collision_while_not_flying_ignored` — verify guards work correctly
4. `test_rocket_sprite_visibility_toggled_on_collision` — verify visual state syncs

**Risk Level:** Breaking gameplay if collision logic regresses — affects hit/miss detection.

---

### [CRITICAL] LaunchController.cs — Zero Test Coverage
**Evidence:** LaunchController.cs (162 lines, 9 public methods) vs test files
- No unit tests exist for drag computation, input handling, or force mapping
- TryComputeDrag (line 122) has complex boundary logic: raw distance < minDragDistance threshold
- Missing: What if drag distance exactly equals minDragDistance? What if maxDragDistance = minDragDistance?
- Missing: Multiple drag attempts in sequence
- Missing: Input while rocket already flying

**Recommendation:** Add tests:
1. `test_launch_controller_drag_below_minimum_returns_false` — boundary test
2. `test_launch_controller_drag_exactly_at_minimum_returns_true` — boundary test
3. `test_launch_controller_drag_clamped_to_max` — verify force caps at maxDragDistance
4. `test_launch_controller_force_maps_to_physics_range` — verify Lerp produces valid GameConstants range
5. `test_launch_controller_normalized_force_0_to_1` — verify normalization logic
6. `test_launch_controller_input_disabled_ignores_drag` — verify input guard
7. `test_launch_controller_camera_null_handled_safely` — verify null checks

**Risk Level:** Input system bug — if drag threshold broken, all launches fail or behave unexpectedly.

---

### [CRITICAL] RoundManager.cs — Zero Test Coverage for Core State Machine
**Evidence:** RoundManager.cs (160 lines, 5 public methods in main + 5 in partial) vs test files
- HandleTargetHit (line 77) and HandleRocketMiss (line 102) have complex conditional logic but NO tests
- Missing: Auto-play mode state management (_isAutoPlaying flag logic)
- Missing: Event cleanup on OnDestroy (line 137)
- Missing: Reload delay coroutine edge cases

**Recommendation:** Add tests:
1. `test_round_manager_handle_target_hit_increments_best_score` — verify round flow
2. `test_round_manager_handle_rocket_miss_increments_miss_count` — verify miss tracking
3. `test_round_manager_hints_shown_after_5_misses` — verify MissesBeforeHints logic
4. `test_round_manager_auto_play_skips_win_ui` — verify _isAutoPlaying bypasses UI
5. `test_round_manager_on_destroy_cleans_up_events` — verify event unsubscription

**Risk Level:** Game loop regression — miss counting, round progression, restart all affected.

---

## Important Priority Findings

### [IMPORTANT] Rocket.cs — MaxHeight Tracking Not Tested
**File:** Rocket.cs:48, 80-81 | **Test Coverage:** 0%
```csharp
_maxHeight = transform.position.y;  // line 48: initialized
if (transform.position.y > _maxHeight)  // line 80: updated in FixedUpdate
    _maxHeight = transform.position.y;
```
- Passed to OnImpact event (line 104, 120) for crater sizing in GroundScorch
- Missing: What if rocket never moves up (starts at peak)? Does _maxHeight stay at launch point?
- Missing: What if negative Y trajectory (arcing downward)? Height should update throughout flight

**Recommendation:** Add test:
```csharp
[Test]
public void Launch_TrackMaxHeightDuringFlight() {
    _rocket.Launch(Vector2.up, 15f);
    // Simulate FixedUpdate with rocket moving up
    // Assert maxHeight increases with position.y
}
```

---

### [IMPORTANT] Rocket.cs — RotateToVelocity Velocity Guard Not Tested
**File:** Rocket.cs:85-92 | **Test Coverage:** 0%
```csharp
Vector2 vel = _rb.linearVelocity;
if (vel.sqrMagnitude < 0.01f) return;  // Guard: skip if velocity too small
```
- Guard prevents degenerate angle calculation (Atan2 on near-zero vectors)
- Missing: Verify rotation skipped when velocity ≤ √0.01 ≈ 0.1 units/sec
- Missing: Verify no NaN/Inf angle produced from degenerate velocity

**Recommendation:** Add test:
```csharp
[Test]
public void RotateToVelocity_WithZeroVelocity_SkipsRotation() {
    _rb.linearVelocity = Vector2.zero;
    _rocket.Launch(Vector2.up, 10f);
    // Trigger FixedUpdate, verify rotation unchanged
}
```

---

### [IMPORTANT] CameraController.cs — State Machine Transitions Not Tested
**File:** CameraController.cs:14-196 | **Test Coverage:** 0%
- Enum CameraState has 5 states: Intro, Idle, Following, Returning, LookingAtTarget (line 14)
- Missing: Verify state transitions on rocket events
  - OnRocketLaunched → Following (line 193)
  - OnRocketLanded/OnTargetHit → Idle (line 196-197)
- Missing: Verify coroutine preemption (StopActiveCoroutine on state change)
- Missing: Verify FollowRocket only executes in Following state

**Recommendation:** Add state transition tests:
```csharp
[Test]
public void CameraState_TransitionsCorrectlyOnRocketLaunched() {
    _camera.HandleRocketLaunched();
    Assert.AreEqual(CameraState.Following, _camera._currentState);
}
```

---

### [IMPORTANT] RocketDebris.cs — Movement Physics Not Tested
**File:** RocketDebris.cs:111-126 | **Test Coverage:** 0%
```csharp
_velocity.y -= Gravity * Time.fixedDeltaTime;  // Gravity application
transform.position += (Vector3)(_velocity * Time.fixedDeltaTime);  // Physics step
float groundY = GroundScorch.GetGroundY(transform.position.x) + _groundYOffset;
if (transform.position.y <= groundY) { /* grounding */ }
```
- Missing: Verify gravity constant (12f) produces realistic fall
- Missing: Verify debris stops at crater floor (GetGroundY interaction)
- Missing: Verify _groundYOffset randomization (line 94: -0.15f to 0.1f) works

**Recommendation:** Add test:
```csharp
[Test]
public void RocketDebris_FallsToGround_UnderGravity() {
    RocketDebris.Spawn(Vector2.zero, 1);
    // Simulate ~0.5s of FixedUpdate steps
    // Assert debris Y position decreased and stopped at ground
}
```

---

### [IMPORTANT] LaunchController.cs — Force Normalization Boundary Cases
**File:** LaunchController.cs:138 | **Test Coverage:** 0%
```csharp
normalizedForce = (clampedDistance - _minDragDistance) / (_maxDragDistance - _minDragDistance);
```
- Expects denominator > 0, but what if _maxDragDistance == _minDragDistance?
- Division by zero risk if inspector values misconfigured
- Missing: Test for exact edge values

**Recommendation:** Add bounds validation test:
```csharp
[Test]
public void TryComputeDrag_MinEqualsMax_SafelyHandled() {
    _launchController._minDragDistance = 1.0f;
    _launchController._maxDragDistance = 1.0f;  // Invalid config
    bool result = _launchController.TryComputeDrag(out _, out float force);
    // Should either return false or clamp force safely
}
```

---

### [IMPORTANT] GroundScorch.cs — Crater Overlap Logic Not Tested
**File:** GroundScorch.cs:119-135 | **Test Coverage:** Partial
```csharp
public static float GetGroundY(float x) {
    // ... iterate all craters, find deepest intersection
    if (craterY < baseY) baseY = craterY;
}
```
- Test ClearAll and GetGroundY separately, **missing:** multiple overlapping craters
- Missing: What if 3 craters at same X? Does "deepest wins" actually work?
- Missing: Test crater at boundary (x = crater.X + crater.Width)

**Test file gap:** ground-scorch-tests.cs has "MultipleCraters_DeepestWins" (line 106) but only tests 2 craters at same X. Should test:
- Crater at exactly crater center
- Crater at crater edge
- 3+ overlapping craters

---

### [IMPORTANT] ObstacleSpawner.cs — Spawn Area Edge Case
**File:** ObstacleSpawner.cs:116-118 | **Test Coverage:** Partial
```csharp
float minX = start.x + _spawnPaddingX;
float maxX = target.x - _spawnPaddingX;
if (minX >= maxX) return;  // Silent failure if spawn area too narrow
```
- If target is too close to vehicle, spawn area collapses (minX >= maxX)
- Test only checks non-null refs, **missing:** narrow spawn area handling
- Missing: Verify safe trajectory still calculated even if obstacles can't spawn

**Recommendation:** Add test:
```csharp
[Test]
public void RespawnObstacles_NarrowSpawnArea_HandlesSafely() {
    WireSpawnerRefs(Vector3.zero, new Vector3(1f, 0f, 0f));  // Very close target
    _spawner.RespawnObstacles();
    // Should not crash; SafeLaunchDirection should be valid
}
```

---

### [IMPORTANT] AudioManager.cs — Singleton Pattern Not Tested
**File:** AudioManager.cs:12-39 | **Test Coverage:** 0%
```csharp
if (Instance != null && Instance != this) { Destroy(gameObject); return; }
Instance = this;
```
- Singleton double-instantiation guard not tested
- Missing: Test duplicate AudioManager destruction
- Missing: Test Instance null after reset

**Recommendation:** Add test:
```csharp
[Test]
public void AudioManager_DuplicateInstance_DestroysNew() {
    var first = new GameObject().AddComponent<AudioManager>();
    var second = new GameObject().AddComponent<AudioManager>();
    Assert.AreEqual(first, AudioManager.Instance);
    // second should be destroyed
}
```

---

### [IMPORTANT] CameraController.cs — Ortho Size Zoom Not Tested
**File:** CameraController.cs:123-139 | **Test Coverage:** 0%
```csharp
float dist = Vector2.Distance(_rocket.transform.position, _vehicleTransform.position);
float zoomT = Mathf.Clamp01(dist / _zoomMaxDistance);
float targetOrtho = Mathf.Lerp(_defaultOrthoSize, _maxOrthoSize, zoomT);
_camera.orthographicSize = Mathf.MoveTowards(
    _camera.orthographicSize, targetOrtho, _zoomOutSpeed * Time.deltaTime);
```
- Complex zoom interpolation: distance-based T value → Lerp → MoveTowards
- Missing: Verify zoom caps at _maxOrthoSize when distance > _zoomMaxDistance
- Missing: Verify smooth transition with MoveTowards

**Recommendation:** Add test:
```csharp
[Test]
public void CameraFollow_ZoomsOutWhenRocketFarAway() {
    // Place rocket far from vehicle (> _zoomMaxDistance)
    // Assert ortho size increases toward _maxOrthoSize
}
```

---

## Moderate Priority Improvements

### [MODERATE] RoundManagerHUD.cs — Singleton Not Tested
**File:** RoundManagerHUD.cs:11-40 | **Test Coverage:** 0%
- Similar pattern to AudioManager
- No test for double instantiation cleanup

---

### [MODERATE] ImpactEffectsHandler.cs — Event Subscription Not Tested
**File:** ImpactEffectsHandler.cs:13-23 | **Test Coverage:** 0%
- OnEnable/OnDisable hook/unhook Rocket.OnImpact
- Missing: Verify subscription/unsubscription lifecycle
- Missing: Verify HandleImpact fires when Rocket impact occurs

---

### [MODERATE] CameraScreenShake.cs — Decay Calculation Not Tested
**File:** camera-screen-shake.cs:24-34 | **Test Coverage:** 0%
```csharp
float decay = 1f - (_elapsed / _duration);
_currentOffset = Random.insideUnitCircle * _magnitude * decay;
```
- Decay should go 1.0 → 0.0 over duration
- Missing: Verify offset magnitude decreases over time
- Missing: Verify offset = 0 after duration elapsed

---

### [MODERATE] ProceduralAudioClipGenerator.cs — Audio Synthesis Not Tested
**File:** ProceduralAudioClipGenerator.cs | **Test Coverage:** 0%
- 5 public static methods create audio clips (lines 14-132)
- No validation that clips are created successfully or have valid sample data
- Missing: Verify sample data bounds (-1.0 to 1.0)
- Missing: Verify clip duration matches specification

---

### [MODERATE] RuntimeSpriteFactory.cs — Cache Reset Not Tested
**File:** runtime-sprite-factory.cs:16-29 | **Test Coverage:** 0%
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState() { /* destroy cached sprite/material */ }
```
- Cleanup on domain reload not verified
- Missing: Verify sprite texture destroyed
- Missing: Verify cache repopulates after reset

---

### [MODERATE] round-manager-auto-play-restart-and-target.cs — Target Randomization Not Tested
**File:** round-manager-auto-play-restart-and-target.cs:103-113 | **Test Coverage:** 0%
```csharp
float x = Random.Range(_targetMinX, _targetMaxX);  // line 107
float y = Random.Range(_targetMinY, _targetMaxY);  // line 108
```
- Missing: Verify target position stays within bounds
- Missing: Verify target respawned on HandleRestart
- Missing: Verify obstacles respawned after target moves

---

### [MODERATE] RocketTrail.cs — ParticleSystem Null Handling
**File:** rocket-trail-particle-effect.cs:29-48 | **Test Coverage:** 0%
- StartTrail/StopTrail/ClearTrail all guard `if (_ps == null) return;`
- Missing: Verify behavior when ParticleSystem created vs found
- Missing: Verify trail clears on Reset

---

### [MODERATE] ExplosionEffect.cs — Burst Configuration Not Tested
**File:** explosion-burst-particle-effect.cs | **Test Coverage:** 0%
- Particle count (30), lifetime (0.6s), speed (4) hardcoded
- Missing: Verify burst fires on Spawn
- Missing: Verify color selection (gold for hit, grey for miss)

---

## Positive Observations

✅ **GameRoundTracker.cs** — Excellent test coverage (9/9 public methods tested)  
✅ **GameConstants.cs** — Invariant validation tests prevent invalid value drift  
✅ **GroundScorch.cs** — Solid cleanup and boundary testing (no craters case, multiple craters)  
✅ **ObstacleSpawner.cs** — Null-safety guards in RespawnObstacles tested  
✅ **RocketDebris.cs** — Spawn/cleanup lifecycle well covered  
✅ **Test naming** — Descriptive test names (e.g., "RespawnObstacles_WithRefs_SafeLaunchForceIsWithinRange")  
✅ **Test isolation** — Proper SetUp/TearDown cleanup prevents state leakage  

---

## Test Coverage by File

| Script | Public Methods | Tested | Coverage | Grade |
|--------|---|---|---|---|
| GameRoundTracker.cs | 4 | 4 | 100% | A |
| GameConstants.cs | 0 (static) | ✓ (invariants) | Partial | B |
| Rocket.cs | 4 | 3 | 75% | C+ |
| RoundManager.cs | 5 | 0 | 0% | F |
| LaunchController.cs | 3 | 0 | 0% | F |
| CameraController.cs | 6 | 0 | 0% | F |
| AimArrow.cs | 2 | 0 | 0% | F |
| AudioManager.cs | 7 | 0 | 0% | F |
| RoundManagerHUD.cs | 5 | 0 | 0% | F |
| CameraScreenShake.cs | 2 | 0 | 0% | F |
| RocketTrail.cs | 3 | 0 | 0% | F |
| ProceduralAudioClipGenerator.cs | 5 | 0 | 0% | F |
| RocketDebris.cs | 3 | 2 | 67% | C |
| GroundScorch.cs | 3 | 3 | 100% | A |
| ObstacleSpawner.cs | 1 | 1 | 100% | A |
| ExplosionEffect.cs | 1 | 0 | 0% | F |
| ImpactEffectsHandler.cs | 0 (callbacks) | 0 | 0% | F |
| RuntimeSpriteFactory.cs | 2 | 0 | 0% | F |
| round-manager-auto-play-restart-and-target.cs | 5 | 0 | 0% | F |

**Overall: 10/56 public methods tested (18%)** | **Grade: D+**

---

## Recommended Test Priorities (by risk)

1. **[CRITICAL]** Rocket collision events (OnCollisionEnter2D, OnTriggerEnter2D)
2. **[CRITICAL]** LaunchController drag computation and input handling
3. **[CRITICAL]** RoundManager state machine (HandleTargetHit, HandleRocketMiss)
4. **[IMPORTANT]** CameraController state transitions and zoom
5. **[IMPORTANT]** RocketDebris movement and grounding physics
6. **[IMPORTANT]** AudioManager singleton pattern
7. **[MODERATE]** GroundScorch crater overlap edge cases
8. **[MODERATE]** ProceduralAudioClipGenerator audio synthesis validation

---

## Unresolved Questions

1. **Rocket collision:** Can OnCollisionEnter2D fire multiple times in single frame? Should there be a guard?
2. **LaunchController:** Is the force normalization formula (line 138) numerically stable with all input ranges?
3. **CameraController:** What happens if ReturnToVehicle called while already Returning? Does StopActiveCoroutine prevent hangs?
4. **RocketDebris:** Why is _groundYOffset randomized per-piece? Is this intentional art design?
5. **GroundScorch:** Why is GetGroundY using halfWidth = Width * 0.4f instead of 0.5f? Intentional crater tapering?

---

**Report Generated:** 2026-04-06 14:18  
**Review Complete:** All 19 runtime scripts + 7 test files analyzed  
**Next Steps:** Prioritize critical test gaps; add collision event tests first (highest gameplay risk)
