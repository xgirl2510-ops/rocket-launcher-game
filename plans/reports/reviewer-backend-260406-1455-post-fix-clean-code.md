# Code Review — Backend/Core Scripts
**Post-fix Clean Code, SOLID, Unity Best Practices**
Date: 2026-04-06 | Reviewer: code-reviewer agent

---

## Code Review Summary

### Scope
- Files reviewed: 12 (RoundManager.cs, round-manager-auto-play-restart-and-target.cs, round-manager-hud.cs, GameRoundTracker.cs, GameConstants.cs, runtime-sprite-factory.cs, Rocket.cs, LaunchController.cs, AimArrow.cs, AudioManager.cs, ProceduralAudioClipGenerator.cs, ObstacleSpawner.cs)
- Lines of code: ~900
- Review focus: Post-fix clean code, SOLID, Unity best practices

### Overall Assessment
Codebase is in strong shape. Lifecycle, event symmetry, singleton patterns, and partial-class split are all correct. Three issues stand out: a GC allocation in the Update path, a latent audio pitch bug, and missing `OnValidate` guards on two components.

---

## High Priority Findings

### [HIGH] GC allocation every frame during drag — `round-manager-hud.cs:107-108`
`UpdateHintTexts()` uses string interpolation (`$"Angle: {angle:F1}°"`, `$"Force: {force:F1}"`) and is called from `LaunchController.HandleTouchMoved()` which runs every frame while dragging. Two string allocations per frame during aiming.

**Fix:** Use `TextMeshProUGUI.SetText(string, float)` overload or format into a `System.Text.StringBuilder`. Alternatively cache a format string and use `string.Format` only when value changes detectably.
```cs
// Option A: TMP built-in formatter (no alloc)
_angleText.SetText("Angle: {0:F1}°", angle);
_forceText.SetText("Force: {0:F1}", force);
```

### [HIGH] Audio pitch not reset before PlayWin/PlayClick/PlayStretch — `AudioManager.cs:70-77`
`PlayHitTarget()` sets `_oneShotSource.pitch = 1.3f`. `PlayWin()`, `PlayClick()`, and `PlayStretch()` do **not** reset pitch before playing. Sequence in `HandleTargetHit()`:
1. `PlayHitTarget()` → pitch 1.3f
2. `PlayWin()` → plays win jingle at 1.3f (wrong)

After restart `PlayClick()` and `PlayStretch()` also play at 1.3f until overwritten.

**Fix:** Reset pitch in every `Play*` method that omits it, or centralise pitch in a single `PlayOneShot(AudioClip, float pitch = 1f)` helper:
```cs
private void Play(AudioClip clip, float pitch = 1f)
{
    if (clip == null) return;
    _oneShotSource.pitch = pitch;
    _oneShotSource.PlayOneShot(clip);
}
public void PlayWin()    => Play(_winClip);
public void PlayStretch()=> Play(_stretchClip);
public void PlayClick()  => Play(_clickClip);
public void PlayHitTarget() => Play(_boomClip != null ? _boomClip : _targetHitClip, 1.3f);
```

---

## Medium Priority Improvements

### [MEDIUM] Missing null guard on `_launchController` in `RoundManager.Start()` — `RoundManager.cs:68`
```cs
else
    _launchController.EnableInput();  // NPE if _launchController is null
```
The `else` branch assumes `_launchController` is non-null if `_cameraController` is null. Same unguarded pattern appears in `ReloadRocket()` (line 135) and `ReloadAfterAutoPlay()` (line 130). OnValidate warns at edit-time but there's no runtime guard.

**Fix:** Add null-conditional:
```cs
else
    _launchController?.EnableInput();
```
Apply same pattern in `ReloadRocket()` and `ReloadAfterAutoPlay()`.

### [MEDIUM] Missing null guard on `_spawnPoint` in `ReloadRocket()` — `RoundManager.cs:131`
```cs
_rocket.ResetToPosition(_spawnPoint.position);  // NPE if _spawnPoint null
```
`_spawnPoint` is warned in OnValidate but not guarded at runtime.

**Fix:**
```cs
if (_spawnPoint != null)
    _rocket.ResetToPosition(_spawnPoint.position);
```

### [MEDIUM] `RotateRocketToDirection()` and `HandleTouchEnded()` unguarded `_rocket` access — `LaunchController.cs:98,143-145`
`RotateRocketToDirection()` is public but calls `_rocket.transform` with no null check. `HandleTouchEnded()` similarly accesses `_rocket.transform.rotation` at line 98 before the null-checked launch path.

**Fix:** Add null guard:
```cs
public void RotateRocketToDirection(Vector2 direction)
{
    if (_rocket == null) return;
    ...
}
```
And in `HandleTouchEnded`:
```cs
if (_rocket != null) _rocket.transform.rotation = Quaternion.identity;
```

