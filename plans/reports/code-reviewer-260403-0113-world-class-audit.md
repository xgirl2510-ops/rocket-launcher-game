# Code Review — World-Class Audit

**Date:** 2026-04-03
**Reviewer:** code-reviewer
**Scope:** Full codebase — all Runtime, Editor, and Test C# files

---

## Code Review Summary

### Scope
- Files reviewed: 28 C# files (18 runtime, 4 editor, 6 tests) + 3 asmdef files
- Lines of code analyzed: ~2,950
- Review focus: Full codebase — world-class standards evaluation
- Updated plans: none (no plan provided)

---

### Overall Assessment

This is a well-structured, polished Unity 2D game codebase for its size and scope. Multiple rounds of prior review have produced clean patterns: event-driven architecture is consistent, resource leaks are addressed, static state is reset correctly, and the editor tool is genuinely useful. However, several gaps prevent this from reaching "world-class" status. The biggest deficiencies are: thin test coverage (6 files covering only ~30% of runtime classes), the legacy `Input` system, the absence of object pooling, mixed file-naming conventions, and limited scalability architecture.

---

## Category Scores

---

### 1. Project Structure & Organization — 7.5/10

**Excellent:**
- Folder hierarchy (`Scripts/{Audio,Camera,Core,Effects,Launch,Obstacles,Rocket}`) is immediately navigable by any Unity developer.
- Three-assembly separation (Runtime / Editor / Tests) is correct and enforced by asmdef files with proper platform constraints.
- `Assets/` root is clean: no junk folders, no stray assets at root level.
- Separator GameObjects (`--- MANAGERS ---`, `--- ENVIRONMENT ---`) in the scene give immediate orientation in the Hierarchy.

**Gaps for world-class:**
- **Mixed file-naming within the same layer.** Runtime scripts are split between PascalCase (`Rocket.cs`, `CameraController.cs`, `AudioManager.cs`) and kebab-case (`rocket-trail-particle-effect.cs`, `round-manager-hud.cs`, `camera-screen-shake.cs`). There is no enforced rule — it appears the split happened at different refactor moments. World-class = one convention, enforced.
- **`Assets/UI/` folder exists** (seen in `ls`) but contains nothing referenced in any script — unknown if it holds orphaned assets.
- **No `ScriptableObjects/` or `Config/` folder.** All tuning values live as `[SerializeField]` on MonoBehaviours or as `const` in `GameConstants`. For a game targeting future scalability this is a structural gap.
- **No `Prefabs/` usage in code.** The `Prefabs/` folder exists but is empty of any referenced prefab — the editor tool builds everything procedurally from code, which works but prevents visual iteration by designers.
- **Tests live under `Assets/Tests/Editor/`** — correct, but there is no `Tests/Runtime/` for any PlayMode tests.

**Recommendation:** Adopt one file-naming rule project-wide (kebab-case is stated in dev rules, so migrate PascalCase files). Audit the `UI/` folder for dead assets. Add a `Config/` or `ScriptableObjects/` folder as a structural placeholder for the next phase.

---

### 2. Clean Code Principles — 7.5/10

**Excellent:**
- Method length is excellent throughout. The longest methods (`CalculateTrajectory`, `CreateTrailParticleSystem`, `BuildMaskSprite`, `ConfigureParticleSystem`) are all 30–50 lines of necessary procedural setup — the complexity is intrinsic, not accidental.
- Naming reveals intent without comments: `TryComputeDrag`, `TryUpdateBest`, `IsInSafeZone`, `SpawnObstaclesAvoidingTrajectory`, `ResetStaticState` — all self-documenting.
- Comments are "why" not "what": `// Stop only RoundManager's own coroutines — CameraController manages its own`, `// High-arc solution: atan2(v^2 + sqrt(disc), g*dx)`, `// Pivot at top-center so it hangs downward from ground line`.
- No dead code or unused imports found.
- No magic numbers in business logic — `GameConstants` is the SSOT and used consistently.

