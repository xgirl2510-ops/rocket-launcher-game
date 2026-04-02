# Code Review — World Class Audit

**Date:** 2026-04-02
**Scope:** Full codebase — all runtime scripts (`Assets/Scripts/`) and editor tools (`Assets/Editor/`)
**Lines analyzed:** ~2,091 runtime + ~792 editor = ~2,883 total
**HEAD SHA:** 28636571 (trail color gradient tweak, 1 file changed)
**Base SHA:** decf92ff

---

## Overall Assessment

This is a **well-crafted, intentional prototype** — not a default Unity mess. The author clearly understands C#, Unity lifecycle rules, and has put effort into architecture. Comments are accurate and useful. Domain reload safety is handled explicitly. For a solo prototype, this is genuinely good work.

**However, it is not world-class.** The gap is not about bugs — there are very few. The gap is structural: the code is written for one game session played once, not for a maintainable, extensible game system. Key world-class gaps: no pooling, static global state with fragile lifetime, God-class `LaunchController`, missing `IDisposable`/cleanup discipline on dynamic objects, and zero tests.

---

## Scores

| Dimension | Score | One-line verdict |
|---|---|---|
| Architecture & Organization | 6/10 | Good folder split, but `LaunchController` is a God class and there's no game-state machine |
| Code Quality | 7/10 | Readable, commented, consistent naming — let down by partial-class split motivation |
| Unity Best Practices | 7/10 | Domain reload handled, lifecycle correct — but no pooling, `new Material()` leak, `Camera.main` in Update |
| Maintainability | 6/10 | Adding a 2nd rocket type or new round mode means touching 4 files; no interfaces |
| Performance | 5/10 | `new float[samples]` allocations at Awake, per-frame `GetGroundY` O(N) loop, `new Material()` in trail |
| Robustness | 6/10 | Null-guards are thorough on inspector refs; dynamic-GO lifecycle and static list hygiene are fragile |
| Scalability | 4/10 | Static lists, singleton AudioManager, no pooling, no scene-management abstraction — all break at scale |

**Overall: 6/10 — Solid prototype, not world-class.**

---

## Critical Issues

### C1 — `new Material()` memory leak in `RocketTrail.CreateTrailParticleSystem()`

```csharp
// Assets/Scripts/Effects/rocket-trail-particle-effect.cs line 107
renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
```

Every time `CreateTrailParticleSystem()` runs and the rocket is reset, a new `Material` is created and the old one is **never destroyed**. In a long session this will accumulate until the GPU runs out of memory. Unity will print "Leaking X objects" warnings.

**Fix:**
```csharp
// Cache at class level and destroy in OnDestroy()
private Material _trailMaterial;
// ...
_trailMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
renderer.material = _trailMaterial;

private void OnDestroy()
{
    if (_trailMaterial != null) Destroy(_trailMaterial);
}
```

Or better: assign a Material reference via `[SerializeField]` so it's a project asset, not a runtime alloc.

---

### C2 — `Shader.Find()` at runtime — fragile and slow

```csharp
renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
```

`Shader.Find` searches by string at runtime. If the shader is stripped by build stripping (which happens by default in release builds), the material shows **pink** in production. This is a ship-stopper bug on mobile.

**Fix:** Assign shader/material through a `[SerializeField]` reference in the inspector, or add it to `Always Included Shaders` in Graphics settings.

---

### C3 — Static `_allDebris` / `_allCraters` lists survive scene reloads incorrectly without domain reload

`RocketDebris._allDebris`, `GroundScorch._allCraters` etc. are static fields. The `[RuntimeInitializeOnLoadMethod]` resets them on subsystem registration — which is correct for domain-reload-disabled builds. **However**, if the game is restarted via `SceneManager.LoadScene()` (not via domain reload), these methods are NOT called. The lists will contain stale `null` or destroyed references and silently skip cleanup.

The current `ClearAll()` calls handle the round-restart case, but if a scene reload path is ever added, this will cause bugs.