### [MEDIUM] No `OnValidate` in `RoundManagerHUD` — `round-manager-hud.cs`
`_roundManager` is a required reference but missing editor-time null warning, inconsistent with `RoundManager` and `LaunchController` patterns.

**Fix:**
```cs
#if UNITY_EDITOR
private void OnValidate()
{
    if (!gameObject.scene.isLoaded) return;
    if (_roundManager == null) Debug.LogWarning("[RoundManagerHUD] _roundManager not assigned.", this);
}
#endif
```

### [MEDIUM] No `OnValidate` in `ObstacleSpawner` — `ObstacleSpawner.cs`
`_spawnPoint` and `_targetTransform` are required but no editor-time warning. Inconsistent with established pattern.

### [MEDIUM] Obstacle layer assignment has invisible collision matrix dependency — `ObstacleSpawner.cs:170`
`go.layer = GameConstants.DefaultLayer` (Layer 0). Rocket is on Layer 8. Whether collisions register depends on the Physics2D collision matrix in Project Settings. No code comment documents this dependency.

**Fix:** Add comment or assert the layer relationship. Consider using a named constant like `ObstacleLayer` and documenting expected collision matrix.

### [MEDIUM] Redundant null check in `LaunchController.HandleTouchEnded()` — `LaunchController.cs:104-105`
```cs
_roundManager?.OnShotFired();
if (_roundManager != null) RoundManagerHUD.Instance?.UpdateStatsUI(_roundManager.RoundTracker);
```
Second check is redundant after null-conditional first call.

**Fix:**
```cs
if (_roundManager != null)
{
    _roundManager.OnShotFired();
    RoundManagerHUD.Instance?.UpdateStatsUI(_roundManager.RoundTracker);
}
```

---

## Low Priority Suggestions

### [LOW] `WaitForSeconds` allocation per `DelayedAction` call — `RoundManager.cs:157`
`new WaitForSeconds(delay)` allocates on every rocket land/hit. Not a hot path but easily cached.

**Fix:** Cache `WaitForSeconds` for the fixed `_reloadDelay`:
```cs
private WaitForSeconds _reloadWait;
// in Awake: _reloadWait = new WaitForSeconds(_reloadDelay);
// in DelayedAction: yield return _reloadWait;
```

### [LOW] `ProceduralAudioClipGenerator.CreateTargetHit()` — magic duration — `ProceduralAudioClipGenerator.cs:99`
`SampleRate / 4` (= 0.25s) is not self-documenting. Other methods use an explicit `float duration`.

**Fix:** `const float duration = 0.25f;` and `int samples = (int)(SampleRate * duration);` for consistency.

### [LOW] `HandleAutoPlay()` partial comment says "demo" — `round-manager-auto-play-restart-and-target.cs:50`
XML summary says "reset for player" but doesn't mention that the target stays the same post auto-play. Minor doc gap.

---

## Positive Observations
- Partial class split (RoundManager/auto-play file) cleanly respects SRP without over-engineering
- Event subscribe/unsubscribe symmetry is correct across all files (Start↔OnDestroy, OnEnable/OnDisable not needed)
- `RuntimeSpriteFactory` static state reset via `RuntimeInitializeOnLoadMethod` is correct for domain reloads
- `GameRoundTracker` as a plain C# class (not MonoBehaviour) is the right design
- `ObstacleSpawner.CalculateTrajectory()` velocity clamping before angle solve is mathematically correct
- Coroutine safety: `StopAllCoroutines()` in `OnDestroy()` and on `HandleRestart()` is correct
- `Camera.main` cached in `LaunchController.Awake()` — no per-frame Camera.main access
- No `GetComponent` calls in Update/FixedUpdate — all cached in Awake
- `DisallowMultipleComponent` on both singletons

---

## Recommended Actions
1. **Fix audio pitch bug** — add `_oneShotSource.pitch = 1f` to `PlayWin`, `PlayClick`, `PlayStretch` (or use helper)
2. **Fix GC allocation** — replace string interpolation in `UpdateHintTexts` with TMP `SetText` format overload
3. **Add null guards** — `_launchController?.EnableInput()` in Start/ReloadRocket/ReloadAfterAutoPlay; `_spawnPoint` guard in ReloadRocket
4. **Add `OnValidate`** to `RoundManagerHUD` and `ObstacleSpawner`
5. **Fix redundant null check** in `LaunchController.HandleTouchEnded()`
6. **Document obstacle layer dependency** in `ObstacleSpawner.CreateObstacle()`
7. **Cache `WaitForSeconds`** in `RoundManager` (low priority)

---

### Metrics
- Linting Issues: 0 (no syntax errors)
- Critical Issues: 0
- High: 2
- Medium: 6
- Low: 3

### Unresolved Questions
- None.