**Gaps for world-class:**
- **`ExplosionEffect` hardcodes configuration as private instance fields** (`_burstCount = 30`, `_particleLifetime = 0.6f`, `_startSpeed = 4f`, `_startSize = 0.2f`) but they are never exposed as `[SerializeField]`. These are effectively magic numbers one layer removed. No one can tune explosion feel without editing code.
- **`RocketDebris` color arrays are static readonly literals.** Correct pattern but the colors (rocket red, dirt brown, target red) are hardcoded tuning values that could diverge from visual design — no linkage to any visual design constant.
- **`HandleRestart()` in `round-manager-auto-play-restart-and-target.cs` has the event subscribe/unsubscribe dance** (`_cameraController.OnIntroComplete -= OnIntroDone; _cameraController.OnIntroComplete += OnIntroDone;`) — this is a safe pattern but the "why" comment only partially explains the defensive unsub. Minor.
- **`ProceduralAudioClipGenerator` uses `System.Random` (seeded at construction time) instead of `UnityEngine.Random`** — acceptable but inconsistent with the rest of the codebase which uses `Random.Range`. Not a bug, but inconsistency.
- **`GetTagManager()` uses `new SerializedObject(...)` every call** in the editor tool — called multiple times during setup. Minor allocation waste in an editor-only path.

**Recommendation:** Expose `ExplosionEffect` tuning values as `[SerializeField]` or move to a `ScriptableObject` config. Add a comment to the `System.Random` usage explaining why it diverges from `UnityEngine.Random`.

---

### 3. Architecture & Design Patterns — 7/10

**Excellent:**
- Event-driven separation is genuine, not cosmetic: `Rocket` fires events, `RoundManager` and `CameraController` subscribe independently. Neither class polls.
- `LaunchController` is pure input — zero game-flow logic.
- `GameRoundTracker` is a plain C# class with no MonoBehaviour dependency — correctly extracted.
- `RuntimeSpriteFactory` as a shared static factory for textures/materials is the right DRY solution and prevents the GPU leak pattern.
- `CameraController` state machine (`CameraState` enum + `_activeCoroutine` guard) is clean.
- `[DisallowMultipleComponent]` on singletons is good practice.
- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` on all static state — correct domain reload handling.

**Gaps for world-class:**
- **Singletons (`AudioManager`, `RoundManagerHUD`) are accessed via `Instance?.Method()` call-sites that are scattered across unrelated classes.** `LaunchController` calls `AudioManager.Instance` and `RoundManagerHUD.Instance`. `Rocket.cs` calls `ExplosionEffect.Spawn(...)` and `RocketDebris.Spawn(...)` directly — static coupling at the physics layer. This means `Rocket` implicitly depends on the audio and effects subsystems existing. World-class = `Rocket` fires a data event (`OnImpact(ImpactData)`) and a separate `ImpactEffectsHandler` wires the visuals/audio. The rocket should know nothing about debris or explosions.
- **`RoundManager` is a partial class split across two files**, which is a code smell at this scale. The split was made for file-size reasons (justified in comments), but the result is that `_isAutoPlaying`, `_missCount`, `_roundTracker` are defined in one file and mutated in the other — cognitive load on new developers. A better split would be composition (extract `AutoPlayController` as a separate `MonoBehaviour`).
- **`ObstacleSpawner` owns both trajectory math and obstacle placement.** Single Responsibility violation: trajectory calculation is physics domain, obstacle placement is game design domain. For this project size it's acceptable, but it's the obvious place to split.
- **`GroundScorch` uses parallel lists** (`_craterXPositions`, `_craterWidths`, `_craterDepths`) instead of a `CraterData` struct — classic anti-pattern. The lists must be kept in sync manually. One missed `_craterDepths.Add()` would silently corrupt the `GetGroundY` calculation.
- **No `IInputProvider` or input abstraction.** `LaunchController` directly uses `Input.GetMouseButtonDown(0)` — cannot be tested without the new Input System or a wrapper.

**Recommendation (priority order):**
1. Replace parallel lists in `GroundScorch` with `private struct CraterData { float X, Width, Depth; }`.
2. Decouple `Rocket` from `ExplosionEffect`/`RocketDebris` — introduce an impact event with position data.
3. Keep partial class for now but document the split explicitly in a comment at the top of each file.

---

### 4. Unity-Specific Best Practices — 8/10

**Excellent:**
- Physics in `FixedUpdate` (`Rocket`, `RocketDebris`), input in `Update` (`LaunchController`), camera in `LateUpdate` (`CameraController`) — all correct.
- `Rigidbody2D.interpolation = Interpolate` set on the rocket — correct for smooth camera follow.
- `CollisionDetectionMode2D.Continuous` on rocket — correct for fast-moving projectile.
- All `OnDestroy` methods unsubscribe from events — no event leaks.
- `_activeCoroutine` guard in `CameraController` prevents coroutine race conditions.
- `[DisallowMultipleComponent]` on singletons.
- `OnValidate` guarded by `#if UNITY_EDITOR` — no editor-only code in builds.
- `RuntimeSpriteFactory.ResetStaticState()` destroys textures and materials — correct.
- `CameraScreenShake.OnDisable` resets offset — correct component lifecycle.
- `Rigidbody2D.MoveRotation` used for physics rotation instead of `transform.rotation` in `FixedUpdate` — correct.

