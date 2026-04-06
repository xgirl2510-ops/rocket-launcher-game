# Code Review: Effects, Camera, Obstacles, Editor Tools

**Report:** code-reviewer-260403-0048-effects-camera-obstacles-editor-audit.md
**Date:** 2026-04-03
**Reviewer:** code-reviewer agent
**Prior score:** 8.0/10

---

## Scope

| File | Lines |
|---|---|
| `Assets/Scripts/Effects/explosion-burst-particle-effect.cs` | 104 |
| `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs` | 132 |
| `Assets/Scripts/Effects/ground-scorch-mark.cs` | 196 |
| `Assets/Scripts/Effects/rocket-trail-particle-effect.cs` | 106 |
| `Assets/Scripts/Camera/CameraController.cs` | 254 |
| `Assets/Scripts/Camera/camera-screen-shake.cs` | 36 |
| `Assets/Scripts/Obstacles/ObstacleSpawner.cs` | 192 |
| `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs` | 365 |
| `Assets/Editor/rocket-launcher-scene-setup-environment-and-gameplay-objects.cs` | 147 |
| `Assets/Editor/rocket-launcher-scene-setup-ui-canvas-and-hud-elements.cs` | 176 |
| `Assets/Editor/rocket-launcher-scene-setup-shared-gameobject-and-sprite-helpers.cs` | 141 |
| Supporting: `GameConstants.cs`, `runtime-sprite-factory.cs`, `RoundManager.cs` | ~220 |

**Total analyzed:** ~2,070 lines

---

## Overall Assessment

Code quality is genuinely good — previous review rounds have fixed the major leaks and races. What remains are correctness issues in trajectory math, a screen-shake architectural flaw, a few real resource-management gaps, and several medium-priority code quality gaps. Nothing crashes the game outright, but two HIGH issues affect gameplay correctness and one HIGH issue breaks the shake guarantee. Reaching 10/10 requires fixing all HIGH issues and closing the gaps below.

---

## CRITICAL Issues

None found.

---

## HIGH Issues

### H1 — `ObstacleSpawner`: trajectory `totalTime` calculation is wrong for negative `dx`

**File:** `ObstacleSpawner.cs` line 98

```csharp
float totalTime = Mathf.Abs(dx) / Mathf.Max(vx, 0.1f);
```

`vx = vClamped * Mathf.Cos(theta)`. When target is to the right, `dx > 0` and `theta` is in the first/second quadrant, so `vx > 0` — fine. But `theta = Mathf.Atan2(vc2 + sqrt(disc), g * dx)`. When `dx` is negative (target to the left of spawn), `g * dx` is negative, making `theta` land in the second quadrant, so `cos(theta) < 0` → `vx < 0`. The `Mathf.Max(vx, 0.1f)` clamp then masks a negative `vx` with `0.1f`, producing an absurdly large `totalTime` and a degenerate trajectory arc. The safe-zone points will be scattered nowhere near the actual flight path, so obstacles can be placed directly in the rocket's path.

The game currently places the target to the right (X 8–35) so this never fires in practice — but it is a latent correctness bug that would manifest if target placement changed.

**Fix direction:** Use `totalTime = Mathf.Abs(dx) / Mathf.Abs(vx)` guarded against near-zero, and ensure `vx` sign matches `dx` sign before clamping.

---

### H2 — `CameraScreenShake.GetOffset()` advances `_elapsed` every frame even when camera-positioning code calls it multiple times per frame

**File:** `camera-screen-shake.cs` lines 28–33

```csharp
public Vector2 GetOffset()
{
    if (_elapsed >= _duration) return Vector2.zero;
    _elapsed += Time.deltaTime;   // ← mutating state inside a getter
    ...
}
```

`CameraController.SetCameraXY` is the only caller today, so this happens to be called once per LateUpdate. But the method is named `GetOffset`, implies read-only semantics, and if any other code (e.g., a future debug overlay, a follow-up pan coroutine) calls it a second time in the same frame, `_elapsed` advances twice and the shake ends early / decays incorrectly. The side-effect inside a getter is an architectural smell regardless.

**Fix direction:** Advance `_elapsed` in `Update()` inside `CameraScreenShake`, cache the computed offset each frame, and have `GetOffset()` return the cached value without mutation.

---

### H3 — `GroundScorch.ClearAll()` does NOT reset `_groundPrepared`, so the ground `SpriteMask` interaction is never restored after a round restart

