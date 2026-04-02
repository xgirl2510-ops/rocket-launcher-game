# Code Review — Round 4 World Class Assessment

**Date:** 2026-04-03
**Reviewer:** code-reviewer
**Scope:** ALL scripts — `Assets/Scripts/` + `Assets/Editor/`

---

## Code Review Summary

### Scope
- Files reviewed: 19 C# files (15 runtime, 4 editor)
- Lines of code: ~2,903 total
- Review focus: Full codebase, post-R3 fixes

### Overall Assessment

The codebase has matured significantly across four rounds. Architecture is clean, separation of concerns is solid, and the R3 fixes (force clamping, material lifecycle, DRY pan coroutine, layer constant) all landed correctly. What remains are a handful of medium and low issues — no critical or new high-severity ones. This is a well-engineered prototype.

---

## Scores

| Dimension | Score | Notes |
|---|---|---|
| 1. Architecture & Organization | 8.5/10 | Event-driven, clear SRP, GameConstants as SSOT. Minor: `RoundManager` still has a bidirectional ref with `LaunchController`. |
| 2. Code Quality | 8.5/10 | Readable, well-commented, consistent conventions. Residual: layer magic number `6` in editor tool. |
| 3. Unity Best Practices | 8.0/10 | `RuntimeInitializeOnLoadMethod` static resets, event cleanup in `OnDestroy`, `MoveRotation` for physics rotation. Missing: `Camera.main` cached only in `LaunchController`, not in `CameraController`. |
| 4. Maintainability | 8.5/10 | `GameRoundTracker` as plain C# class is excellent. Force constants in `GameConstants` now referenced properly. One lingering loose constant in `Rocket.cs`. |
| 5. Performance | 8.0/10 | Sprite caching, sqrMagnitude comparisons. `IsInSafeZone` is O(n*m) per frame (spawning only, acceptable). `GroundScorch.GetGroundY()` linear scan called every `FixedUpdate` per debris piece — could be non-trivial with many craters + many debris. |
| 6. Robustness | 7.5/10 | Null guards throughout. Several edge cases remain (see below). |
| 7. Scalability | 7.5/10 | Scene-level singleton pattern fine for single-scene game. Would not scale to multi-scene without `DontDestroyOnLoad` or a service locator, but YAGNI applies here. |

**Overall: 8.0 / 10**

Up from 8.0 (R3) — the targeted fixes were all applied correctly. Ceiling is now mostly robustness and a small number of code-quality items.

---

## Critical Issues

None.

---

## High Priority Findings

### H1 — Rocket layer hardcoded as `6` in editor tool (regression from stated fix)

`rocket-launcher-scene-setup-environment-and-gameplay-objects.cs`, line 110:
```csharp
go.layer = 6;
```
The fix description for R4 states "Editor layer changed from 6 to 8." The constant `SetupLayer(8, "Rocket")` is called in `RunCoreSetup()` (correct), but `CreateRocket()` **still assigns `go.layer = 6`** — the old, wrong value. The `"Rocket"` layer is registered at index 8, so the rocket is on the wrong physics layer at runtime.

**Fix:**
```csharp
go.layer = LayerMask.NameToLayer("Rocket"); // resolves to 8 after setup
```
Or define a constant and call after `SetupLayer`:
```csharp
private const int RocketLayerIndex = 8;
// ...
go.layer = RocketLayerIndex;
```
Using `LayerMask.NameToLayer` is preferred — decoupled from the index.

---

### H2 — `LaunchController._minLaunchForce/_maxLaunchForce` diverge from `GameConstants`

`LaunchController.cs` lines 23-24:
```csharp
[SerializeField, Range(3f, 15f)] private float _minLaunchForce = 5f;
[SerializeField, Range(10f, 40f)] private float _maxLaunchForce = 30f;
```
`GameConstants.MinLaunchForce = 5f` and `MaxLaunchForce = 30f` — the **defaults** match, but the serialized fields allow divergence via Inspector. If someone tweaks them, `RoundManagerHUD.UpdateHintTexts` (which reads `GameConstants`) and `LaunchController.HandleTouchEnded` (which reads the serialized fields) will disagree.

