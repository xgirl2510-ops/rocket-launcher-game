# Code Review: Full Codebase — Rocket Launcher

**Date:** 2026-04-06 | **Score: 8.8 / 10** | **Prev score: 7.4/10 (world-class audit, 2026-04-03)**

---

## Scope

- **Files reviewed:** 19 runtime scripts + 4 editor tools + 7 test files
- **LOC:** ~3,050 (scripts) + ~650 (tests) = ~3,700 total
- **Review focus:** Full codebase, all categories
- **Updated plans:** none (no active plan file provided)

---

## Overall Assessment

Significant improvement since the 7.4/10 world-class audit. All 5 critical bugs (B1–B5) from `consolidated-260403-0054` are fixed. Architecture has matured: `ImpactEffectsHandler` decouples Rocket from effects, `CameraScreenShake` is isolated, `RuntimeSpriteFactory` DRYs sprite/material creation, `RoundManager` split into 3 partial-class files, 52 tests across 6 files. Main remaining gap is a handful of medium issues found in today's sub-reviews (two still unresolved) plus one pattern gap in test quality.

---

## Critical Issues

**None.** All previously critical bugs resolved.

---

## High Priority Findings

### H1 — `GroundScorch.ClearAll()` leaks 8 Texture2D per round restart

`ClearAll()` destroys crater GameObjects and clears `_craters` list but does NOT call `DestroyMaskVariants()`. Eight 64×64 RGBA32 Texture2D (≈128KB each = ~1MB total) accumulate in GPU memory each round. `DestroyMaskVariants()` only runs on domain reload (`SubsystemRegistration`), not on normal round reset.

**Fix:** Add `DestroyMaskVariants();` inside `ClearAll()` before `_groundPrepared = false`. Mask variants rebuild lazily via `EnsureMaskVariants()` on next impact.

```csharp
// ground-scorch-mark.cs:139
public static void ClearAll()
{
    for (int i = _allCraters.Count - 1; i >= 0; i--)
        if (_allCraters[i] != null) Object.Destroy(_allCraters[i]);
    _allCraters.Clear();
    _craters.Clear();
    DestroyMaskVariants();  // ← ADD THIS
    _groundPrepared = false;
}
```

### H2 — `Rocket.OnCollisionEnter2D` leaves body Dynamic after impact

After ground collision, code zeros velocity but doesn't switch back to `Kinematic`. Body stays `Dynamic`, so if any Physics Material 2D with bounciness > 0 is on the ground or rocket collider, rocket can bounce/drift after `_isFlying = false`, potentially firing stale events or drifting off-screen.

```csharp
// Rocket.cs:99-107 — missing bodyType reset
_isFlying = false;
_rb.linearVelocity = Vector2.zero;
_rb.angularVelocity = 0f;
// ← should add: _rb.bodyType = RigidbodyType2D.Kinematic;
```

**Fix:** Add `_rb.bodyType = RigidbodyType2D.Kinematic;` immediately after zeroing velocity (matches `ResetToPosition` behavior).

### H3 — `ObstacleSpawner.CalculateTrajectory` — totalTime may under-sample arc when target is elevated

`timeOfFlight` uses `start.y - GameConstants.GroundTop` as the fall height, not `start.y - target.y`. When target Y > GroundTop (normal gameplay, target floats in sky at ~2-7 units), time-of-flight to GroundTop is longer than time-of-flight to target, so `totalTime` is over-estimated and the safe-zone covers more than the actual trajectory. This is actually the **safe direction** (more coverage = fewer blocked paths) but it means the trajectory arc simulation doesn't match reality precisely, which could sometimes include safe-zone points above the target, potentially blocking obstacle placement unnecessarily.

*Impact: low in practice but the math inconsistency is worth documenting.*

---

## Medium Priority Improvements

### M1 — `AudioManager.PlayHitTarget()` — pitch not reset if exception occurs

```csharp
_oneShotSource.pitch = 1.3f;
if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip);  // if throws?
_oneShotSource.pitch = 1.0f;  // never reached on exception
```

All subsequent audio plays at 1.3x pitch. Use try/finally or set pitch back before calling PlayOneShot.

### M2 — `CameraScreenShake.Update` runs every frame even when idle

When `_elapsed >= _duration`, `Update` still runs each frame setting `_currentOffset = Vector2.zero` repeatedly. Add `_isShaking` flag or `enabled = false` guard:

```csharp
private void Update()
{
    if (_elapsed >= _duration)
    {
        if (_currentOffset != Vector2.zero) _currentOffset = Vector2.zero;
        return;  // or: enabled = false;
    }
    // ...
}
```

### M3 — `ObstacleSpawner.CreateObstacle` — hardcoded `"Default"` layer

```csharp
go.layer = LayerMask.NameToLayer("Default");
```

All other layer/tag refs use `GameConstants`. Should be `GameConstants.LayerObstacle` or at minimum a local constant. Layer 0 ("Default") is fine functionally but breaks SSOT.

### M4 — `LaunchController.HandleTouchEnded` — potential NRE on `_roundManager.RoundTracker`