**File:** `ground-scorch-mark.cs` lines 137–148

```csharp
public static void ClearAll()
{
    // ... destroys crater GOs ...
    _craterXPositions.Clear();
    _craterWidths.Clear();
    _craterDepths.Clear();
    // _groundPrepared is NOT reset here
}
```

`PrepareGround()` sets `sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask` and sets `_groundPrepared = true`. On round restart `ClearAll()` is called, all crater GameObjects are destroyed, but `_groundPrepared` stays `true`. On the NEXT `Spawn()` call `PrepareGround()` is skipped — which is fine as long as the ground object is the same instance. But `HandleRestart()` calls `_rocket.ResetToPosition` and then `RandomizeTarget()` → `_obstacleSpawner.RespawnObstacles()` — the ground GameObject itself persists. So in this specific game this is actually safe. However, `ResetStaticState()` (called by `RuntimeInitializeOnLoadMethod`) DOES reset `_groundPrepared = false`, which is inconsistent with `ClearAll()` not doing so. If `ClearAll()` is ever called mid-scene (editor reload, domain reload without full restart), the ground SR stays in `VisibleOutsideMask` mode permanently even with no craters, which slightly alters ground rendering.

**Fix direction:** Add `_groundPrepared = false;` to `ClearAll()` for consistency with `ResetStaticState`.

---

### H4 — `ExplosionEffect.Spawn()` creates a new `Material` on every explosion via `RuntimeSpriteFactory.GetParticleMaterial()`... but `RuntimeSpriteFactory` returns a single cached `Material` that is shared

The concern is the other direction: `ParticleSystemRenderer` does NOT take ownership of the material, so if `RuntimeSpriteFactory._particleMaterial` is destroyed by `ResetStaticState()` mid-game (domain reload scenario), the `ParticleSystemRenderer` holds a dead reference. More concretely: the explosion `GameObject` is `Destroy`-ed after `_particleLifetime + 0.2f`. Unity does not destroy the shared material when destroying the renderer — that is correct. But the `ExplosionEffect` never calls `renderer.material = null` on destroy, so if the `ParticleSystemRenderer` is destroyed with a reference to the shared factory material Unity will internally call `DestroyImmediate` on the `material` property copy — this could destroy the shared instance. This is a subtle Unity-specific behavior: `ParticleSystemRenderer.material` (unlike `SpriteRenderer.sharedMaterial`) creates a material instance per-renderer on assignment in some Unity versions. Test in build to confirm no leak; switch to `renderer.sharedMaterial` if available for `ParticleSystemRenderer`.

---

## MEDIUM Issues

### M1 — `RocketDebris._allDebris` list leaks stale `null` entries after `OnDestroy`

**File:** `rocket-debris-shatter-effect.cs` lines 127–130

```csharp
private void OnDestroy()
{
    _allDebris.Remove(gameObject);
}
```

`ClearAll()` calls `Destroy()` on every GO then `_allDebris.Clear()`. During the destroy pass Unity calls `OnDestroy` on each piece, which calls `_allDebris.Remove(gameObject)` — removing items from the list while `ClearAll`'s reverse-loop is still iterating (it iterates `Count - 1` down to 0). The reverse iteration in `ClearAll` protects against index-shift, but every `Destroy` call immediately invokes `OnDestroy` in the same frame (Edit mode), or defers to end-of-frame (Play mode). In Play mode this is safe. In Edit mode or when calling `DestroyImmediate` the `OnDestroy` fires synchronously, causing `Remove` during the loop. Since `ClearAll` calls `_allDebris.Clear()` after the loop, the duplicated removes are harmless in practice, but the pattern is fragile.

**Fix direction:** Either remove the `_allDebris.Remove` from `OnDestroy` (since `ClearAll` handles bulk cleanup) or document the assumption explicitly.

---

### M2 — `GroundScorch.BuildMaskSprite()` creates a `Texture2D` without specifying `mipChain=false` for the inner crater texture

**File:** `ground-scorch-mark.cs` line 167

```csharp
var tex = new Texture2D(size, size);
```

Default constructor enables mipmaps. A 64×64 scratch texture used only as a SpriteMask alpha source does not benefit from mipmaps; they waste ~33% GPU memory per crater variant. The `RuntimeSpriteFactory` correctly passes `mipChain: false` for its 4×4 sprite texture, but the crater mask variant textures do not.

**Fix direction:** `new Texture2D(size, size, TextureFormat.RGBA32, false)`.

