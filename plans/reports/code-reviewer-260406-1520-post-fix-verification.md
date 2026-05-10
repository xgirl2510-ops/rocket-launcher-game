# Code Review: Post-Fix Verification (Opus Round 2)

**Commit:** `d30e532` — fix: audio pitch leak, GC alloc, null guards, OnValidate, Undo support
**Base:** `f1302df`
**Date:** 2026-04-06
**Files changed:** 9 | +49 / -19

---

## Change-by-Change Verdict

### 1. AudioManager — pitch reset before PlayOneShot (lines 75-77)

```csharp
public void PlayStretch() { _oneShotSource.pitch = 1.0f; if (_stretchClip != null) _oneShotSource.PlayOneShot(_stretchClip); }
```

**[PASS]** Correct fix. `PlayHitTarget()` sets pitch to 1.3f; without reset, subsequent PlayOneShot calls on same AudioSource inherit that pitch. All six Play* methods now explicitly set pitch before playing.

No issues.

---

### 2. RoundManagerHUD — TMP SetText to avoid GC alloc (lines 107-108)

```csharp
_angleText.SetText("Angle: {0:F1}\u00b0", angle);
_forceText.SetText("Force: {0:F1}", force);
```

**[PASS]** Good catch. `SetText(string, float)` uses TMP's internal formatting buffer, avoiding per-frame string allocation from `$"..."` interpolation. Called during drag (every frame), so GC reduction matters.

No issues.

---

### 3. CameraController.PanCoroutine — unscaledDeltaTime (line 233)

```csharp
elapsed += Time.unscaledDeltaTime;
```

**[IMPORTANT]** Partially correct but **inconsistent**.

- `PanCoroutine` now uses `unscaledDeltaTime` — good for ensuring camera transitions work even if `timeScale` is changed.
- But `CameraScreenShake.Update()` (line 31) still uses `Time.deltaTime`.
- `FollowRocket()` (line 139) uses `Time.deltaTime` for zoom speed.
- `WaitForSeconds` in `IntroCoroutine` / `PanToTargetCoroutine` is affected by `timeScale` (would need `WaitForSecondsRealtime`).
- Currently the codebase **never modifies `Time.timeScale`**, so this change is harmless but **YAGNI** — introduces inconsistency for a scenario that doesn't exist.

**Verdict:** Not a bug, but a mixed time model. If `timeScale` is never changed, both `deltaTime` and `unscaledDeltaTime` are identical. If someone later adds slow-mo, the camera will half-work (pans unscaled, shake scaled, waits scaled).

**Recommendation:** Either revert to `deltaTime` (KISS — no timeScale usage exists) or commit fully (convert shake + WaitForSeconds too). Current state is the worst of both worlds.

---

### 4. RoundManager.Start — null guards + null-conditional (lines 59-71)

```csharp
if (_launchController == null) Debug.LogError("[RoundManager] _launchController is null.", this);
if (_spawnPoint == null) Debug.LogError("[RoundManager] _spawnPoint is null.", this);
...
_launchController?.EnableInput();
```

**[IMPORTANT]** The `?.` fix in Start() is correct — prevents NRE when `_cameraController` is null and `_launchController` is also null.

**However:** 8 other call sites in `RoundManager` and its partial class use `_launchController.` **without** null-conditional:

| File | Line | Call |
|------|------|------|
| `RoundManager.cs` | 138 | `_launchController.EnableInput()` |
| `round-manager-auto-play-restart-and-target.cs` | 46 | `_launchController.EnableInput()` |
| `round-manager-auto-play-restart-and-target.cs` | 70 | `_launchController.RotateRocketToDirection(dir)` |
| `round-manager-auto-play-restart-and-target.cs` | 77 | `_launchController.DisableInput()` |
| `round-manager-auto-play-restart-and-target.cs` | 86 | `_launchController.DisableInput()` |
| `round-manager-auto-play-restart-and-target.cs` | 95 | `_launchController.EnableInput()` |
| `round-manager-auto-play-restart-and-target.cs` | 101 | `_launchController.EnableInput()` |
| `round-manager-auto-play-restart-and-target.cs` | 130 | `_launchController.EnableInput()` |

The Start() LogError + `?.` creates a "warn and proceed" pattern, but the game will NRE on the first user interaction at `ReloadRocket()` line 138 anyway. Either:
- (a) Fail fast: keep LogError, remove `?.`, let it crash visibly at Start → developer fixes wiring immediately
- (b) Fail safe: add `?.` to ALL call sites

Current state: **inconsistent** — worst option. Partial null-safety means the crash is deferred to a harder-to-diagnose moment (after first miss, not at Start).

---

### 5. LaunchController.RotateRocketToDirection — null guard (line 147)

```csharp
if (_rocket == null) return;
```

**[IMPORTANT]** Same inconsistency. `HandleTouchEnded()` line 98 still has:

```csharp
_rocket.transform.rotation = Quaternion.identity;  // NRE if _rocket is null
```

And line 110:
```csharp
_rocket.Launch(launchDirection, launchForce);  // NRE if _rocket is null
```

If `_rocket` can be null at `RotateRocketToDirection`, it can be null at these sites too. Adding a guard to one public method but not the private caller that touches `_rocket` in the same path is incomplete.