**Fix:** Either use `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` (fires on every scene load) or move state into a MonoBehaviour singleton with proper `OnDestroy`.

---

## High Priority Findings

### H1 — `LaunchController` is a God class (385 lines split into 2 files)

`LaunchController` manages: input, physics, audio, camera coordination, HUD state, round tracking, auto-play demo, look-target feature, restart flow, and hint display. That is 8 responsibilities.

The partial-class split into `launch-controller-hud-management.cs` creates an **illusion of separation** — it's still one class in the compiler's view. Partial classes are a C# tool for generated code (WinForms, Source Generators), not for organizing hand-written logic.

**What world-class looks like:**
- `InputHandler` — mouse/touch drag, fires events
- `LaunchController` — only launch physics (force/direction computation)
- `RoundManager` — miss counts, round tracking, restart
- `HUDController` — all TextMeshPro updates
- `AutoPlayController` — demo trajectory logic

**Impact:** Adding a second launch mechanic, a touch-screen version, or a multiplayer mode requires touching this file everywhere.

---

### H2 — `Camera.main` called via `_camera = Camera.main` in `Awake()` — acceptable, but naming is misleading

This is fine performance-wise (cached once). No real issue here except: the cached variable `_camera` in `LaunchController` is set in `Awake()` but never null-checked after that. If `Main Camera` is destroyed mid-game, this silently crashes. Minor.

---

### H3 — `GroundScorch.GetGroundY()` is O(N) per debris piece per `FixedUpdate`

```csharp
// rocket-debris-shatter-effect.cs line 134
float groundY = GroundScorch.GetGroundY(transform.position.x) + _groundYOffset;
```

Every debris piece calls `GetGroundY()` every `FixedUpdate`, which loops through all craters. With 16 debris + dirt debris (up to 20) + target debris (20) = ~56 debris pieces, each calling `O(craters)` per fixed frame, this multiplies quickly.

In practice craters max out at 3-4 so it's fine **now**. But the pattern is fragile for scale.

**Fix for world-class:** Spatial hash or debris registering its local crater Y at spawn rather than re-querying every frame.

---

### H4 — `ProceduralAudioClipGenerator`: `new float[samples]` allocates on Awake path

```csharp
// ProceduralAudioClipGenerator.cs
var data = new float[samples]; // e.g. 44100 * 0.8 = ~35K floats per clip
```

4 procedural clips * average ~22K floats each = ~350KB of managed heap allocations at startup. For a mobile game this is non-trivial GC pressure. The clips themselves are fine to keep, but the generation arrays could be avoided with pre-baked `.wav` assets.

---

### H5 — `ObstacleSpawner.CreateSquareSprite()` tries `Resources.Load<Sprite>("ObstacleSquare")` which will never succeed

```csharp
var existing = Resources.Load<Sprite>("ObstacleSquare"); // Always returns null
```

There is no `Resources/ObstacleSquare` asset in the project. This dead code path runs every time `CreateSquareSprite()` is called before the sprite is cached. It's a no-op, but it's confusing.

**Fix:** Remove the `Resources.Load` fallback — it's unreachable.

---

### H6 — `ExplosionEffect.Spawn()` allocates a new `GameObject` + `ParticleSystem` per explosion — no pooling

```csharp
var go = new GameObject("Explosion");
go.AddComponent<ExplosionEffect>();
```

Same pattern in `RocketDebris.SpawnInternal()` — 16–56 new GameObjects per impact. On mobile this will stall for 2-5ms on the frame of impact. Not game-breaking but visible as a hitch.

**Fix:** `ObjectPool<GameObject>` (Unity 2021+ built-in) or simple manual pool.

---

## Medium Priority Improvements

### M1 — `CameraController` state machine is an enum + if-statement, not a state pattern

```csharp
public enum CameraState { Intro, Idle, Following, Landed, Returning, LookingAtTarget }
// Transitions scattered as: _currentState = CameraState.Following; etc.
```