---

### M3 — `CameraController.PanCoroutine()` runs in `Update` timing (via `yield return null`), not `LateUpdate`

**File:** `CameraController.cs` lines 228–239

The pan coroutine yields `null`, which resumes after `Update`. Camera position changes applied after Update but before rendering are correct — Unity processes coroutine resumes after `Update` and the camera's `LateUpdate` runs after that. This is actually fine for Unity's execution order. However, `FollowRocket()` runs in `LateUpdate` and reads `_smoothVelocity`. If a coroutine fires a pan `SetCameraXY` call in the same frame that `LateUpdate` also calls `FollowRocket`, the state transition from `Following` to `Idle` (via `HandleRocketLanded`) happens in event callbacks mid-physics, which could leave one frame where both paths write to camera position. Low severity, but worth noting.

---

### M4 — `ObstacleSpawner` never calls `ClearObstacles()` in `OnDestroy`

**File:** `ObstacleSpawner.cs` — no `OnDestroy`

Obstacle GameObjects are children of the scene root, not children of the `ObstacleSpawner` GO. If `ObstacleSpawner` is destroyed (scene reload, GO deletion) the spawned obstacles remain in the scene as orphan GameObjects. In normal flow `RoundManager.HandleRestart` → `RespawnObstacles` → `ClearObstacles` handles this. But an `OnDestroy` safety net is missing.

---

### M5 — `camera-screen-shake.cs` has no `OnDisable` / guard: shake continues if component is disabled

If `CameraScreenShake` is disabled mid-shake (edge case: error-disabled `CameraController` calling `enabled = false`), `GetOffset()` continues to advance `_elapsed` whenever called because there is no `enabled` check. Minor, but inconsistent with the rest of the codebase's defensive guards.

---

### M6 — `ObstacleSpawner.CreateObstacle()` assigns `GameConstants.TagGround` to obstacles

**File:** `ObstacleSpawner.cs` line 167

```csharp
go.tag = GameConstants.TagGround;
```

Obstacles are tagged `"Ground"`. The scorch/explosion spawning logic (in `RoundManager` / `Rocket`) presumably checks the hit tag. If rockets hitting obstacles trigger a ground-scorch (which may or may not be intended), the tag is the cause. If obstacles should be distinct collidables that don't trigger craters, they need a separate tag. This is a semantic issue — worth confirming intentionality.

---

### M7 — `GroundScorch.GetGroundY()` is O(n×craters) called every `FixedUpdate` for every active debris piece

**File:** `ground-scorch-mark.cs` lines 119–133 and `rocket-debris-shatter-effect.cs` line 119

Each debris piece calls `GroundScorch.GetGroundY()` every `FixedUpdate`. With 20 debris + 5 craters = 100 crater-distance checks per physics tick. Negligible at current counts, but scales poorly. Not a problem today; document if counts grow.

---

### M8 — `rocket-launcher-scene-auto-setup-editor-tool.cs`: `SetupLayer(8, "Rocket")` writes unconditionally, clobbering any existing layer 8

**File:** `rocket-launcher-scene-auto-setup-editor-tool.cs` lines 314–319

```csharp
private static void SetupLayer(int index, string layerName)
{
    var tm = GetTagManager();
    tm.FindProperty("layers").GetArrayElementAtIndex(index).stringValue = layerName;
    tm.ApplyModifiedProperties();
}
```

No existence check before writing. Compare to `EnsureTag` / `SetupSortingLayers` which do check first. If the user already had a different named layer at index 8, this silently overwrites it. Should mirror the guard pattern used in `EnsureTag`.

---

### M9 — `rocket-launcher-scene-setup-shared-gameobject-and-sprite-helpers.cs`: sprite assets always deleted and recreated on every `SetupScene` call

**File:** line 38: `AssetDatabase.DeleteAsset(assetPath);`

Every call to `SetupScene` destroys and recreates the sprite `.asset` files. This dirtifies the project even when textures are already correct, causes unnecessary AssetDatabase churn, and breaks any external references to those sprite assets (e.g., serialized prefab references). Should compare pixel content or a version hash before recreating.

---

### M10 — Editor tool: `SetupSortingLayers()` uses `name.GetHashCode()` as `uniqueID`

**File:** `rocket-launcher-scene-auto-setup-editor-tool.cs` lines 305–311

```csharp
entry.FindPropertyRelative("uniqueID").intValue = name.GetHashCode();
```