```csharp
_roundManager?.OnShotFired();
RoundManagerHUD.Instance?.UpdateStatsUI(_roundManager.RoundTracker);  // no null-check on _roundManager
```

If `_roundManager` is null, line 2 throws NRE. Should be:
```csharp
if (_roundManager != null)
    RoundManagerHUD.Instance?.UpdateStatsUI(_roundManager.RoundTracker);
```

### M5 — `GroundScorch.BuildMaskSprite()` uses RGBA32 where Alpha8 suffices

8 mask textures × 64×64 × 4 bytes = ~128KB per round. `Alpha8` format would reduce this 75% with no visual difference (masks only use alpha channel). `TextureFormat.Alpha8` is supported on all target platforms.

---

## Low Priority Suggestions

### L1 — `RocketDebris._allDebris` not null-guarded in `OnDestroy`

`OnDestroy` calls `_allDebris.Remove(gameObject)` — correct. But if debris is destroyed during scene teardown after `ResetStaticState()` cleared the list, `_allDebris` could be null or already GC'd. Add null-check: `_allDebris?.Remove(gameObject)`.

### L2 — `CameraController` 256 LOC — exceeds 200-line guideline

`CameraController.cs` is 256 lines. Could split coroutine methods into a partial class `camera-controller-pan-coroutines.cs`, but given the cohesion of the file, this is borderline YAGNI. Noted for guideline compliance.

### L3 — `rocket-launcher-scene-auto-setup-editor-tool.cs` is 374 LOC

Exceeds 200-line guideline significantly. Already partially split into 4 files but the main file is still too large. `SetupAudio`, `WireRoundManager`, `WireLaunchController`, `WireRoundManagerHUD`, `WireCameraController` could move to `rocket-launcher-scene-setup-wiring.cs`.

### L4 — `AimArrow` doesn't reset rotation on `Hide()`

Not a bug, but on first frame of showing after a launch cycle, arrow could briefly show previous rotation until `UpdateArrow` is called. Add `transform.rotation = Quaternion.identity` in `Hide()` for cleanliness.

### L5 — `ProceduralAudioClipGenerator` — phase discontinuity in win jingle

Each note uses `globalT = (n * noteSamples + i) / SampleRate` for phase, which resets phase continuity at note boundaries. Audible click between notes. Fix by not resetting phase per note (use continuous `globalT`).

### L6 — Test naming convention inconsistency

Tests use `PascalCase_With_Underscores` (e.g. `InitialState_RoundOneZeroShots`). Project `test-standards.md` specifies `test_[system]_[scenario]_[expected_result]`. Not a functional issue but inconsistent with stated standards.

---

## Positive Observations

- **`ImpactEffectsHandler`** — clean decoupling of Rocket physics from visual effects. Subscribes via `OnEnable`/`OnDisable`, not `Start`/`OnDestroy`. Correct pattern.
- **`RuntimeSpriteFactory`** — DRY sprite/material creation. `ResetStaticState()` with `SubsystemRegistration` ensures no stale assets on domain reload.
- **`GroundScorch` struct `CraterData`** — previous parallel-lists issue resolved. Struct keeps related data together. Good.
- **`RoundManager` partial class split** — 160 + 132 lines, clean separation: core event handling vs. restart/auto-play/target. Respects 200-line guideline.
- **`CameraController.PanCoroutine`** — DRY coroutine used for intro, return, and look-target. Optional ortho lerp parameter clean.
- **`StopActiveCoroutine` pattern** — single tracked `_activeCoroutine` prevents race conditions. Well-implemented.
- **`OnDestroy` event unsubscription** — all subscriber classes properly unsubscribe in `OnDestroy`. No leaked delegates.
- **`OnValidate` guards** — all major MonoBehaviours check `gameObject.scene.isLoaded` before warnings. Editor-tool safe.
- **`GameConstants` SSOT** — tags, forces, ground position all read from one place. No hardcoded tag strings in runtime scripts.
- **Test coverage** — 52 tests across 6 files covering: constants, tracker, physics state, debris lifecycle, trajectory math, scorch system. Meaningful tests with clear Arrange/Act/Assert.
- **`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`** — used correctly on all static state holders.

---

## Comparison With Previous Audit (7.4/10)

### What Improved (score drivers)