**Recommendation:** Since `_rocket` is a required reference (game can't work without it), the OnValidate warning + Awake/Start LogError pattern is sufficient. The null guard in `RotateRocketToDirection` is defensive-but-incomplete. Either add `if (_rocket == null) return;` at the top of `HandleTouchMoved()` / `HandleTouchEnded()` too, or remove it from `RotateRocketToDirection` (KISS: crash early, fix wiring).

---

### 6. LaunchController.HandleTouchEnded — roundManager null guard (lines 104-108)

```csharp
if (_roundManager != null)
{
    _roundManager.OnShotFired();
    RoundManagerHUD.Instance?.UpdateStatsUI(_roundManager.RoundTracker);
}
```

**[PASS]** Correct. Groups the two dependent calls under a single null check. Removes the `?.` from `_roundManager?.OnShotFired()` — if roundManager is null, stats update is also pointless. Clean.

---

### 7. ExplosionEffect — SerializeField → const (lines 14-17)

```csharp
private const int BurstCount = 30;
private const float ParticleLifetime = 0.6f;
private const float StartSpeed = 8f;
private const float StartSize = 0.3f;
```

**[PASS]** These are never tweaked at runtime and the class is instantiated via `Spawn()` static factory (never placed in scene). Removing `[SerializeField]` is correct — eliminates inspector noise and prevents accidental per-instance overrides.

No issues.

---

### 8. Editor: Undo support for TagManager (lines 70-72)

```csharp
var tagManagerAsset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
if (tagManagerAsset != null)
    Undo.RegisterCompleteObjectUndo(tagManagerAsset, "Rocket Launcher Scene Setup");
```

**[PASS]** Good practice. Without this, tag/layer changes via `SetupTags()`, `SetupSortingLayers()`, `SetupLayer()` were not undoable. `RegisterCompleteObjectUndo` before modifications is correct API usage.

Note: The dialog still says "This cannot be undone" but now it partially can. Minor doc inconsistency, not a code issue.

---

### 9. Editor: Layer hardcode `go.layer = 8` (line 109)

```csharp
go.layer = 8; // Rocket layer — must match GameConstants/Physics2D matrix
```

**[MODERATE]** The comment explains the magic number. Previous code used `LayerMask.NameToLayer("Rocket")` which would return -1 if the layer wasn't set up yet (editor tool calls `SetupLayer(8, "Rocket")` later in `RunCoreSetup`, after `CreateRocket`). Hardcoding 8 avoids this timing issue.

However: `GameConstants` already has `DefaultLayer = 0`. A `RocketLayer = 8` constant there would be cleaner. The comment "must match GameConstants" is ironic when GameConstants doesn't define it.

**Recommendation:** Add `public const int RocketLayer = 8;` to GameConstants, use it in both editor and runtime code.

---

### 10. OnValidate additions (RoundManagerHUD, ObstacleSpawner)

**[PASS]** Both follow the established pattern:
- `#if UNITY_EDITOR` guard
- `if (!gameObject.scene.isLoaded) return;` — prevents false warnings during prefab edit / AddComponent
- `Debug.LogWarning` with component name prefix

Consistent with existing OnValidate in RoundManager, LaunchController, CameraController.

---

### 11. ObstacleSpawner layer comment (line 170-171)

```csharp
// Obstacles use Default layer (0). Physics2D matrix must allow Default<->Rocket (layer 8) collision.
go.layer = GameConstants.DefaultLayer;
```

**[PASS]** Helpful documentation comment. Using `GameConstants.DefaultLayer` is correct and consistent.

---

## Summary

| Severity | Count | Description |
|----------|-------|-------------|
| CRITICAL | 0 | No crashes or security issues introduced |
| IMPORTANT | 3 | Inconsistent null guards (2), mixed time model (1) |
| MODERATE | 1 | Magic number 8 without const |
| PASS | 7 | Clean, correct fixes |

### Overall: 7/10 — Fixes are directionally correct but 3 are incomplete.

The audio pitch fix and GC allocation fix are solid. The null guard and unscaledDeltaTime changes introduce inconsistency — they fix the symptom at one call site but leave identical symptoms at others.

---

## Recommended Actions (Priority Order)

1. **Null guard consistency for `_launchController`**: Either add `?.` to all 8 remaining call sites in RoundManager, or adopt fail-fast (remove `?.` from Start, let the LogError + NRE surface the wiring bug immediately). I recommend fail-fast since `_launchController` is mandatory.

2. **Null guard consistency for `_rocket` in LaunchController**: Add `if (_rocket == null) return;` to top of `HandleTouchEnded()` (line 92), or remove from `RotateRocketToDirection` and rely on OnValidate/LogError.

3. **Time model**: Revert `unscaledDeltaTime` to `deltaTime` in PanCoroutine. No code in the project modifies `timeScale`. If slow-mo is added later, all timing code should be audited holistically (YAGNI).

4. **Add `RocketLayer = 8` to GameConstants**: Replace magic number in editor code.

---

## Unresolved Questions

1. Was the `unscaledDeltaTime` change motivated by a specific bug (e.g., camera pan freezing during pause)? If so, the fix needs to extend to `WaitForSeconds` calls too.
2. Is `_launchController` truly optional? The LogError suggests it's required, but `?.` suggests it's optional. These signals conflict.