The comment on line 10 of `GameConstants.cs` acknowledges this: *"must match LaunchController serialized defaults"* — which is itself a code smell.

**Fix:** Remove the two `[SerializeField]` force fields from `LaunchController` and use `GameConstants` directly:
```csharp
float launchForce = Mathf.Lerp(GameConstants.MinLaunchForce, GameConstants.MaxLaunchForce, normalizedForce);
```
Also simplify `GameConstants` comment accordingly.

---

## Medium Priority Improvements

### M1 — `Rocket.cs` magic number `1.5f` in ground proximity check

`Rocket.cs` line 107:
```csharp
if (transform.position.y < GameConstants.GroundTop + 1.5f)
    GroundScorch.Spawn(transform.position, _maxHeight);
```
`1.5f` is unexplained. Should be a named constant or at minimum a comment explaining it is a "near-ground landing threshold."

**Fix:**
```csharp
private const float GroundProximityThreshold = 1.5f;
// ...
if (transform.position.y < GameConstants.GroundTop + GroundProximityThreshold)
```

---

### M2 — `GroundScorch.GetGroundY()` called every `FixedUpdate` per active debris

`rocket-debris-shatter-effect.cs` line 141:
```csharp
float groundY = GroundScorch.GetGroundY(transform.position.x) + _groundYOffset;
```
`GetGroundY` loops over all craters linearly. With 16 debris pieces + 6 obstacles (each potentially spawning more debris) × 50 Hz FixedUpdate, this is ~800 crater-lookups/second before craters even accumulate. Each miss can spawn up to ~36 pieces (16 rocket + 20 dirt). Round with 5 misses = 180 active debris × linear crater scan. Practically fine for a prototype with a small crater list, but worth noting.

**Improvement (low effort):** Cache `groundY` once when debris first falls below a threshold — it will not change after the debris reaches that level:
```csharp
if (_grounded) return;
// compute once when near ground, then reuse
```
Or simply compute `_groundY` once in `FixedUpdate` when `_velocity.y < 0 && transform.position.y < GameConstants.GroundTop + 2f`.

---

### M3 — `ObstacleSpawner.CalculateTrajectory` — `v` may not clear the trajectory

Lines 83-84:
```csharp
float vSquared = g * (dy + Mathf.Sqrt(dx * dx + dy * dy)) * 1.5f;
float v = Mathf.Sqrt(Mathf.Max(vSquared, 100f));
```
`Mathf.Max(vSquared, 100f)` clamps the minimum speed to `sqrt(100) = 10`, but `SafeLaunchForce` is then clamped to `GameConstants.MaxLaunchForce = 30`. If the computed `v` is, say, 45 (needed to clear the arc), clamping it to 30 means the auto-play shot **will not actually follow the computed safe trajectory** — it will fall short. The safe zone the spawner computed is invalid.

This is a correctness issue for auto-play: the rocket launched by `HandleAutoPlay` uses `_lastLaunchForce` which is clamped, but the trajectory used to clear obstacles was computed with unclamped `v`.

**Fix options:**
1. Clamp `v` before computing the trajectory so the trajectory matches what will actually be launched.
2. Scale `_safeRadius` by the ratio `clampedV / v` to widen the safe zone accordingly.
3. Accept that the trajectory is "best effort" and document the limitation.

---

### M4 — `RoundManager.HandleRestart` does `StopAllCoroutines` but `CameraController` coroutines are independent

`RoundManager.cs` line 130:
```csharp
StopAllCoroutines();
```
This stops coroutines on `RoundManager`, but `CameraController` has its own `_activeCoroutine`. Then immediately:
```csharp
_cameraController.PlayIntro();
```
`PlayIntro()` calls `StopActiveCoroutine()` first, so this is fine. However, the intent to "cancel everything" only half-succeeds: if audio is mid-`PlayWin()`, `PlayOneShot` cannot be interrupted. Not a bug, but the code creates an expectation of a full reset that isn't fully delivered. Consider a comment clarifying what `StopAllCoroutines` does and does not cover.

