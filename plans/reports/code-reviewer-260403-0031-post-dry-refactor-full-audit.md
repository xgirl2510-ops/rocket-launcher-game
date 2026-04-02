# Code Review -- Round 5 Post-DRY Refactor Full Audit

**Date:** 2026-04-03
**Reviewer:** code-reviewer
**Scope:** ALL scripts (`Assets/Scripts/`, `Assets/Editor/`, `Assets/Tests/Editor/`), asmdefs, manifest.json, CI pipeline

---

## Code Review Summary

### Scope
- Files reviewed: 22 C# files (16 runtime, 4 editor, 2 test) + 3 asmdef + 1 manifest.json + 1 CI workflow
- Lines of code: ~3,071 total C# (excluding asmdefs/JSON/YAML)
- Review focus: Full codebase audit post-DRY refactor (RuntimeSpriteFactory extraction, OnValidate additions, test suite, asmdef structure, CI pipeline)

### Overall Assessment

This round represents the single largest quality jump in the project's history. The RuntimeSpriteFactory extraction cleanly eliminates 5 duplicate sprite/material caches. The asmdef structure is textbook correct. Tests are well-structured NUnit with proper arrange/act/assert. The CI pipeline is clean and uses game-ci best practices. Nearly all prior review issues (H1, H2, M1, M3, M6, L7) are confirmed resolved in the working tree.

**Score: 8.5 / 10** (up from 8.0)

---

## Scores

| Dimension | Score | Delta | Notes |
|---|---|---|---|
| Architecture & Organization | 9.0 | +0.5 | RuntimeSpriteFactory is clean SRP. asmdef boundaries correct. |
| Code Quality | 9.0 | +0.5 | DRY extraction removes ~90 lines of duplicate code. OnValidate coverage excellent. |
| Unity Best Practices | 8.5 | +0.5 | Static reset on factory, `#if UNITY_EDITOR` on OnValidate, proper asmdef platform constraints. |
| Maintainability | 9.0 | +0.5 | Single cache owner for textures/materials. Clear naming (DestroyMaskVariants). |
| Performance | 8.0 | 0 | No change; debris ground-Y linear scan still exists (acceptable at scale). |
| Robustness | 8.0 | +0.5 | OnValidate catches missing wiring in editor. Tests validate invariants. |
| Testability | 7.5 | NEW | 14 passing tests, but only covers pure C# classes. No MonoBehaviour or physics tests. |
| CI/CD | 7.0 | NEW | Pipeline present and structured. Missing UNITY_LICENSE secret warning. |

---

## Critical Issues

None.

---

## High Priority Findings

### H1 -- `Shader.Find("Sprites/Default")` will return null in builds if shader not referenced

`runtime-sprite-factory.cs` line 51:
```csharp
_particleMaterial = new Material(Shader.Find("Sprites/Default"));
```

`Shader.Find` only works at runtime if the shader is already referenced by a material in the project, included in the "Always Included Shaders" list in Graphics Settings, or in a Resources folder. `Sprites/Default` is typically safe because Unity includes it by default for 2D projects, but this is not guaranteed in all build configurations (especially stripped builds or custom render pipelines).

This was previously spread across 2 files (RocketTrail, ExplosionEffect) with the same issue. Now centralized, which is better -- but still has no null-check.

**Fix:**
```csharp
public static Material GetParticleMaterial()
{
    if (_particleMaterial != null) return _particleMaterial;
    var shader = Shader.Find("Sprites/Default");
    if (shader == null)
    {
        Debug.LogWarning("[RuntimeSpriteFactory] Sprites/Default shader not found; using fallback.");
        shader = Shader.Find("UI/Default");
    }
    _particleMaterial = new Material(shader);
    return _particleMaterial;
}
```
And add "Sprites/Default" to Project Settings > Graphics > Always Included Shaders.

**Severity:** High in production builds; low for editor-only testing.

---

### H2 -- Uncommitted changes: all refactored files are unstaged

`git status` shows 10 modified files + 5 untracked (RuntimeSpriteFactory, asmdefs, tests, CI) that are NOT committed. The last commit (`2f6b7ff`) contains the prior round's fixes but NOT the DRY refactor, test suite, or asmdef additions.

If the working tree is lost, all refactoring work is gone. This is the highest operational risk right now.

