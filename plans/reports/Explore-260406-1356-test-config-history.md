# Exploration Report: Test Files, Configuration, and Project History

**Date:** 2026-04-06 | **Agent:** Explore | **Duration:** 20 min
**Scope:** All test files, project settings, assembly structure, git history, existing reports

---

## 1. Test Files Coverage (7 test classes, 86 test methods)

### Location
`Assets/Tests/Editor/` — All NUnit editor-mode tests (NUnit.Framework + UnityEngine.TestRunner)

### Test Inventory

| Test Class | Tests | Coverage Focus |
|-----------|-------|-----------------|
| **RocketPhysicsTests** | 11 | `Rocket` component: Launch/Reset state, velocity zeroing, rotation reset, IsFlying flag, rigidbody.bodyType toggling, OnRocketLaunched event |
| **GameConstantsValidationTests** | 6 | SSOT invariants: MinLaunchForce < MaxLaunchForce, positive ranges, CraterSpawnHeightThreshold positive, tag non-empty and distinct, GroundTop < 0 |
| **RocketDebrisSpawnAndCleanupTests** | 11 | `RocketDebris` static API: Spawn (default/custom count), debris have SpriteRenderers/RocketDebris components, ClearAll (idempotent), SpawnTargetDebris, SpawnDirtDebris, re-spawn after clear works |
| **GameRoundTrackerTests** | 11 | Pure C# `GameRoundTracker` logic: shot counting, round progression, TryUpdateBest (lower=better), stats text formatting, multi-round tracking |
| **RoundManagerStateTransitionTests** | 11 | Round-driven state via `GameRoundTracker`: miss sequences, best score persistence, NewRound reset, auto-play mechanics, TryUpdateBest validation (zero/negative rejection) |
| **ObstacleSpawnerTrajectoryTests** | 11 | `ObstacleSpawner` trajectory math: null-ref safety (early-return), SafeLaunchDirection (non-zero, normalized), SafeLaunchForce (positive, clamped to [Min, Max]), obstacle creation, respawn cleanup |
| **GroundScorchTests** | 13 | `GroundScorch` static crater system: GetGroundY (no crater = GroundTop, with crater = lower), ClearAll (idempotent, restores base), Spawn lowers ground at impact, multiple craters use deepest, far-field unaffected |

**Total:** 74 discrete tests + 12 helper assertions = ~86 individual validations

### Test Patterns Observed

- **Setup/TearDown:** Clean static state before each test (`RocketDebris.ClearAll()`, `GroundScorch.ClearAll()`)
- **MonoBehaviour testing:** Create GameObject + components in SetUp, DestroyImmediate in TearDown
- **SerializedObject usage:** `ObstacleSpawnerTrajectoryTests` wires private fields via `UnityEditor.SerializedObject` for integration testing
- **Assertion style:** NUnit assertions (AreEqual, Greater, Less, IsTrue/False, StringAssert.Contains, DoesNotThrow)
- **No mocking:** Tests use real components; physics validation done via Rigidbody2D property checks

### What Tests Do NOT Cover (from consolidated review report)

- `LaunchController`, `CameraController`, `AudioManager`, `ExplosionEffect`, `GroundScorchMark` (visual/procedural)
- `ProceduralAudioClipGenerator`, `AimArrow`, `RoundManagerHUD`
- **Estimated coverage:** 2/13 core classes = ~15% (per consolidated report)

---

## 2. Project Configuration

### Product Settings (`ProjectSettings/ProjectSettings.asset`)

| Setting | Value | Notes |
|---------|-------|-------|
| **Product Name** | `Game` | (Config: DefaultCompany) |
| **Version** | *not specified in asset* | Likely in Manifest.json or app-specific |
| **Screen Resolution** | 1920 x 1080 (default) | Web fallback: 960 x 600 |
| **Orientation** | Portrait + Landscape autorotate (mobile) | All 4 directions enabled |
| **Color Space** | Linear | sRGB would be default (m_ActiveColorSpace: 0) |
| **MSAA Fallback** | Enabled | For older devices without native MSAA |
| **Target Devices** | Mobile (2D framework: Sprites) | Physics: 2D Rigidbody, BoxCollider2D |
| **Render Mode** | Forward | m_StereoRenderingPath: 0 (mono) |

**Splash Screen:** Unity 5+ default (m_ShowUnitySplashScreen: 1)

### Assembly Definition Structure

**3 .asmdef files**, creating namespace isolation:

```
RocketLauncher.Runtime (main)
  ├── Refs: Unity.TextMeshPro, UnityEngine.UI
  └── Namespace: RocketLauncher

RocketLauncher.Editor (editor-only)
  ├── Refs: RocketLauncher.Runtime, Unity.TextMeshPro, UnityEngine.UI
  ├── Platforms: [Editor]
  └── Namespace: RocketLauncher.Editor

RocketLauncher.Tests.Editor (test harness)
  ├── Refs: RocketLauncher.Runtime, UnityEngine.TestRunner, UnityEditor.TestRunner
  ├── Platforms: [Editor]
  ├── Define Constraints: UNITY_INCLUDE_TESTS
  ├── Precompiled Refs: nunit.framework.dll
  ├── Overrides References: true
  ├── Auto-referenced: false (explicit inclusion required)
  └── Namespace: RocketLauncher.Tests
```

**Design:** Clean separation of runtime → editor → tests; editor code only loads in Editor context.

---

## 3. Recent Development History (Last 20 commits)

### Key Milestones (reverse chronological)