---

### M5 — `RoundManager` and `LaunchController` are bidirectionally coupled via `SerializeField`

`LaunchController` holds `_roundManager` ref; `RoundManager` holds `_launchController` ref. This is already acknowledged in past reviews and the editor tool handles the cross-wiring. For this game's scope it's acceptable, but it prevents either class from being used independently. No action required unless the game grows; just noting for the record.

---

### M6 — `AudioManager.PlayHitTarget` ignores `_groundHitClip` fallback

`AudioManager.cs` line 62-65:
```csharp
public void PlayHitTarget()
{
    if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip);
}
```
`PlayHitGround` falls back to `_groundHitClip` if `_boomClip` is null, but `PlayHitTarget` does not. If `rocket-boom.mp3` is missing, target hits are silent. Likely intentional (same boom for both), but the asymmetry is surprising.

---

## Low Priority Suggestions

### L1 — `CameraController` does not cache `Camera.main` in `Awake`, uses `GetComponent<Camera>()` instead

`CameraController.cs` line 64:
```csharp
_camera = GetComponent<Camera>();
```
This is correct and efficient — it's using `GetComponent` on `this`, not `Camera.main`. Actually fine. No change needed. (Previous review flagged `Camera.main` as expensive; this is already resolved here.)

---

### L2 — `SetupLayer` uses a raw integer index with no guard

`rocket-launcher-scene-auto-setup-editor-tool.cs` line 65:
```csharp
SetupLayer(8, "Rocket");
```
```csharp
private static void SetupLayer(int index, string layerName)
{
    var tm = GetTagManager();
    tm.FindProperty("layers").GetArrayElementAtIndex(index).stringValue = layerName;
    tm.ApplyModifiedProperties();
}
```
No bounds check, no check whether another layer already occupies index 8. Silent data corruption if Unity ever assigns index 8 to something else. Low risk in practice but worth a guard:
```csharp
var layersProp = tm.FindProperty("layers");
if (index >= layersProp.arraySize) { Debug.LogError(...); return; }
var existing = layersProp.GetArrayElementAtIndex(index).stringValue;
if (!string.IsNullOrEmpty(existing) && existing != layerName)
    Debug.LogWarning($"[SceneSetupTool] Layer {index} already '{existing}', overwriting with '{layerName}'");
```

---

### L3 — `ProceduralAudioClipGenerator.CreateStretch` phase accumulation is incorrect

`ProceduralAudioClipGenerator.cs` line 88:
```csharp
data[i] = Mathf.Sin(2f * Mathf.PI * freq * t * duration) * envelope * 0.3f;
```
`freq` changes each sample (it is `Mathf.Lerp(200f, 500f, t)`), but `t` is used as both the normalized time AND as the phase argument. Phase should be the integral of frequency, not `freq * t`. For a 0.15 s clip at these low frequencies, the artifact is inaudible — but technically the waveform is not a proper frequency sweep. Same pattern in `CreateClick`. No action needed for a game jam, just noting.

---

### L4 — `GroundScorch.PrepareGround` uses `GameObject.FindWithTag` at runtime

`ground-scorch-mark.cs` line 64:
```csharp
var ground = GameObject.FindWithTag(GameConstants.TagGround);
```
Called once per session (guarded by `_groundPrepared`), so the perf cost is negligible. Fine as-is.

---

### L5 — `RoundManager.ReloadAfterAutoPlay` duplicates reset logic from `HandleRestart`

Both methods do:
- `_rocket.gameObject.SetActive(true)`
- `_rocket.ResetToPosition(_spawnPoint.position)`
- `_targetTransform.gameObject.SetActive(true)`
- `_missCount = 0`
- `_launchController.EnableInput()`

Consider extracting a private `ResetPlayState()` helper. Small duplication, low urgency.

---

### L6 — `RocketDebris._allDebris` list grows stale entries until `ClearAll()`