**Fix:** Commit the changes. Suggested grouping:
1. `runtime-sprite-factory.cs` + consumer diffs (5 effect/obstacle files) -- "refactor: extract RuntimeSpriteFactory, remove duplicate sprite/material caches"
2. asmdefs + manifest.json -- "chore: add asmdef assembly definitions and test-framework package"
3. Tests -- "test: add GameRoundTracker (9) and GameConstants (5) unit tests"
4. CI workflow -- "ci: add GitHub Actions Unity CI build and test pipeline"
5. OnValidate additions (3 files) -- "feat: add OnValidate missing-reference warnings to LaunchController, RoundManager, CameraController"

---

## Medium Priority Improvements

### M1 -- `RuntimeSpriteFactory.ResetStaticState` destroys texture but consumers may still hold refs

When domain reload fires `ResetStaticState`, the sprite and its texture are destroyed. But if any SpriteRenderer in the scene still references the destroyed sprite (e.g., an obstacle or debris piece surviving across domain reload in editor), it will show pink/missing. This is only an issue in the Editor during play-mode-without-domain-reload or when scripts recompile during play.

The factory re-creates on next access, so runtime is fine. But the destroy-while-potentially-referenced pattern can cause transient visual glitches in editor.

**Mitigation:** This is acceptable for the current scope. If it becomes noticeable, delay destruction until after scene unload.

---

### M2 -- `RoundManager.cs` exceeds 200-line threshold (274 lines)

`RoundManager.cs` is 274 lines, `CameraController.cs` is 277 lines. Both exceed the 200-line guidance from the project conventions. The editor tool (`rocket-launcher-scene-auto-setup-editor-tool.cs` at 365 lines) is split into partials, which is correct. Similar treatment could be applied to RoundManager if it grows further -- e.g., extracting auto-play logic to a helper class.

No immediate action required; just flagging proximity to the threshold. YAGNI applies.

---

### M3 -- `ObstacleSpawner` trajectory clamp still has a mathematical edge case

`ObstacleSpawner.CalculateTrajectory` clamps `v` to `[MinLaunchForce, MaxLaunchForce]` *before* computing trajectory points (this was the M3 fix from Round 4). Good. However, the angle `theta` was computed using unclamped `v`:

```csharp
// Line 83: theta uses v (unclamped)
theta = Mathf.Atan2(v * v + Mathf.Sqrt(discriminant), g * dx);
// Line 88: then v is clamped
float vClamped = Mathf.Clamp(v, GameConstants.MinLaunchForce, GameConstants.MaxLaunchForce);
```

The angle is correct for unclamped `v` but the trajectory is computed with clamped `v`. For the angle to be optimal at clamped velocity, theta should be recomputed after clamping. In practice, targets within range (~92 units at max force) work fine, and the safe-zone radius provides enough margin. But for far targets requiring forces > 30, the rocket will fall short of the computed safe zone.

**Verdict:** Known limitation, documented in the roadmap. Acceptable as-is.

---

### M4 -- `RocketDebris.OnDestroy` does `_allDebris.Remove(gameObject)` -- O(n) per destruction

When `ClearAll()` destroys N debris objects, each `OnDestroy` fires `_allDebris.Remove(gameObject)` which is O(n) list scan. After `ClearAll()` empties the list, these `Remove` calls on an empty list are O(1), so it's fine. But if debris are individually destroyed (not via ClearAll), each removal is O(n). With max ~36 debris per miss and maybe 5 misses = 180 objects, this is still negligible.

No action needed. Noting for completeness.

---

### M5 -- Test naming does not follow `test_[system]_[scenario]_[expected_result]` convention

Per `test-standards.md`, tests should follow `test_[system]_[scenario]_[expected_result]`. Current tests use PascalCase NUnit convention (`IncrementShots_IncreasesCount`, `TryUpdateBest_FirstCall_AlwaysUpdates`) which is idiomatic for C#/NUnit but diverges from the standard.

Since this is a Unity C# project (not GDScript), the PascalCase convention is more appropriate. The existing names are clear and follow the standard `Method_Scenario_Expected` C# test naming pattern. Recommend leaving as-is and noting the test-standards.md is GDScript-oriented.

---

## Low Priority Suggestions

### L1 -- `RocketTrail._startColor` and `_endColor` serialized fields are unused