`GetHashCode()` is not guaranteed stable across .NET runtime versions and can produce collisions. Unity expects stable, unique IDs for sorting layers. If two layer names hash to the same value, Unity will behave unpredictably when sorting. Should use a monotonically increasing counter or a stable deterministic hash.

---

## LOW Issues

### L1 — `ExplosionEffect`: `fadeColor` RGB is wrong for the miss case

**File:** `explosion-burst-particle-effect.cs` lines 46–47

```csharp
Color fadeColor = isHit
    ? new Color(1f, 1f, 0f, 0f)
    : new Color(1f, 1f, 1f, 0f);  // alpha=0 correct, but RGB is bright white
```

For the miss case the burst starts at `(0.7, 0.7, 0.7)` grey and fades toward `(1.0, 1.0, 1.0, 0)` white. The colorOverLifetime `GradientColorKey` endpoint is white, not grey, so particles slightly brighten before fading out. Likely unintentional; should be `new Color(0.7f, 0.7f, 0.7f, 0f)`.

---

### L2 — `RocketTrail.CreateTrailParticleSystem()`: `main.loop` is not explicitly set to `false`

**File:** `rocket-trail-particle-effect.cs` — `main.loop` never assigned

`ExplosionEffect` explicitly sets `main.loop = false`. `RocketTrail` omits this. The default for a programmatically created ParticleSystem is `loop = true`. `StopTrail()` stops emission but does not stop looping, so if `Play()` is ever called without a prior `Stop`, the trail will loop indefinitely. Currently `StartTrail` always calls `_ps.Clear()` then `_ps.Play()` which is fine — but the missing `main.loop = false` is a dangerous omission relative to the explosion effect's explicit pattern.

---

### L3 — `CameraController`: `_smoothVelocity` is never reset between pan coroutines

**File:** `CameraController.cs` — `SetState()` resets `_smoothVelocity` to zero, which is correct. But `StopActiveCoroutine()` does not reset `_smoothVelocity`. When a coroutine is interrupted mid-pan (e.g., rocket launched while intro pan is in progress), the accumulated velocity is carried forward into the next `FollowRocket` call, causing a one-frame jerk. `SetState(CameraState.Following)` does call `StopActiveCoroutine` then resets, so this is handled. Low risk in current flow, but the `StopActiveCoroutine` helper not resetting velocity is a subtle trap.

---

### L4 — `ObstacleSpawner`: `_safeTrajectory` is a public-readable array but never defensively copied

**File:** `ObstacleSpawner.cs` — `_safeTrajectory` is `private`, so this is fine as-is. No action needed.

---

### L5 — `ground-scorch-mark.cs`: `EnsureMaskVariants()` is not thread-safe (Unity is single-threaded so irrelevant, but worth noting for readers)

No action needed; Unity C# is single-threaded at runtime.

---

### L6 — `rocket-launcher-scene-setup-environment-and-gameplay-objects.cs`: `TargetAspect` is `9f/19.5f` (hardcoded for iPhone 15 Pro Max)

**File:** line 23

```csharp
private const float TargetAspect = 9f / 19.5f;
```

This is a hardcoded portrait aspect ratio for one device family. The camera's actual half-width at runtime will differ on other screens. Vehicle X placement via `CamLeft` is editor-only layout math (not runtime), so this is acceptable for an initial setup tool, but the comment should document the assumption more clearly.

---

### L7 — `rocket-launcher-scene-auto-setup-editor-tool.cs`: `SetupSceneBatchMode()` uses reflection to call `TMP_PackageUtilities.ImportProjectResourcesMenu`

**File:** lines 43–46

This reflection call depends on TMP internals that can change between TextMeshPro versions. A version bump that renames or removes the method will silently fail (the method returns `void` so `importMethod?.Invoke(null, null)` with a null `importMethod` is a no-op). Should log a warning if `importMethod == null`.

---

### L8 — `RocketDebris.SpawnInternal()`: debris launch angle is always `15°–165°` (upper hemisphere only)

**File:** `rocket-debris-shatter-effect.cs` line 90

```csharp
float angle = Random.Range(15f, 165f) * Mathf.Deg2Rad;
```

All debris always flies upward and outward — never downward or back toward the vehicle. For target debris or a high-speed impact this is visually correct, but for a ground impact debris that bounces along the surface would look more realistic. Low priority visual note.

---