`OnDestroy` removes entries from `_allDebris`, which is correct. But if a debris GO is destroyed by something other than `ClearAll()` or self-destruct (e.g., a future scene reload mid-game), the list could accumulate `null` entries that are only swept in `ClearAll()`. `ResetStaticState` only calls `_allDebris.Clear()` — it doesn't `Destroy` remaining objects first. This is safe because `SubsystemRegistration` fires before scene load, so GOs are already gone. But a comment explaining this assumption would prevent future confusion.

---

### L7 — `ExplosionEffect` has no material assigned to its `ParticleSystemRenderer`

`explosion-burst-particle-effect.cs` — `ConfigureParticleSystem` sets burst, colors, and shape but never assigns a material to the `ParticleSystemRenderer`. Unity will use the default particle material, which may appear differently across platforms. `RocketTrail` correctly calls `GetParticleMaterial()`. `ExplosionEffect` should do the same or at least add a comment that the default is intentional.

---

## Positive Observations

- `GameRoundTracker` as a plain C# class is a textbook separation of data from MonoBehaviour lifecycle — excellent.
- `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` on every singleton and static cache is correct and shows domain knowledge of Unity's initialization order.
- `PanCoroutine` DRY extraction (R3 fix) landed cleanly — `IntroCoroutine` and `PanToTargetCoroutine` both delegate to it correctly.
- `MoveRotation` in `Rocket.RotateToVelocity` is the right call for physics-driven rotation — avoids transform/rigidbody conflicts.
- Force clamping in `ObstacleSpawner.CalculateTrajectory` now references `GameConstants` — SSOT principle respected (caveat from M3 aside).
- Editor partial-class split is well-organized and the shared helper layer (`CreateEmpty`, `CreateSprite`, `Hex`) eliminates significant repetition.
- Event unsubscription in every `OnDestroy` is consistent and correct throughout.
- `TryComputeDrag` returning bool with out params is clean and avoids intermediate state.
- Coroutine race prevention via `_activeCoroutine` tracking in `CameraController` is robust.
- `GroundScorch` crater hole with `SpriteMask` + dark interior is a creative, zero-dependency visual trick.

---

## Recommended Actions

1. **(High — Bug)** Fix `go.layer = 6` in `CreateRocket()` → use `LayerMask.NameToLayer("Rocket")` or `RocketLayerIndex = 8`. This is a regression against the stated R4 fix.
2. **(High — Correctness)** Remove `_minLaunchForce`/`_maxLaunchForce` `[SerializeField]` from `LaunchController`, use `GameConstants` directly. Eliminates the divergence risk the comment in `GameConstants` warns about.
3. **(Medium)** Address `ObstacleSpawner.CalculateTrajectory` force clamping mismatch (M3) — clamp `v` *before* computing trajectory so the safe zone is valid for the actual launched force.
4. **(Medium)** Extract `ReloadAfterAutoPlay` + `HandleRestart` shared reset into `ResetPlayState()`.
5. **(Low)** Add material assignment in `ExplosionEffect.ConfigureParticleSystem` for cross-platform consistency.
6. **(Low)** Add guard + warning to `SetupLayer` for occupied indices.
7. **(Low)** Name the `1.5f` magic constant in `Rocket.cs` ground proximity check.

---

## Metrics

- Files: 19
- Total lines: ~2,903
- Files over 200 lines: 3 (`RoundManager` 262, `CameraController` 268, `SceneSetupEditor` 365 — editor tool acceptable given partial-class split)
- Critical issues: 0
- High: 2 (H1 regression bug, H2 SSOT drift)
- Medium: 6
- Low: 7
- Test coverage: N/A (Unity runtime game, no automated test suite)

---

## Unresolved Questions

1. H1 is listed as a R4 fix ("layer changed from 6 to 8") but `CreateRocket()` still reads `go.layer = 6`. Confirm whether this was intentionally deferred or is a missed change.
2. M3: Is the auto-play trajectory correctness (clamped force vs. computed arc) acceptable as "approximate demo" or should it guarantee the rocket follows the cleared path?
3. L7: Is the default Unity particle material intentional for `ExplosionEffect`, or was the `GetParticleMaterial()` call meant to be added (matching `RocketTrail`)?