`rocket-trail-particle-effect.cs` lines 16-17:
```csharp
[SerializeField] private Color _startColor = new Color(1f, 0.3f, 0f, 1f);
[SerializeField] private Color _endColor = new Color(0.4f, 0.4f, 0.4f, 0f);
```
These fields are declared and serialized but never read. The `CreateTrailParticleSystem` method hardcodes its own gradient colors (lines 85-96). Either use these fields in the gradient or remove them.

---

### L2 -- CI workflow references `secrets.UNITY_LICENSE` without documentation

`.github/workflows/unity-ci-build-and-test.yml` line 10:
```yaml
env:
  UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
```
No README or CI doc explains how to set up the `UNITY_LICENSE` secret. First-time contributors will see a CI failure with no guidance.

**Fix:** Add a comment in the YAML or a `docs/ci-setup.md` with game-ci license activation steps.

---

### L3 -- `game-constants-validation-tests.cs` missing test for `GroundTop` invariant

`GameConstants.GroundTop = -5f` is not validated. The test suite checks force values, tags, and thresholds but not the ground position. If someone changes `GroundTop` to a positive value, the entire ground/vehicle/crater system breaks silently.

```csharp
[Test]
public void GroundTop_IsBelowZero()
{
    Assert.Less(GameConstants.GroundTop, 0f);
}
```

---

### L4 -- `RoundManagerHUD` singleton does not call `DontDestroyOnLoad`

Both `AudioManager` and `RoundManagerHUD` use singleton patterns but neither calls `DontDestroyOnLoad`. For a single-scene game this is correct. If multi-scene is ever added, both would be destroyed on scene load. Acceptable per YAGNI.

---

### L5 -- `ExplosionEffect.Spawn` creates GO without pooling

Each impact creates a new `GameObject("Explosion")` that self-destructs after `_particleLifetime + 0.2f`. With rapid-fire misses, this generates GC pressure. Object pooling would eliminate this, but YAGNI for a prototype.

---

### L6 -- asmdef `RocketLauncher.Runtime` includes `Unity.TextMeshPro` and `UnityEngine.UI` references

These are needed because `round-manager-hud.cs` uses `TMPro` and `UnityEngine.UI`. This is correct. However, it couples the runtime assembly to UI packages. If the HUD were in its own assembly (e.g., `RocketLauncher.UI`), the core gameplay assembly would be decoupled from UI. Low priority; YAGNI.

---

## Verification of Prior Review Issues

| ID | Status | Verification |
|----|--------|-------------|
| R4-H1 | FIXED | `CreateRocket()` now uses `LayerMask.NameToLayer("Rocket")` (line 110 of env-setup file) |
| R4-H2 | FIXED | `LaunchController` reads `GameConstants.MinLaunchForce`/`MaxLaunchForce` directly (line 99) |
| R4-M1 | FIXED | `1.5f` extracted to `GameConstants.CraterSpawnHeightThreshold` (line 15), used in `Rocket.cs` line 107 |
| R4-M3 | PARTIALLY FIXED | Clamp applied before trajectory computation; angle still uses unclamped v (see M3 above) |
| R4-M6 | FIXED | `PlayHitTarget()` now has `_groundHitClip` fallback (AudioManager lines 63-65) |
| R4-L7 | FIXED | `ExplosionEffect` now calls `RuntimeSpriteFactory.GetParticleMaterial()` (line 99) |

---

## New Additions Assessment

### RuntimeSpriteFactory (runtime-sprite-factory.cs) -- EXCELLENT

- Clean static class, 55 lines
- Proper `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` reset
- Destroys texture before sprite to avoid orphaned GPU resource
- Null-check before destroy prevents `MissingReferenceException`
- Single responsibility: solid sprite + particle material

**One concern:** `GetSolidSprite()` creates texture and sprite but only the sprite is tracked for cleanup. The texture is accessed via `_solidSprite.texture` in `ResetStaticState` -- this works because `Sprite.Create` does not take ownership of the texture. Correct.

### Assembly Definitions -- CORRECT

| asmdef | Platform | Refs | Verdict |
|--------|----------|------|---------|
| `RocketLauncher.Runtime` | All | TMP, UI | Correct -- runtime code needs TMP for HUD |
| `RocketLauncher.Editor` | Editor only | Runtime, TMP, UI | Correct -- editor tools reference runtime types |
| `RocketLauncher.Tests.Editor` | Editor only | Runtime, TestRunner (Editor + Engine) | Correct -- `overrideReferences: true` + `nunit.framework.dll` + `UNITY_INCLUDE_TESTS` define constraint |