**Gaps for world-class:**
- **Legacy `Input` system (`Input.GetMouseButtonDown`, `Input.mousePosition`).** Unity 6 ships with the new Input System as default. The legacy system is deprecated. Mobile platforms in particular have significant differences. For a game targeting mobile (iPhone aspect ratio hardcoded), this is a real gap.
- **`Camera.main` is called in `Awake` and as a fallback in `Update`** (`if (_camera == null) _camera = Camera.main`). `Camera.main` does a tag-based `FindObjectsOfType` scan every call — the fallback in `Update` is a minor performance risk if the camera is ever null at runtime. Use `[SerializeField]` for the camera reference instead.
- **`GameObject.Find(...)` used extensively in the editor tool** (correct for editor-time), but also `GroundScorch.PrepareGround()` calls `GameObject.Find(GameConstants.GroundObjectName)` at runtime. This is O(N) over all scene objects, called on first crater spawn. Fine for a single-scene game but fragile.
- **`ExplosionEffect` creates a new `GameObject` per explosion with `Destroy(gameObject, lifetime + 0.2f)` — no object pooling.** Same for `RocketDebris`. For mobile targets, GC pressure from repeated instantiate/destroy cycles will cause frame spikes.
- **`StopAllCoroutines()` called in `HandleRestart()` on `RoundManager`** — this stops ALL coroutines including any from `_cameraController` if it were ever started from RoundManager context. The comment explains this correctly, but it's a footgun as the code grows. The `_activeCoroutine` pattern used in `CameraController` is superior.
- **`GroundScorch.BuildMaskSprite()` calls `tex.SetPixel(x, y, ...)` in a double loop** — 64x64 = 4,096 individual `SetPixel` calls. `SetPixels` with a pre-built array would be ~10x faster (minor, editor-only path, but inconsistent with `RuntimeSpriteFactory.GetSolidSprite()` which correctly uses `SetPixels`).

**Recommendation:**
1. Migrate to Unity Input System — highest impact for mobile viability.
2. Replace `Camera.main` call-site with `[SerializeField] private Camera _camera` in `LaunchController`.
3. Add a simple object pool for `ExplosionEffect` and `RocketDebris` (even a `Queue<GameObject>` pool of 20 pre-warmed objects eliminates GC spikes).
4. Use `SetPixels` in `BuildMaskSprite`.

---

### 5. Testing & Reliability — 6/10