There's no enforcement that states are entered/exited correctly. `Landed` state is set by `HandleRocketLanded` and `HandleRocketHitTarget` but there's nothing that actually uses the `Landed` state in `LateUpdate`. It's set but never acted upon — dead state.

**Fix:** Either remove `Landed` (it serves no purpose) or use a proper state pattern with `Enter()`/`Exit()` hooks.

---

### M2 — `RotateRocketToDirection` is duplicated between `LaunchController` and `Rocket.Launch()`

```csharp
// LaunchController.cs line 344
private void RotateRocketToDirection(Vector2 direction)
{
    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
    _rocket.transform.rotation = Quaternion.Euler(0f, 0f, angle);
}

// Rocket.cs line 42
float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
transform.rotation = Quaternion.Euler(0f, 0f, angle);
```

Identical logic in two places. DRY violation. `Rocket.cs` already does this in `Launch()` — `LaunchController` shouldn't need to pre-rotate at all, or it should call a `Rocket.SetFacingDirection()` method.

---

### M3 — `HandleRestart()` calls `StopAllCoroutines()` on `LaunchController` — could cancel unrelated coroutines if class grows

`StopAllCoroutines()` is a nuclear option. It works today because `LaunchController` only has one type of coroutine (`DelayedAction`). If a second coroutine is ever added (e.g., a tutorial sequence, a slow reveal), `HandleRestart()` would silently kill it.

**Fix:** Store the reload coroutine reference and `StopCoroutine(ref)` explicitly — same pattern already used correctly in `CameraController`.

---

### M4 — `GameConstants` is anemic — magic numbers remain scattered

`GameConstants.cs` defines `GroundTop`, `TagGround`, `TagTarget`. But physics magic numbers remain scattered:
- `Rocket.cs` line 113: `GameConstants.GroundTop + 1.5f` — unexplained 1.5f offset
- `ObstacleSpawner.cs`: `Gravity = 12f` hardcoded in `RocketDebris` — doesn't match `Physics2D.gravity.magnitude` (9.81 by default), silently inconsistent
- Editor tool: `CamY=2`, `CamOrthoSize=9`, etc. are separate from `GameConstants`

The editor constants in `rocket-launcher-scene-setup-environment-and-gameplay-objects.cs` duplicate `GameConstants.GroundTop` with a separate `private const float GroundTop = GameConstants.GroundTop` that's fine, but other layout constants (`CamY`, `CamOrthoSize`) are editor-only and have no runtime counterpart.

---

### M5 — `GroundScorch` modifies a live scene object globally (`maskInteraction`) via `PrepareGround()`

```csharp
private static void PrepareGround()
{
    var ground = GameObject.FindWithTag(GameConstants.TagGround);
    var sr = ground.GetComponent<SpriteRenderer>();
    sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
}
```

`GameObject.FindWithTag` at runtime is slow and fragile (tag typos, multiple tagged objects). This runs on first crater spawn — it modifies the Ground's `SpriteRenderer` as a side effect of spawning. That's a hidden coupling; the Ground's setup should be done at scene init, not lazily on first impact.

**Fix:** Move `PrepareGround()` into scene setup or `LaunchController.Start()`.

---

### M6 — `CameraController` has an unused `CameraState.Landed` state

As noted in M1. `Landed` is assigned but `LateUpdate` only checks `Following`. `Landed` has no behavior. Remove it or add behavior.

---

### M7 — No namespace — all classes in global namespace