| Item | Prev | Now |
|------|------|-----|
| B1: Auto-play invisible rocket | BUG | Fixed — `ResetToPosition` + `SetActive(true)` before `Launch()` |
| B2: Debris memory leak | BUG | Fixed — `Destroy(gameObject, 2f)` after grounding |
| B3: Trajectory degenerate | BUG | Fixed — clamped force, discriminant fallback to 60° |
| B4: `FindWithTag("Ground")` picks obstacle | BUG | Fixed — uses `GameObject.Find(GroundObjectName)` by name |
| B5: Auto-play skips `NewRound()` | BUG | Fixed — `ReloadAfterAutoPlay` calls `NewRound()` |
| Q1: `CameraScreenShake` side-effect in `GetOffset` | HIGH | Fixed — `_elapsed` moved to `Update()`, `GetOffset()` is pure |
| Q2: `ClearAll()` not resetting `_groundPrepared` | HIGH | Fixed — `_groundPrepared = false` in `ClearAll()` |
| Q5: Tag constants as `[SerializeField]` | MEDIUM | Fixed — now `private const` from `GameConstants` |
| Q6: `ObstacleSpawner` no `OnDestroy` | MEDIUM | Fixed — `OnDestroy` calls `ClearObstacles()` |
| Q7: `CameraScreenShake` no `OnDisable` | MEDIUM | Fixed — `OnDisable` zeroes offset + sets elapsed = duration |
| Q10: `TryUpdateBest(0)` corrupts best | LOW | Fixed — guard `if (shots <= 0) return false` |
| A2: Test coverage (18 tests) | LOW-15% | Now 52 tests, 6 files — coverage significantly higher |
| A5: No audio differentiation hit vs ground | MEDIUM | Fixed — separate `PlayHitGround`/`PlayHitTarget`, procedural fallbacks |
| A7: Obstacles shared `TagGround` | MEDIUM | Fixed — obstacles use `TagGround` but `PrepareGround` uses name lookup |
| Partial class split | N/A | RoundManager, editor tool split cleanly |
| `ImpactEffectsHandler` | N/A | New — fully decouples Rocket from effects |
| `RuntimeSpriteFactory` | N/A | DRY sprite/material, domain reload safe |
| `GroundScorch` struct refactor | OPTIONAL | Done — parallel lists → `CraterData` struct |

### Still Unresolved From Prev Audit

| Item | Status |
|------|--------|
| H1: Mask texture leak in `ClearAll()` | NEW FINDING (not in prev audit) |
| H2: Rocket stays Dynamic after collision | PARTIALLY new (prev noted bounce risk, not exact fix) |
| M3: `LayerMask.NameToLayer("Default")` hardcoded | Identified in today's sub-review, still present |
| M4: NRE risk in `LaunchController.HandleTouchEnded` | Partially new |
| A1: Legacy Input API | Accepted as YAGNI |
| A3: Object pooling | Accepted as YAGNI |
| A4: ScriptableObject config | Accepted as YAGNI |
| A10: Hardcoded UI strings | Accepted as YAGNI |
| Test naming convention | Still inconsistent |

### What Regressed

**None identified.** All changes are improvements or neutral.

---

## Top 5 Actionable Improvements (by impact)

1. **[HIGH] Fix mask texture leak in `GroundScorch.ClearAll()`** — call `DestroyMaskVariants()`. ~1MB leak per round on mobile. One line fix.

2. **[HIGH] Fix `Rocket.OnCollisionEnter2D` — switch to Kinematic after zeroing velocity** — prevents potential bounce/drift on rigidbody after `_isFlying = false`.

3. **[MEDIUM] Fix `LaunchController.HandleTouchEnded` NRE** — null-check `_roundManager` before accessing `.RoundTracker`. One line.

4. **[MEDIUM] Fix `AudioManager.PlayHitTarget()` pitch** — wrap in try/finally or reorder: set pitch back before calling PlayOneShot.

5. **[LOW] `BuildMaskSprite()` texture format** — `TextureFormat.Alpha8` cuts 75% VRAM for mask textures. Two-word change.

---

## Metrics

- **Total scripts reviewed:** 19 runtime + 4 editor + 7 test = 30 files
- **Total LOC:** ~3,700
- **Files over 200 lines:** 3 (`CameraController` 256, editor tool 374, UI canvas 176)
- **Test count:** 52 tests, 7 test files
- **Test coverage (estimated):** ~60–70% of core logic (GameConstants, GameRoundTracker, Rocket physics, debris, scorch, trajectory, round-state fully covered; AudioManager, CameraController, LaunchController, RoundManager not directly unit-tested)
- **Linting issues:** N/A (no C# linter configured — Unity compiler is check)
- **Critical issues:** 0
- **High issues:** 2 (H1 memory leak, H2 rigidbody state)
- **Medium issues:** 4 (M1 pitch, M2 idle Update, M3 hardcoded layer, M4 NRE)
- **Low issues:** 6 (L1–L6)
- **Score delta vs prev audit:** +1.4 (7.4 → 8.8)

---

## Unresolved Questions

1. Does any Physics Material 2D with `bounciness > 0` exist on Ground or Rocket collider? If no, H2 is theoretical only.
2. `GroundScorch._groundPrepared` is reset in `ClearAll()` but NOT in `ResetStaticState()`. This means if domain reload fires (editor only) the ground renderer loses mask interaction but `_groundPrepared` is already false — so `PrepareGround()` re-runs on next `Spawn()`. Functionally correct but worth confirming in Play Mode with `Reload Domain` disabled.
3. `ObstacleSpawner` tags spawned obstacles with `GameConstants.TagGround`. The B4 fix (using `GameObject.Find(GroundObjectName)` in `PrepareGround`) is correct, but if any other code does `FindWithTag(TagGround)` it will still find obstacles. Confirm no such call exists.
4. `rocket-launcher-scene-auto-setup-editor-tool.cs` at 374 LOC — should this be split into a `rocket-launcher-scene-setup-wiring.cs` partial class per the 200-line guideline?