### Unit Tests -- GOOD

**GameRoundTrackerTests (9 tests):**
- Covers initial state, increment, new round, best score (first/lower/higher/equal), stats text, multi-round flow
- Proper `[SetUp]` with fresh instance
- Clear assert messages implicit in NUnit
- Missing: edge case for `TryUpdateBest(0)` -- what happens if someone passes 0 shots? `_bestScore == 0 || shots < _bestScore` would set best to 0, which then locks out future updates since `shots < 0` is unlikely. Minor edge case.

**GameConstantsValidationTests (5 tests):**
- Validates force ordering, positivity, threshold positivity, tag non-empty, tag distinctness
- Good invariant guards against accidental constant changes
- Missing: `GroundTop < 0` check (see L3)

### CI Pipeline -- GOOD

- Correct `game-ci/unity-test-runner@v4` for EditMode tests
- WebGL build only on main branch pushes (not PRs) -- good gate
- Artifact upload with `if: always()` on tests -- ensures test results are available even on failure
- Missing: `UNITY_LICENSE` documentation, no caching step (would speed up builds)

### OnValidate Additions -- GOOD

- Added to `LaunchController`, `RoundManager`, `CameraController`
- Wrapped in `#if UNITY_EDITOR` -- correct, avoids stripping issues
- Checks critical `[SerializeField]` refs
- Uses `Debug.LogWarning` (not Error) -- appropriate for editor-time hints
- Note: `RoundManager.OnValidate` does not check `_cameraController` or `_obstacleSpawner` -- both are nullable/optional in some configurations, so this is fine

---

## Positive Observations

- RuntimeSpriteFactory is a textbook DRY extraction -- removes ~90 lines of nearly identical code from 5 files into a single 55-line class with centralized lifecycle management
- `DestroyMaskVariants()` rename from `DestroySprites()` is more precise and self-documenting
- Test suite is well-structured with clear arrange/act/assert even without explicit comments
- asmdef structure follows Unity best practices: Runtime (all platforms) > Editor (editor-only) > Tests (editor-only with define constraint)
- CI uses `game-ci` ecosystem which is the de facto standard for Unity GitHub Actions
- OnValidate catches are defensive but not annoying -- they produce warnings, not errors, and only in editor
- `GroundScorch` comment update (3 layers -> 2 layers) keeps docs in sync with code
- All prior H1/H2 fixes verified correct in working tree

---

## Recommended Actions

1. **(High -- Operational)** Commit all uncommitted changes. 10 modified + 5 untracked files at risk of loss.
2. **(High -- Build Safety)** Add null-check for `Shader.Find("Sprites/Default")` in `RuntimeSpriteFactory.GetParticleMaterial()`. Add shader to Always Included Shaders.
3. **(Low)** Remove unused `_startColor`/`_endColor` fields from `RocketTrail` or use them in the gradient.
4. **(Low)** Add `GroundTop_IsBelowZero` test to `GameConstantsValidationTests`.
5. **(Low)** Add CI setup documentation for `UNITY_LICENSE` secret.
6. **(Low)** Add `TryUpdateBest(0)` edge case test.

---

## Metrics

| Metric | Value |
|--------|-------|
| Files | 22 C# + 3 asmdef + 1 manifest + 1 YAML = 27 |
| Total C# lines | 3,071 |
| Files over 200 lines | 3 (RoundManager 274, CameraController 277, SceneSetupEditor 365) |
| Critical issues | 0 |
| High | 2 (shader null-check, uncommitted work) |
| Medium | 5 |
| Low | 6 |
| Tests | 14 (9 GameRoundTracker + 5 GameConstants) |
| Test coverage | Pure C# only; no MonoBehaviour/physics tests |
| DRY violations eliminated | 5 (sprite/material caches consolidated) |
| Lines removed by refactor | ~90 |
| Lines added (factory + tests) | ~223 |
| Net complexity change | Reduction -- fewer static caches to reason about |

---

## Unresolved Questions

1. Should `Shader.Find("Sprites/Default")` null-check be addressed now or deferred until a build pipeline is tested?
2. Is `TryUpdateBest(0)` a valid gameplay scenario (0 shots = instant win)? If not, add a guard; if so, add a test.
3. Should `RocketTrail._startColor`/`_endColor` be wired into the gradient, or are they vestigial from an earlier iteration?