Every class in the project is in the global namespace. For 14 scripts this is fine. For 50+ it becomes a collision risk (e.g., `CameraController` already has a naming collision with `TextMesh Pro`'s example `CameraController.cs`). Unity resolves this by path but it's confusing.

**For world-class:** Add `namespace RocketLauncher` or `namespace Game.Core` etc.

---

### M8 — `LaunchController._camera` set in `Awake()` but `RandomizeTarget()` also called in `Awake()` before references are guaranteed

`Awake()` order between MonoBehaviours is not guaranteed in Unity unless execution order is set. `RandomizeTarget()` in `LaunchController.Awake()` calls `_targetTransform` (set via inspector, fine) and `_obstacleSpawner.RespawnObstacles()` which calls `_spawnPoint.position`. This works if all refs are set in inspector, but if any ref is null, it silently does nothing rather than logging a clear error.

**Fix:** Move `RandomizeTarget()` to `Start()` where all refs are guaranteed to be `Awake`-initialized.

---

## Low Priority Suggestions

### L1 — File naming inconsistency: PascalCase vs kebab-case in same folder

Runtime scripts:
- `Assets/Scripts/Launch/LaunchController.cs` — PascalCase
- `Assets/Scripts/Launch/launch-controller-hud-management.cs` — kebab-case
- `Assets/Scripts/Effects/rocket-trail-particle-effect.cs` — kebab-case
- `Assets/Scripts/Effects/explosion-burst-particle-effect.cs` — kebab-case
- `Assets/Scripts/Camera/CameraController.cs` — PascalCase

Unity requires file names to match class names for MonoBehaviours. For plain C# / static classes and partial files the name can differ. The mix is confusing: some files are kebab-case because the modularization rules in the project require it (200-line limit), but Unity convention is PascalCase matching class name.

The project README/rules mandate kebab-case, so this is intentional — but it creates confusion when the same folder has `LaunchController.cs` (Unity-required name) alongside `launch-controller-hud-management.cs` (rule-required kebab).

---

### L2 — `DelayedAction` coroutine re-implemented from scratch vs `WaitForSeconds` pattern is fine

`DelayedAction` is a thin wrapper that adds no value over calling `StartCoroutine(SomeMethod())` with `yield return new WaitForSeconds(delay)` inside `SomeMethod`. The abstraction is marginally useful for lambda captures but the `WaitForSeconds` allocates anyway.

---

### L3 — `AudioManager.Instance` checked with `!= null` everywhere in `LaunchController`

9 separate `if (AudioManager.Instance != null)` guards in `LaunchController`. A null-safe extension method or a `TryGetInstance` pattern would reduce noise, though this is minor.

---

### L4 — `GroundScorch.BuildMaskSprite()` uses `SetPixel()` in a double loop (64*64 = 4096 individual calls)

`SetPixel` per-pixel is slower than `SetPixels32` with a pre-built array. 8 variants * 4096 calls = 32,768 `SetPixel` calls at startup. Fine for a one-time editor-type operation but `SetPixels32` would be cleaner.

---

## Positive Observations

1. **Domain reload safety is explicit and correct.** Every class with static state has `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`. This is genuinely advanced Unity knowledge that most tutorials skip entirely.

2. **Event cleanup in `OnDestroy()` is thorough.** Both `LaunchController` and `CameraController` unsubscribe from events. This prevents the most common Unity memory leak pattern.

3. **Physics correctness.** `FixedUpdate` for physics, `LateUpdate` for camera follow, `Update` for input. No violations. `MoveRotation()` used in `Rocket.FixedUpdate()` instead of direct transform mutation — correct.

4. **`GameConstants` as single source of truth for tags and layout.** Editor tool reads from `GameConstants.GroundTop` instead of duplicating the constant. Correct.

5. **Coroutine race condition prevention in `CameraController`.** `_activeCoroutine` pattern with `StopActiveCoroutine()` before starting a new one is the right pattern. Many developers miss this.

6. **Procedural audio is clever.** Generating AudioClips from math at runtime is unconventional but works, ensures no asset dependencies for basic sounds, and demonstrates real understanding of PCM.

7. **`ObstacleSpawner` trajectory math.** Computing a ballistic arc using the actual 2-solution angle formula (not a hardcoded angle) to guarantee a clear path is non-trivial and done correctly.

8. **Sprite caching (`GetOrCreateSprite`, `_cachedSquareSprite`).** The debris and obstacle spawners don't re-allocate a texture per object — they cache a single shared Sprite. Correct.

9. **Editor tool coverage.** A full setup tool that wires all references via `SerializedObject` (not `[RequireComponent]` or manual Unity editor dragging) means the scene can be rebuilt from scratch in one click. Production teams use exactly this pattern.

---

## Recommended Actions (Prioritized)

1. **[Critical] Fix `new Material()` leak in `RocketTrail`** — assign via `[SerializeField]` instead of `Shader.Find`. This is a shipping bug on mobile.

2. **[Critical] Remove/fix `Shader.Find("Particles/Standard Unlit")`** — shader will be stripped in release builds and appear pink.

3. **[High] Introduce `ObjectPool` for `ExplosionEffect` and `RocketDebris`** — eliminates per-impact GC hitch. Unity 2021+ has `UnityEngine.Pool.ObjectPool<T>` built in.

4. **[High] Split `LaunchController` into `InputHandler` + `RoundManager` + keep `LaunchController` for physics only** — this is the most important maintainability improvement. The partial-class split is cosmetic; real separation requires separate classes.

5. **[High] Remove `Resources.Load<Sprite>("ObstacleSquare")` dead code** — misleading, always returns null.

6. **[Medium] Remove or implement `CameraState.Landed`** — dead state causes confusion when reading the state machine.

7. **[Medium] Move `PrepareGround()` call out of `GroundScorch.Spawn()`** — lazy side-effect mutation of ground's SpriteRenderer is a hidden coupling. Call it in `LaunchController.Start()` or editor setup.

8. **[Medium] Replace `StopAllCoroutines()` in `HandleRestart()` with a stored coroutine reference** — safer, follows the pattern already in `CameraController`.

9. **[Medium] Fix the `Gravity = 12f` constant in `RocketDebris`** — either sync with `Physics2D.gravity.magnitude` or explicitly document why it differs (artistic choice is fine, but it should be in `GameConstants`).

10. **[Low] Move `RandomizeTarget()` from `Awake()` to `Start()`** — avoids Awake ordering dependency.

---

## Metrics

| Metric | Value |
|---|---|
| Runtime files | 14 scripts, ~2,091 lines |
| Editor files | 4 scripts, ~792 lines |
| Files over 200 lines | 4 (CameraController 281, GroundScorch 227, ObstacleSpawner 222, LaunchController 385) |
| Identified bugs | 2 (material leak, shader strip) |
| Dead code instances | 2 (`Resources.Load` fallback, `Landed` state) |
| Missing tests | 100% — zero test files |
| Namespaces used | 0 — all global namespace |

---

## Gap to World-Class

World-class for this scope requires:
- **Object pooling** for all runtime-spawned GameObjects
- **Explicit game state machine** (title → playing → win → restart) rather than scattered `_inputEnabled` flags and UI show/hide calls
- **Interface-driven audio** (`IAudioService`) so `LaunchController` doesn't hard-couple to `AudioManager.Instance`
- **At minimum smoke tests** for trajectory calculation and round tracking logic
- **No `Shader.Find` or `new Material()` at runtime**
- **Namespaces** to prevent the already-present `CameraController` name collision with TMP examples

The code is at a "strong senior prototype" level — well above average Unity hobby projects, below production-shipped game standards.

---

## Unresolved Questions

1. Is `RocketDebris.Gravity = 12f` intentional (feels heavier for game feel) or accidental mismatch with `Physics2D.gravity`? If intentional, add a comment.
2. Why does the restart button label say "CONTINUE" in editor setup but the code/comments call it "Restart"? Naming inconsistency across UI and code.
3. `LaunchController._lastLaunchForce` is stored by `ObstacleSpawner` — is `SafeLaunchForce` expected to work before `RespawnObstacles()` is called? Currently returns 0 with no warning.
4. No scene management abstraction — if a title screen or level select is ever added, where does that live? Not defined.