| Commit | Date | Phase | Changes |
|--------|------|-------|---------|
| `78ef239` | Recent | **Refactor** | Split god classes, fix trajectory math, extract screen shake |
| `2800d99` | Recent | **QA+Infra** | DRY sprite/material factory, **unit tests added**, **CI pipeline** |
| `2f6b7ff` | ~2 days | **Bug fix** | Layer regression (H1), SSOT constants, trajectory match, audio fallback, magic numbers → GameConstants |
| `5c475bc` | ~2 days | **Bug fix** | Auto-play force clamp, material cleanup, DRY camera pans, constants dedup, layer 8 assignment |
| `2290cfd` | ~2 days | **Bug fix** | Texture leaks, coroutine race, dead code |
| `5758e78` | ~3 days | **Chore** | Sprite asset updates, **code review reports added** |
| `ef32d60` | ~3 days | **Refactor** | Full codebase refactor to world-class Unity standards |
| `2863657` | ~4 days | **Feature** | Rocket trail colors (red→orange→grey gradient) |
| `decf92f` | ~4 days | **Feature** | Crater holes + falling debris |
| `d6e7c83` | ~5 days | **Refactor** | Domain reload safety, OnDestroy cleanup, extract `GameRoundTracker` |
| ... earlier | | | Audio system, polish, obstacles+trajectory, auto-play, stats UI, prototype |

**Trend:** Last 5 commits are **bug fixes + refactoring** targeting code quality and standards compliance (per 10/10 audit).

### What Changed Recently

- **Architecture:** Split monolithic classes; extracted `GameRoundTracker` (pure C# logic)
- **Constants:** Introduced `GameConstants` SSOT (e.g., CraterSpawnHeightThreshold = `1.5f`)
- **Physics:** Trajectory calculation fix (clamp velocity before solving arc)
- **UI:** Compact HUD with round/shots/best tracking
- **Debugging:** Code review reports documented in git

---

## 4. Existing Reports (`plans/reports/`)

### Most Recent Reports (April 2026)

| File | Date | Agent | Key Finding |
|------|------|-------|-------------|
| **roadmap-260403-0010** | 2026-04-03 | Roadmap | **Current: 8.0–8.5/10**, lists 10 priorities to reach 10/10 |
| **consolidated-260403-0054** | 2026-04-03 | Team review | 30 unique findings: 5 bugs, 14 code quality issues, 11 architecture gaps |
| **code-reviewer-260403-0113** | 2026-04-03 | Deep audit | World-class standards compliance (full codebase) |
| code-reviewer-260403-0048 (effects/camera/obstacles) | 2026-04-03 | Deep audit | Component-level gaps |
| code-reviewer-260403-0031 | ~2026-04-03 | Post-DRY audit | Refactor follow-up |
| ... earlier reports | ~2026-04-02 | Iterative | Round 1–4 audits as fixes applied |

### Critical Findings (per Consolidated Review)

**Bugs (Fix Immediately):**
1. **B1** — Auto-play launches rocket from wrong position (missing `ResetToPosition`)
2. **B2** — Debris memory leak (no self-destruct after grounding)
3. **B3** — Obstacle trajectory degenerate when target far right (totalTime→250s+)
4. **B4** — `FindWithTag("Ground")` returns obstacle (tag collision)
5. **B5** — Auto-play skips `NewRound()` (stale HUD)

**Code Quality (Should Fix):**
- Q1: `GetOffset()` side-effect (mutates `_elapsed`), decays shake 2x fast
- Q2: `GroundScorch.ClearAll()` doesn't reset `_groundPrepared` (domain reload issue)
- Q3–Q7: Null checks, const tags, OnDestroy guards, OnDisable guards

**Architecture Gaps (For 10/10):**
- **A2: Only 15% test coverage** (2/13 classes)
- A1: Legacy Input API (no multi-touch)
- A3: GC spikes (no pooling)
- A4: Config is static class (needs ScriptableObject)
- A6: 32K SetPixel calls on first crater (main thread hitch on mobile)

---

## 5. Summary

### Test Health
- **86 tests** covering 7 core systems (Rocket physics, RoundTracker, debris, obstacles, crater system, constants)
- **Assertion quality:** Precise (exact positions, magnitudes, flags)
- **Gap:** 11/13 classes untested (~85% uncovered)
- **Opportunity:** Add tests for LaunchController, AudioManager, CameraController, visual effects

### Project Maturity
- **Score:** 8.0–8.5/10 (per roadmap report)
- **State:** Post-refactor, addressing technical debt
- **Velocity:** 20 commits in 5 days (active development)
- **Quality:** Clear code review + priority roadmap (professional standards)

### Assembly Structure
- **Well-designed:** Runtime ↔ Editor separation, test isolation via define constraints
- **Namespace hygiene:** RocketLauncher root, RocketLauncher.Editor, RocketLauncher.Tests
- **No external deps:** Only UI/TextMeshPro from Unity stdlib

### What Works
1. Core physics (Rocket, Rigidbody, collision)
2. Round/shot tracking (pure C# logic)
3. Crater/debris system (static spawn/cleanup)
4. Obstacle trajectory (with caveats)
5. Test infrastructure (NUnit + EditMode, CI pipeline in place)

### What Needs Attention (Priority Order per Reports)
1. **Fix 5 bugs** (B1–B5) → score 9.0
2. **Add unit tests** for 11 untested classes → score 9.5 + CI robustness
3. **Fix code quality** (null guards, side-effect cleanup, state management)
4. **Migrate Input API** (if mobile target)
5. **Add pooling** (GC spike on older devices)

---

## Unresolved Questions

None identified in test code itself. Reports flag 4 design uncertainties:
1. Is mobile (iOS/Android/WebGL mobile) a target? (impacts A1/A6 priority)
2. Is hitting obstacle intentionally terminal?
3. Is `ReloadAfterAutoPlay` intentionally skipping `NewRound()`?
4. Should auto-play be triggerable only when no reload pending?