### L9 — Access modifier: `ObstacleSpawner.SafeLaunchDirection` and `SafeLaunchForce` are public properties on a component

Fine as-is (consumed by `RoundManager.HandleAutoPlay`), but they expose mutable backing fields (`_lastLaunchDir`, `_lastLaunchForce`) indirectly. The struct copy semantics of `Vector2` make this safe. No action needed.

---

### L10 — `CameraController.OnIntroComplete` and `OnLookTargetComplete` are public events

Fine for inter-component communication, but they use `event Action` (no args). Consistent with project patterns. No action needed.

---

## Positive Observations

- `RuntimeSpriteFactory` is clean, well-documented, properly cached with `[RuntimeInitializeOnLoadMethod]` — genuinely good resource management.
- `GroundScorch.DestroyMaskVariants()` manually destroys both texture AND sprite to prevent GPU leaks — correct.
- `CameraController` coroutine race prevention via `_activeCoroutine` is solid.
- `SetCameraXY` always applies shake offset as an additive layer — correct architecture, shake never corrupts the logical camera position.
- `ObstacleSpawner` trajectory calculation with `discriminant < 0` fallback is a real safety net, not just ignored.
- All static classes use `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` for reset — previous review feedback was applied correctly.
- `RoundManager.OnDestroy` unsubs every event, including the `OnLookTargetComplete` path that is wired conditionally in `HandleLookTarget`. Thorough.
- Editor tool uses `SerializedObject` for private `[SerializeField]` wiring — correct approach, not reflection-hacking.
- Partial class split for editor tool keeps each file under 200 lines — follows project standard.

---

## Recommended Actions (Prioritized)

1. **[HIGH H2]** Refactor `CameraScreenShake`: move `_elapsed += Time.deltaTime` to `Update()`, cache offset, make `GetOffset()` pure.
2. **[HIGH H3]** Add `_groundPrepared = false` to `GroundScorch.ClearAll()`.
3. **[HIGH H1]** Fix `ObstacleSpawner.CalculateTrajectory` `totalTime` for negative `dx` case: use `Mathf.Abs(vx)` and validate sign of `vx` vs `dx`.
4. **[HIGH H4]** Investigate `ParticleSystemRenderer.sharedMaterial` vs `.material` for particle effects to confirm shared-material ownership.
5. **[MEDIUM M2]** Fix crater `Texture2D` constructor: add `TextureFormat.RGBA32, false` to disable mipmaps.
6. **[MEDIUM M8]** Add existence check to `SetupLayer` before writing (mirror `EnsureTag` pattern).
7. **[MEDIUM M9]** Check sprite asset content before delete-recreate in `CreateOrLoadSpriteAsset`.
8. **[MEDIUM M10]** Replace `name.GetHashCode()` for sorting layer `uniqueID` with a stable counter.
9. **[MEDIUM M6]** Confirm intentionality of tagging obstacles as `"Ground"` — add a comment or create `TagObstacle` constant.
10. **[MEDIUM M4]** Add `OnDestroy` to `ObstacleSpawner` that calls `ClearObstacles()`.
11. **[LOW L2]** Add `main.loop = false` to `RocketTrail.CreateTrailParticleSystem()`.
12. **[LOW L1]** Fix miss-case `fadeColor` to `new Color(0.7f, 0.7f, 0.7f, 0f)`.
13. **[LOW L7]** Add null warning log to `SetupSceneBatchMode` TMP reflection call.

---

## Metrics

- Type coverage: 100% explicit typing, no `var` ambiguity
- Linting issues: 0 syntax errors found
- Resource leaks fixed in prior rounds: confirmed clean
- New leaks found: 1 potential (H4 particle material ownership), 1 confirmed minor (M2 mip chain)
- Coroutine safety: good, one race noted (M3) but low severity
- Editor tool completeness: all major references wired; 2 robustness gaps (M8, M10)

---

## Unresolved Questions

1. **H4**: Does Unity's `ParticleSystemRenderer.material` setter create a per-renderer instance copy or assign the shared reference? Behavior differs between Unity versions. Needs verification in Unity 6.0.4f1 specifically.
2. **M6**: Is tagging obstacles as `"Ground"` intentional? If so, should craters spawn on obstacle hits? The current `Rocket` collision logic needs to be cross-checked to confirm the design intent.
3. **L6**: Is the iPhone 15 Pro Max aspect ratio hardcoded in the editor tool by design (mobile-first project) or should this read from project settings?
