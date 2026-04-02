# Path to 10/10 — What Remains

**Date:** 2026-04-03
**Current state:** Round 4 review fixes applied, codebase is clean and consistent.

---

## What was fixed in this session

| ID | Fix |
|----|-----|
| H1 | `CreateRocket()` layer: `6` → `LayerMask.NameToLayer("Rocket")` |
| H2 | `LaunchController` removes `_minLaunchForce`/`_maxLaunchForce` fields; reads `GameConstants` directly |
| M1 | `1.5f` magic number extracted to `GameConstants.CraterSpawnHeightThreshold` |
| M3 | `ObstacleSpawner` clamps velocity before computing arc, so safe zone matches actual rocket flight |
| M4 | `RoundManager.HandleRestart` gets clarifying comment on `StopAllCoroutines` scope |
| M6 | `AudioManager.PlayHitTarget()` gains `_groundHitClip` fallback, symmetric with `PlayHitGround()` |
| L  | `ExplosionEffect` assigns `Sprites/Default` material to `ParticleSystemRenderer` (prevents pink particles in builds) |

---

## What would be required for a true 10/10

### 1. Automated tests (highest impact gap)
- No unit or integration tests exist.
- Need: NUnit tests for `GameConstants`, `GameRoundTracker`, `ObstacleSpawner.CalculateTrajectory`, `AudioManager` fallback logic.
- Physics integration tests (EditMode) for `Rocket.Launch()` → velocity/direction.
- EditMode scene setup smoke test to catch layer/tag regressions like H1.

### 2. Input System (Unity Input System package)
- Currently uses legacy `Input.GetMouseButton*` which does not support touch natively on mobile.
- Replace with `InputSystem` (`EnhancedTouch` or `InputAction`) for proper multi-touch, pointer abstraction, and testability.

### 3. Scene wiring validation
- Editor tool creates objects but cannot verify all `[SerializeField]` references are wired at runtime.
- Add an `OnValidate()` or editor-time validator that logs missing refs and fails Setup if any are null.

### 4. Object pooling
- `ExplosionEffect`, `RocketDebris`, `GroundScorch` all use `new GameObject` + `Destroy()` per shot.
- Replace with a simple pool (`Stack<T>`) to eliminate GC spikes on older devices.

### 5. Asset-based config (ScriptableObject)
- `GameConstants` is a static class; changing values requires recompile.
- A `GameConfig` ScriptableObject would allow designers to tweak forces/thresholds without touching code and would be testable via asset swap.

### 6. CI/CD pipeline
- No GitHub Actions workflow exists.
- Add: compile check (`unity-builder`), NUnit test runner, optional device build on tag.

### 7. Audio clip assignment via Resources / Addressables
- `_launchClip`, `_thrustClip`, `_boomClip` are unassigned by default; editor tool does not wire them.
- Load from `Resources` or Addressables so a fresh scene setup is immediately playable with sound.

### 8. Obstacle layer assignment
- `CreateObstacle()` in `ObstacleSpawner` does not set a layer; obstacle GameObjects land on layer 0 (Default).
- Should be assigned to the same layer as Ground for consistent physics filtering.

### 9. `ObstacleSpawner` trajectory accuracy (partial — M3 improves but does not fully solve)
- When `v` is clamped the target may no longer be reachable with the clamped force.
- Proper fix: re-solve the angle given the clamped force, or guarantee spawn positions such that target is always within range.

### 10. Localization / accessibility baseline
- All UI strings are hardcoded in C#.
- Minimum bar: extract to `const string` or a `StringTable` so text can be changed without code edits.

---

## Priority order for next sprint

1. Unit tests (NUnit, EditMode) — unblocks CI
2. CI workflow — catches regressions automatically
3. Object pooling — performance on device
4. Input System migration — mobile correctness
5. ScriptableObject config — designer-friendly
6. Obstacle layer fix — minor correctness gap
7. Trajectory re-solve when force is clamped — correctness
8. Audio auto-wiring — playability out of the box
9. OnValidate scene wiring check — developer experience
10. Localization baseline — future-proofing

---

*Unresolved questions: none.*