**Excellent:**
- Six test classes covering `GameRoundTracker`, `GameConstants`, `ObstacleSpawner`, `Rocket`, `GroundScorch`, and `RocketDebris` — the most critical pure-logic classes are tested.
- `GameRoundTracker` tests are exemplary: AAA pattern, edge cases (equal score, zero shots, multi-round), correct isolation (plain C# class, no scene needed).
- `RocketPhysicsTests` correctly builds a minimal scene in `SetUp` and tears it down — proper `DestroyImmediate` in `TearDown`.
- `GroundScorchTests` correctly manages static state via `ClearAll()` in both `SetUp` and `TearDown`.
- `ObstacleSpawnerTrajectoryTests` wires via `SerializedObject` — correct approach for testing `[SerializeField]` fields.
- `[RuntimeInitializeOnLoadMethod]` on all static state means tests don't bleed into each other.

**Gaps for world-class:**
- **0 tests for `RoundManager`, `CameraController`, `LaunchController`, `AudioManager`, `RoundManagerHUD`, `AimArrow`.** These are the most complex behaviour classes. `RoundManager` state transitions (miss → reload, hit → win, auto-play loop) have zero test coverage.
- **Test naming does not follow the project's stated convention.** The `test-standards.md` rule specifies `test_[system]_[scenario]_[expected_result]` (snake_case) but all tests use `PascalCase` (`Launch_SetsIsFlying_True`, `GetGroundY_NoCraters_ReturnsGroundTop`). The PascalCase is actually more readable for C# NUnit, but it contradicts the documented standard — pick one.
- **No PlayMode tests.** All 6 test files are EditMode. Physics integration (does the rocket actually reach the target given the calculated trajectory?), audio (does the manager play the right clip on hit?), and camera state transitions are completely untested.
- **`ObstacleSpawnerTests.RespawnObstacles_CalledTwice_ClearsPreviousObstacles` is a weak assertion** (`Assert.LessOrEqual(secondCount, firstCount * 2)`). This would pass even if obstacles doubled. Should assert `secondCount == firstCount` (same obstacle count after re-spawn).
- **`RocketDebrisSpawnAndCleanupTests` mixes `Destroy` and `DestroyImmediate`** in teardown — the `ClearAll()` calls `Destroy` (deferred), then `DestroyAllDebrisImmediate()` cleans up in the same frame. This works but is fragile: tests depend on order of operations in the frame boundary. Using `Object.DestroyImmediate` consistently inside `ClearAll()` when in Editor mode (via `Application.isPlaying` check) would make this cleaner.
- **No fuzz/boundary tests for trajectory math.** `ObstacleSpawner.CalculateTrajectory` has a discriminant guard (`if (discriminant < 0f)`) — there are no tests that exercise this branch (target directly above spawn, target behind spawn, extreme distances).

**Recommendation:**
1. Add `RoundManagerTests` covering at minimum: miss increments count, hit calls `TryUpdateBest`, auto-play flag prevents player reload.
2. Fix the weak assertion in `RespawnObstacles_CalledTwice` to `Assert.AreEqual(firstCount, secondCount)`.
3. Add boundary tests for trajectory: `target.x < spawn.x` (negative dx), `target == spawn` (degenerate), `discriminant < 0` path.
4. Decide on naming convention and apply consistently.

---

### 6. Code Consistency — 8/10

**Excellent:**
- `namespace RocketLauncher` on every runtime file, `namespace RocketLauncher.Editor` on every editor file — zero violations.
- `[SerializeField] private _camelCase` pattern followed everywhere.
- `OnPascalCase` event naming consistent (`OnRocketLaunched`, `OnTargetHit`, `OnIntroComplete`, `OnLookTargetComplete`).
- `#if UNITY_EDITOR` guards on `OnValidate` consistent across `LaunchController`, `RoundManager`, `CameraController`.
- Null-guard pattern (`if (X != null) X.Method()` vs `X?.Method()`) is used both ways but never inconsistently within the same class.
- Brace style (Allman-adjacent, opening brace on same line for expressions, own line for methods) is consistent.
- `[Header("...")]` for inspector organization is consistent across all MonoBehaviours.
- `[DisallowMultipleComponent]` on singletons.
- `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` pattern consistent on all static classes.

**Gaps for world-class:**
- **File-naming inconsistency is the single biggest consistency failure** (covered in Category 1). Seven files use PascalCase (`Rocket.cs`, `CameraController.cs`, etc.), eleven use kebab-case. This is not a minor style issue — it actively confuses `ls`, file search, and tab-completion.
- **`ExplosionEffect` fields are private instance with inline initialization** (`private int _burstCount = 30`) while similar values in `RocketTrail` are `[SerializeField]` fields. Different pattern for the same type of data.
- **`ObstacleSpawner.CreateObstacle` uses `go.layer = LayerMask.NameToLayer("Default")`** — a literal string `"Default"` outside `GameConstants`. The tag (`GameConstants.TagGround`) is correctly centralized but the layer is not.
- **`Rocket.cs` declares local `const` aliases** (`private const string GroundTag = GameConstants.TagGround`) — unnecessary indirection. Use `GameConstants.TagGround` directly.

**Recommendation:** Migrate PascalCase filenames to kebab-case. Add `LayerDefault = "Default"` to `GameConstants`. Remove the redundant local const aliases in `Rocket.cs`.

---

### 7. Scalability & Maintainability — 6.5/10

**Adding a new weapon type:**
Currently impossible without significant restructuring. `LaunchController` is tightly coupled to `Rocket`. To add a second weapon, you would need to: abstract a `ILaunchable` interface on `Rocket`, inject it into `LaunchController`, and handle per-weapon trajectory math. The architecture supports this but the scaffolding does not exist. **Effort: Medium** (2-3 days).

**Adding multiplayer:**
Hard. `RoundManager` is a singleton-adjacent pattern. `AudioManager` and `RoundManagerHUD` are true singletons. Static classes (`GroundScorch`, `RocketDebris`, `RuntimeSpriteFactory`) have no per-player context. `GameRoundTracker` is per-instance — the one correctly designed class for multi-player. **Effort: Large** (full rewrite of static layer).

**Adding new obstacle types:**
Easy. `ObstacleSpawner.CreateObstacle` adds a `BoxCollider2D` and a sprite — adding a new shape type requires only a new branch in `CreateObstacle` or a new spawner subclass. Tagged `GameConstants.TagGround` collision is picked up automatically. **Effort: Small** (< 1 day).

**Technical debt level:** Low-medium. The codebase is clean but has structural patterns (static utility classes, parallel lists, `Input` legacy system) that will compound with scale.

**Documentation adequacy:** Good at the class level (every class has a `<summary>` doc comment). Thin at the system level — no document explains how `ObstacleSpawner` trajectory math works, why `RocketDebris` uses manual gravity instead of `Rigidbody2D`, or the crater mask rendering technique. These are non-obvious decisions that new developers will question.

**Recommendation:**
1. Introduce `ILaunchable` on `Rocket` now — zero cost, eliminates the largest scalability blocker.
2. Replace `GroundScorch` parallel lists with a struct immediately — low effort, high correctness gain.
3. Add a `ARCHITECTURE.md` or inline doc block in `ObstacleSpawner` explaining the trajectory algorithm.

---

## Critical Issues

None. No security vulnerabilities, data corruption risks, or breaking bugs found.

---

## High Priority Findings

1. **`GroundScorch` parallel lists** (`_craterXPositions`, `_craterWidths`, `_craterDepths`) — silent data corruption if any list gets out of sync. Replace with `private struct CraterData`.

2. **Legacy `Input` system** — `Input.GetMouseButtonDown` / `Input.mousePosition` in `LaunchController`. Unity 6 default is the new Input System; this is the critical gap for mobile viability.

3. **`Rocket.cs` is statically coupled to `ExplosionEffect` and `RocketDebris`** — physics class directly instantiates visual effects. Fire a data event instead; let a dedicated handler wire effects.

4. **No tests for `RoundManager`, `CameraController`, `LaunchController`** — the three most complex stateful classes have 0% test coverage.

5. **Weak test assertion**: `RespawnObstacles_CalledTwice` asserts `secondCount <= firstCount * 2` which passes even if obstacles doubled.

---

## Medium Priority Improvements

1. **Migrate PascalCase filenames** to kebab-case — `Rocket.cs` → `rocket.cs`, `CameraController.cs` → `camera-controller.cs`, etc. (6 files).

2. **`ExplosionEffect` tuning values** (`_burstCount`, `_particleLifetime`, `_startSpeed`, `_startSize`) — make `[SerializeField]` or move to config.

3. **`Camera.main` fallback in `LaunchController.Update`** — replace with `[SerializeField] private Camera _camera` wired at scene setup time.

4. **Object pooling for `ExplosionEffect` and `RocketDebris`** — current instantiate/destroy causes GC spikes on mobile.

5. **`partial class RoundManager` split** — refactor `HandleAutoPlay` / `HandleRestart` into a separate `AutoPlayController` MonoBehaviour to eliminate the partial class.

6. **`"Default"` layer string in `ObstacleSpawner.CreateObstacle`** — add `LayerDefault` constant to `GameConstants`.

7. **`BuildMaskSprite` in `GroundScorch`** — use `SetPixels` array instead of 4,096 individual `SetPixel` calls.

8. **Add boundary tests for `ObstacleSpawner.CalculateTrajectory`** — discriminant < 0 path, negative dx, degenerate target position.

---

## Low Priority Suggestions

1. Remove redundant const aliases in `Rocket.cs` (`GroundTag`, `TargetTag`) — use `GameConstants` directly.

2. `ProceduralAudioClipGenerator` uses `System.Random` — either document why or switch to `UnityEngine.Random` for consistency.

3. `GetTagManager()` in editor tool creates a new `SerializedObject` per call — cache it within `SetupTags()` and `SetupSortingLayers()` if called together.

4. `Assets/UI/` folder — audit for orphaned assets.

5. Decide on and document test naming convention (`PascalCase` vs `snake_case`) — current tests use PascalCase, `test-standards.md` specifies snake_case.

6. Add `Assets/Config/` or `Assets/ScriptableObjects/` folder as a structural placeholder for future data-driven configuration.

---

## Positive Observations

- **`GameRoundTracker`** — perfect class: plain C#, single responsibility, fully tested, zero Unity dependency. Model for everything else.
- **`CameraController` coroutine guard** (`_activeCoroutine`) — production-grade pattern, prevents all race conditions.
- **`RuntimeSpriteFactory`** — correct DRY solution for shared GPU resources, correct `ResetStaticState` lifecycle.
- **`RuntimeInitializeOnLoadMethod(SubsystemRegistration)`** on ALL static classes — demonstrates understanding of Unity domain reload. Most Unity codebases miss this entirely.
- **Editor tool (`SceneSetupTool`)** — auto-wires all references via `SerializedObject`, handles TMP import via reflection for Unity 6 compatibility, supports batch mode CI. This is genuinely advanced editor tooling.
- **`ProceduralAudioClipGenerator`** — zero external audio file dependency, correct 44100Hz mono generation, musically correct win jingle (C5-E5-G5-C6 arpeggio).
- **`ObstacleSpawner` trajectory math** — correct parabolic arc with discriminant guard, high-arc vs low-arc selection, force clamping before angle solve (not after) — the physics is done right.
- **`GroundScorch` Perlin noise mask** — creative, non-trivial visual effect with correct SpriteMask + `VisibleOutsideMask` + `VisibleInsideMask` layering.
- **All `OnDestroy` event unsubscriptions** — zero event leaks found in any class.

---

## Recommended Actions (Priority Order)

1. **[High]** Replace `GroundScorch` parallel lists with `private struct CraterData` — 10 min, eliminates silent corruption risk.
2. **[High]** Decouple `Rocket` from `ExplosionEffect`/`RocketDebris` via a data event.
3. **[High]** Add `RoundManagerTests` covering miss/hit/auto-play state transitions.
4. **[High]** Fix weak assertion in `RespawnObstacles_CalledTwice`.
5. **[Medium]** Migrate to Unity Input System — required for mobile production.
6. **[Medium]** Migrate 6 PascalCase filenames to kebab-case.
7. **[Medium]** Replace `Camera.main` in `LaunchController` with `[SerializeField]`.
8. **[Medium]** Add object pooling for `ExplosionEffect` and `RocketDebris`.
9. **[Low]** Expose `ExplosionEffect` tuning values as `[SerializeField]`.
10. **[Low]** Add `CraterData`-level boundary tests for trajectory math.

---

## Metrics

- Type Coverage: ~95% (all public APIs have clear types; `object` / `var` used appropriately)
- Test Coverage: ~30% of runtime classes by count (6/18 classes have any test); 0% for the 3 most complex classes
- Linting Issues: 0 syntax errors; 4 style/consistency issues (parallel lists, redundant const aliases, legacy Input, mixed file naming)
- Files over 200 LOC: 0 (longest is `CameraController.cs` at ~254 lines including blank lines and comments — borderline)

---

## Overall Verdict

**Not world-class. Score: 7.4/10.** Well above average for a solo/small-team Unity project — clean architecture, no leaks, genuine editor tooling, and correct physics math. The gap to world-class is specific and closeable: migrate the Input System, add pooling, fix the structural coupling of `Rocket` to its effects, strengthen test coverage to the three untested behaviour classes, and unify file naming. None of these are fundamental redesigns — they are known technical debt items that have been deferred. The code quality trajectory is clearly positive (commit history shows 4 rounds of improvement); executing the 10 recommendations above would bring this to 9/10.

---

## Unresolved Questions

1. What is in `Assets/UI/`? If it contains unused assets, it should be deleted.
2. Is mobile (iOS) a committed target? If yes, Input System migration is blocking, not optional.
3. Why does `RocketDebris` use manual gravity (`_velocity.y -= Gravity * Time.fixedDeltaTime`) instead of `Rigidbody2D`? The comment says "avoid fall-through" but with `CollisionDetectionMode2D.Continuous` available, this deserves explicit documentation.
4. `Assets/Prefabs/` exists but appears empty. Intentional? The editor tool builds everything procedurally — is there a plan to prefab any objects?
