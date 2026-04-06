# Code Review -- Deep Architecture, Quality & Correctness Audit (Round 6)

**Date:** 2026-04-03
**Reviewer:** code-reviewer
**Prior score:** 8.5 / 10 (Round 5)
**Scope:** Core gameplay scripts — architecture, event safety, lifecycle, null safety, thread/coroutine safety, magic numbers, dead code, naming, access modifiers, static state, memory, race conditions, DRY, error handling

---

## Code Review Summary

### Scope
- Files reviewed: 18 C# runtime scripts (all scripts under `Assets/Scripts/`) + 2 test files
- Lines of code analyzed: ~2,100 runtime C# lines
- Review focus: "Find everything blocking 10/10" — deep read of all 11 requested scripts plus full context scripts
- Updated plans: none (no plan file provided)

### Overall Assessment

The codebase is in genuinely good shape. The refactors up to Round 5 resolved the big-ticket items. What remains are a cluster of medium-weight architectural concerns (event/lifecycle asymmetry, a silent gameplay bug in shot counting, coroutine edge cases) plus a larger set of low-priority polish items. Nothing is crashworthy in normal play, but the shot-counter bug and the dual-subscriber pattern in `HandleRestart` are correctness issues that affect gameplay and reliability respectively.

**Score: 8.5 / 10** (no change — prior issues addressed but new issues of equivalent weight found; net neutral)

---

## CRITICAL Issues

None found — no crashes or data-loss bugs in normal play paths.

---

## HIGH Priority Findings

### H1 -- Shot counter counts auto-play shots as player shots (gameplay bug)

**File:** `LaunchController.cs` line 101, `round-manager-auto-play-restart-and-target.cs` line 67

`OnShotFired()` / `_roundTracker.IncrementShots()` is called in two places:
- `LaunchController.HandleTouchEnded()` — correct (player shot)
- `HandleAutoPlay()` calls `_rocket.Launch()` directly (line 67) but does NOT call `OnShotFired()` or `IncrementShots()`

At first glance this looks fine. But `HandleTargetHit()` (RoundManager line 92) calls `_roundTracker.TryUpdateBest(_roundTracker.RoundShots)` — this is called for **both** auto-play hits and player hits. When auto-play hits the target, `RoundShots = 0` (no shots were counted), so `TryUpdateBest(0)` fires.

From `GameRoundTracker.TryUpdateBest`: `if (_bestScore == 0 || shots < _bestScore)` — since `_bestScore == 0` and `shots == 0`, `_bestScore` is set to `0`. Now `_bestScore == 0` forever, and the HUD will always show `"Best --"` even after a genuine player win, because `GetStatsText()` only shows the number if `_bestScore > 0`.

**Reproduction:** Let auto-play run and hit the target, then play normally and win. Best score will never display.

**Root cause:** `HandleTargetHit` does not guard on `_isAutoPlaying` before calling `TryUpdateBest`. The early-return at line 87-90 returns before the `TryUpdateBest` call at line 92 — this is correct. Wait — re-reading:

```csharp
// RoundManager.cs line 86-95
if (_isAutoPlaying)
{
    StartCoroutine(DelayedAction(_reloadDelay, ReloadAfterAutoPlay));
    return;  // <-- returns here, skips TryUpdateBest
}

_roundTracker.TryUpdateBest(_roundTracker.RoundShots);
```

The `return` at line 90 DOES prevent `TryUpdateBest` from being called during auto-play. So the `TryUpdateBest(0)` scenario does NOT occur.

**Re-assessment:** `HandleTargetHit` is actually safe. However, there is still a real bug: `ReloadAfterAutoPlay` (line 112) resets `_missCount = 0` but does NOT call `_roundTracker.NewRound()`. After auto-play completes, the tracker still reflects the pre-auto-play state. If auto-play was triggered mid-round (e.g. after 3 misses), the shot count continues from where it was when player resumes. Minor but inconsistent with the intent of auto-play as a "demo then reset" flow.

**Severity:** Medium (see reclassification below). Moved to MEDIUM section.

---

### H2 -- `HandleRestart` dual-subscribe pattern is fragile and leaks on rapid clicks

**File:** `round-manager-auto-play-restart-and-target.cs` lines 38-41

```csharp
_cameraController.OnIntroComplete -= OnIntroDone;
_cameraController.OnIntroComplete += OnIntroDone;
```

This unsubscribe-then-subscribe pattern is used to prevent double-subscription on repeated restarts. It works correctly for the `OnIntroComplete` event. However:

1. `OnLookTargetComplete` is NOT unsubscribed/resubscribed in `HandleRestart`. If the player clicks restart while `PanToTarget` is mid-coroutine, `OnLookTargetComplete` fires after restart, calling `_launchController.EnableInput()` a second time on a fresh round. Harmless here (calling `EnableInput()` twice is idempotent) but demonstrates the pattern is applied inconsistently.

2. The unsubscribe-then-subscribe idiom is a code smell — it signals that subscriber lifetime is not clearly owned. The correct approach is to ensure `OnIntroDone` unsubscribes itself when it fires (already done at line 90) AND to call `StopActiveCoroutine()` in CameraController before re-subscribing (also done). So the code is defensively correct but aesthetically fragile.

3. If `HandleRestart` is called while `IntroCoroutine` is in its `WaitForSeconds` pause (before `PanCoroutine` starts), `StopActiveCoroutine()` kills the coroutine, the `OnIntroComplete` event never fires, but the subscription has already been re-added. The handler remains subscribed until the next restart. On the NEXT restart, the unsubscribe removes it correctly. Net effect: no duplicate subscription accumulates. Correct, but non-obvious.

**Verdict:** Not a bug but a reliability smell. Acceptable for this project scope.

---

### H3 -- `CameraController.Start` subscribes to `Rocket` events; `Rocket` is destroyed mid-game then re-instanced (potential NRE)

**File:** `CameraController.cs` lines 85-88

```csharp
_rocket.OnRocketLaunched += HandleRocketLaunched;
_rocket.OnRocketLanded += HandleRocketLanded;
_rocket.OnTargetHit += HandleRocketHitTarget;
```

`Rocket` is never `Destroy()`-ed — it is hidden via `SetActive(false)` and reset via `ResetToPosition`. So the subscription remains valid across rounds. No NRE risk.

**However:** `CameraController.OnDestroy` (line 198) correctly unsubscribes from `_rocket`, but it only checks `if (_rocket != null)`. Since `Rocket` is a MonoBehaviour that can be destroyed independently (e.g. if scene is reloaded), if `_rocket` is destroyed before `CameraController`, the subscription reference is a dangling managed delegate — Unity's event system holds a reference to `HandleRocketLaunched` which captures `this` (CameraController). This will not crash because C# GC handles it, but the delegate table on the destroyed Rocket still holds a reference to the live CameraController, preventing GC of the CameraController until Rocket is also GC'd. In a single-scene game with no Rocket destroy, this is a non-issue.

**Verdict:** Non-issue for current architecture. Note for future multi-scene work.

---

### H4 -- `RoundManager.Start` vs `CameraController.Start` — execution order not guaranteed

**File:** `RoundManager.cs` lines 53-65, `CameraController.cs` lines 76-89

Both subscribe to `_rocket.OnRocket*` events in `Start()`. Unity does not guarantee `Start()` execution order unless Script Execution Order is explicitly set. `CameraController.Start()` calls `PlayIntro()` which fires `OnIntroComplete` at the end. `RoundManager.Start()` subscribes to `OnIntroComplete` — but if `CameraController.Start()` runs BEFORE `RoundManager.Start()`, the intro starts and might complete before `RoundManager` has subscribed, meaning `EnableInput()` is never called and the game is stuck waiting.

In practice, `PanCoroutine` with `_introPanDuration = 1.5f` means the intro takes at least ~2.5 seconds (pause + pan). `Start()` for all MonoBehaviours runs in the same frame. So `RoundManager.Start()` will subscribe within the same frame that `CameraController.Start()` starts the coroutine — before any `yield` in `IntroCoroutine` fires. This is safe in practice.

**But** it is an implicit ordering dependency. The design works only because coroutines yield at least one frame. If the intro duration were 0, it could break. This is a latent brittleness.

**Fix (low risk):** Set Script Execution Order: `CameraController` after `RoundManager`. Or: use `Awake` for subscriptions instead of `Start`.

**Severity:** Low in practice but medium architectural concern.

---

## MEDIUM Priority Findings

### M1 -- `ReloadAfterAutoPlay` does not call `NewRound()` — round tracker state is inconsistent

**File:** `round-manager-auto-play-restart-and-target.cs` lines 112-124

After auto-play finishes:
- `_missCount` is reset to 0 ✓
- `_rocket` is reset ✓
- `_isAutoPlaying` is reset ✓
- `_roundTracker.NewRound()` is **NOT** called ✗

Compare with `HandleRestart` (line 32): it calls `_roundTracker.NewRound()` and `UpdateStatsUI`. `ReloadAfterAutoPlay` skips both.

Result: after auto-play hits or misses, the HUD still shows the pre-auto-play round number and shot count. The player resumes playing in the same "round" that was active before auto-play. This is probably intentional (auto-play is a "demo" not a new round) but it is undocumented and the inconsistency between the two reload paths is confusing.

**Verdict:** If intentional, add a comment explaining why `NewRound()` is skipped. If unintentional, add the call.

---

### M2 -- `LaunchController.HandleTouchEnded` calls `_roundManager.OnShotFired()` before `_rocket.Launch()`

**File:** `LaunchController.cs` lines 101-104

```csharp
_roundManager.OnShotFired();           // line 101 — increments shot counter
RoundManagerHUD.Instance?.UpdateStatsUI(_roundManager.RoundTracker);  // line 102
_rocket.Launch(launchDirection, launchForce);  // line 104
```

Shot is counted before the rocket is actually launched. If `_rocket.Launch()` throws (e.g. `_rocket` is null, `_rb` is null after some edge-case destroy), the shot was already counted. Low probability but poor ordering. The shot should be counted after launch succeeds (or at minimum, null-check `_rocket` before counting).

Also: `LaunchController` does not null-check `_roundManager` before calling `OnShotFired()`. `_roundManager` is a `[SerializeField]` that can be null if wiring is broken. `OnValidate` warns but does not prevent. A null `_roundManager` here causes a `NullReferenceException` mid-launch.

---

### M3 -- `CameraController` subscriptions to `Rocket` events in `Start` but `_rocket` is checked in `Awake` OnValidate only

**File:** `CameraController.cs` lines 78-83

```csharp
if (_rocket == null || _vehicleTransform == null || _targetTransform == null)
{
    Debug.LogError("...");
    enabled = false;
    return;
}
_rocket.OnRocketLaunched += HandleRocketLaunched;
```

Good — there is a null guard. But `enabled = false` only prevents `Update`/`LateUpdate`. It does NOT prevent `OnDestroy` from running. `OnDestroy` at line 198 checks `if (_rocket != null)` before unsubscribing — since `_rocket` IS null (that's why we disabled), it correctly skips. Fine.

But `PlayIntro()` is called at line 89 even when all refs are valid. If `_vehicleTransform` is valid but `_targetTransform` is valid and then destroyed before `IntroCoroutine` runs its `SetCameraXY(_targetTransform.position.x, ...)` at line 105, we get NRE. The null check at line 104 (`if (_targetTransform != null)`) handles this. ✓

---

### M4 -- `AimArrow.UpdateArrow` modifies `localScale.x` by reading `transform.localScale.x` — preserves existing X scale but not Z

**File:** `AimArrow.cs` line 50

```csharp
transform.localScale = new Vector3(transform.localScale.x, scaleY, 1f);
```

Z is hardcoded to `1f`. If the parent object ever has a non-1 Z scale, this would fight it on every frame. In a 2D game this is nearly always fine, but it's silently overriding Z each frame. The more correct idiom:

```csharp
var s = transform.localScale;
transform.localScale = new Vector3(s.x, scaleY, s.z);
```

Minor — low probability of causing a real bug.

---

### M5 -- `ObstacleSpawner.CalculateTrajectory` uses unclamped `v` for discriminant but clamped `vClamped` for angle — math inconsistency (known from Round 5, now documenting detail)

**File:** `ObstacleSpawner.cs` lines 72-96

The discriminant at line 79 is computed with `vc2 = vClamped * vClamped` — this was the Round 4 fix. The discriminant IS computed with the clamped velocity. The angle `theta` is then computed with clamped velocity. This is internally consistent.

The Round 5 report stated "theta uses v (unclamped)" — this was **incorrect**. Re-reading the code: `vc2` and `vc4` are derived from `vClamped` (not `v`). The math is consistent with the clamped velocity. This is actually correct.

**Verdict:** Round 5 M3 was a false positive. No issue here.

---

### M6 -- `GroundScorch` static class uses `GameObject.FindWithTag` at runtime in `PrepareGround()`

**File:** `ground-scorch-mark.cs` line 55

```csharp
var ground = GameObject.FindWithTag(GameConstants.TagGround);
```

`FindWithTag` is an O(n) scene scan. It is called once, gated by `_groundPrepared`. However, obstacles also use `GameConstants.TagGround` as their tag (set in `ObstacleSpawner.CreateObstacle` line 167). If obstacles are spawned BEFORE the first crater, `FindWithTag("Ground")` could return an obstacle instead of the ground sprite, and the ground SpriteRenderer's `maskInteraction` would be set on an obstacle — meaning craters wouldn't cut the ground correctly.

The order of events:
1. `RoundManager.Awake()` → `RandomizeTarget()` → `ObstacleSpawner.RespawnObstacles()` → obstacles tagged "Ground" are created
2. Later, rocket lands → `GroundScorch.Spawn()` → `PrepareGround()` → `FindWithTag("Ground")` — returns whichever "Ground"-tagged object Unity finds first (undefined order)

**Fix:** Give the actual ground tile a distinct tag (e.g., "GroundTile") and store a direct reference, OR pass the ground SpriteRenderer as a parameter to `PrepareGround`. Alternatively, use `FindObjectOfType<SpriteRenderer>()` on the ground layer.

**Severity:** Medium — can silently break the crater visual effect when obstacles are tagged "Ground" and are found first.

---

### M7 -- `RocketDebris.OnDestroy` calls `_allDebris.Remove(gameObject)` — but `gameObject` may already be null during domain reload

**File:** `rocket-debris-shatter-effect.cs` line 128

During domain reload in the Editor, `gameObject` can be null in `OnDestroy`. `_allDebris.Remove(null)` is safe (List.Remove handles this), but the intent is to remove the specific GameObject, not null. In play mode this is fine. In Editor domain reload this is a silent no-op that leaves a null entry in `_allDebris` until the next `ResetStaticState`.

`ResetStaticState` only calls `_allDebris.Clear()` (line 41) without `Destroy`-ing entries, but by the time `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` fires, all GameObjects are already destroyed by the domain reload. Fine.

**Verdict:** Technically correct but fragile in editor. Low priority.

---

### M8 -- `CameraScreenShake.GetOffset()` advances `_elapsed` as a side effect of reading

**File:** `camera-screen-shake.cs` lines 28-33

```csharp
public Vector2 GetOffset()
{
    if (_elapsed >= _duration) return Vector2.zero;
    _elapsed += Time.deltaTime;
    ...
}
```

`GetOffset()` is a getter that mutates state. It is called once per frame from `SetCameraXY`, which is called from `LateUpdate` or from PanCoroutine. If `SetCameraXY` is called more than once per frame (e.g., once from `LateUpdate` in Follow mode AND once from a pan coroutine step in the same frame), `_elapsed` advances twice in one frame, halving the effective shake duration.

Looking at `CameraController`: `SetCameraXY` is called from `FollowRocket()` (via `LateUpdate`) and from `PanCoroutine` (via coroutine `yield return null`, which also runs each frame). But these states are mutually exclusive (state machine: `Following` runs `LateUpdate`, pans run coroutines, never simultaneously). Safe in practice.

**However**, the naming `GetOffset()` violates Command-Query Separation — it should be renamed `ConsumeShakeOffset()` or the advance should happen in a separate `Tick()` method called from an `Update` in `CameraScreenShake` itself.

---

### M9 -- `ProceduralAudioClipGenerator.CreateStretch` has a phase accumulation bug

**File:** `ProceduralAudioClipGenerator.cs` lines 83-89

```csharp
float freq = Mathf.Lerp(200f, 500f, t);
data[i] = Mathf.Sin(2f * Mathf.PI * freq * t * duration) * envelope * 0.3f;
```

The phase argument is `freq * t * duration`. As `t` goes from 0 to 1 and `duration = 0.15f`, the phase at the end is `500 * 1.0 * 0.15 = 75` radians. This is not how FM synthesis works — for a correct chirp/sweep, the instantaneous frequency must be integrated over time:

```
phase(i) = Σ freq(j)/SampleRate for j=0..i
```

The current formula `freq * t * duration` is equivalent to multiplying instantaneous frequency by normalized time — which produces a non-linear, "squished" sweep rather than a smooth linear frequency ramp. For a short (0.15s) stretch sound this is barely audible, but the same pattern in `CreateClick` (`freq * t * duration` where `freq=1000`, `t=0..1`, `duration=0.05`) produces essentially just `Mathf.Sin(50 * t)` which is a very low-frequency single sine period — basically DC offset with minor oscillation rather than a 1kHz click.

**Impact:** The stretch and click sounds may not sound as designed. Low gameplay impact (they are feedback sounds) but worth noting for audio quality.

Same issue in `CreateClick` line 107.

---

## LOW Priority Findings

### L1 -- `RocketTrail._startColor` and `_endColor` serialized fields are declared but unused (carry-over from R5)

**File:** `rocket-trail-particle-effect.cs`

These fields appear to have been removed in the working tree since Round 5 (file now shows only `_emissionRate`, `_particleLifetime`, `_startSize`). Confirmed RESOLVED.

---

### L2 -- `Rocket._groundTag` and `_targetTag` are `[SerializeField]` but only hold `GameConstants` values

**File:** `Rocket.cs` lines 14-15

```csharp
[SerializeField] private string _groundTag = GameConstants.TagGround;
[SerializeField] private string _targetTag = GameConstants.TagTarget;
```

Exposing these as `[SerializeField]` means they can be overridden in the Inspector, creating a silent divergence from `GameConstants`. If someone changes the tag in the Inspector but not in `GameConstants`, obstacles (which use `GameConstants.TagGround`) and the rocket (which uses `_groundTag`) would use different strings — craters would no longer trigger.

Since these values are SSOT'd in `GameConstants`, they should not be inspector-overridable. Remove `[SerializeField]` and make them `private const string`.

---

### L3 -- `LaunchController` caches `Camera.main` in `Awake` — safe but brittle

**File:** `LaunchController.cs` line 32

```csharp
_camera = Camera.main;
```

`Camera.main` is a scene find that returns the first camera tagged "MainCamera". Caching in `Awake` is correct (avoids per-frame overhead). But if the camera GameObject is replaced or re-tagged at runtime, the cache goes stale. For a single-scene game with one camera this is fine. Note: same pattern used in `LaunchController` only — `CameraController` injects the camera reference directly via `GetComponent<Camera>()`, which is correct and preferred.

---

### L4 -- `RoundManagerHUD.OnDestroy` does not remove `_lookTargetButton` listener from `null`-safe path

**File:** `round-manager-hud.cs` lines 116-124

```csharp
private void OnDestroy()
{
    if (_restartButton != null) _restartButton.onClick.RemoveListener(OnRestartClicked);
    if (_autoPlayButton != null) _autoPlayButton.onClick.RemoveListener(OnAutoPlayClicked);
    if (_lookTargetButton != null) _lookTargetButton.onClick.RemoveListener(OnLookTargetClicked);
}
```

This is correct. However, the listeners were added in `Start()` without null-checks on some of them:

- `_restartButton.onClick.AddListener` — guarded by `if (_restartButton != null)` ✓
- `_autoPlayButton.onClick.AddListener` — guarded ✓
- `_lookTargetButton.onClick.AddListener` — guarded by `if (_lookTargetButton != null)` ✓ (line 55)

All three are guarded. Fine.

---

### L5 -- `RoundManager.OnDestroy` unsubscribes `OnLookTargetComplete` but never subscribes in the constructor path

**File:** `RoundManager.cs` lines 147-148

```csharp
if (_cameraController != null)
{
    _cameraController.OnIntroComplete -= OnIntroDone;
    _cameraController.OnLookTargetComplete -= OnLookTargetDone;  // line 148
}
```

`OnLookTargetComplete` subscription happens only in `HandleLookTarget()` (which may never be called). The unsubscribe in `OnDestroy` is therefore potentially removing a handler that was never added. `C# -= on an event with no matching subscriber` is a no-op — this is safe, just slightly misleading. It would be clearer to only unsubscribe `OnLookTargetComplete` if it was actually subscribed (e.g., via a flag), but this is genuine over-engineering for this scope.

---

### L6 -- `GameRoundTracker.TryUpdateBest(0)` permanently locks best score display (confirmed edge case from R5)

**File:** `GameRoundTracker.cs` lines 29-35

```csharp
if (_bestScore == 0 || shots < _bestScore)
{
    _bestScore = shots;
    return true;
}
```

`TryUpdateBest(0)` sets `_bestScore = 0`. Subsequently `_bestScore == 0` is always true, so every future call returns true and sets best to whatever is passed. `GetStatsText()` shows `"--"` when `_bestScore == 0`, so calling `TryUpdateBest(0)` effectively resets the best display to `"--"` and makes the next genuine win always the new best.

Since `TryUpdateBest` is called with `_roundTracker.RoundShots` (which starts at 0 at round start, increments on shot), a 0-shot call is only possible if `HandleTargetHit` fires before any shot was counted — e.g., if auto-play fires and the guard at line 86 fails somehow. As established in H1 analysis, the `_isAutoPlaying` guard prevents this. But the API itself is foot-gun territory.

**Fix:** Add a guard `if (shots <= 0) return false;` at the top of `TryUpdateBest`.

---

### L7 -- `ObstacleSpawner` does not have `[RuntimeInitializeOnLoadMethod]` for static state reset

**File:** `ObstacleSpawner.cs`

`ObstacleSpawner` has no static fields — all state is instance fields. No static reset needed. ✓ (False alarm on initial check.)

---

### L8 -- `ExplosionEffect._burstCount`, `_particleLifetime`, `_startSpeed`, `_startSize` are `[SerializeField]` on a programmatically-instantiated GameObject

**File:** `explosion-burst-particle-effect.cs` lines 15-18

`ExplosionEffect` is instantiated at runtime via `Spawn(Vector2, bool)` which uses `new GameObject()` + `AddComponent`. The `[SerializeField]` attributes on `_burstCount` etc. are meaningless — they cannot be configured in the Inspector because there is no prefab. These inspector values will always be the default (C# initializer) values.

Either:
- Remove `[Header]` and `[SerializeField]` and make them `private const` / `private readonly`
- Or convert `ExplosionEffect` to use a prefab (higher effort)

Same concern applies to `RocketTrail` to a lesser degree — `RocketTrail` IS a component on the rocket prefab, so its `[SerializeField]` fields ARE inspector-configurable. Fine there.

---

### L9 -- `GroundScorch.Spawn` passes `_maxHeight` from `Rocket` but `Rocket` does not expose `_maxHeight`

**File:** `Rocket.cs` line 108, `ground-scorch-mark.cs` line 64

```csharp
// Rocket.cs
GroundScorch.Spawn(transform.position, _maxHeight);
```

`_maxHeight` is a private field of `Rocket` — this works because `Rocket` calls `GroundScorch.Spawn` directly. Fine. The `maxHeight` parameter in `GroundScorch.Spawn` defaults to `10f` which is a magic number:

```csharp
public static void Spawn(Vector2 impactPosition, float maxHeight = 10f)
```

The default of `10f` is arbitrary — it maps to a `heightAboveGround` of `10 - (-5) = 15`, which falls in the small-crater bucket. The comment on the parameter says "max flight height" but the default value isn't meaningful as a constant. Low severity since the default is only reached if called without the second argument (which only happens from `ExplosionEffect` if it ever calls it — it doesn't).

---

### L10 -- `RocketDebris` hardcodes `Gravity = 12f` as a module-level constant that duplicates/diverges from `Physics2D.gravity`

**File:** `rocket-debris-shatter-effect.cs` line 43

```csharp
private const float Gravity = 12f;
```

`Physics2D.gravity.magnitude` is the actual physics gravity (default 9.81). Debris uses a custom `12f` — intentional for "heavier" feel, but undocumented. This should have a comment like `// Intentionally heavier than physics gravity for dramatic arc`.

Also: `ObstacleSpawner.CalculateTrajectory` uses `Physics2D.gravity.magnitude` (line 65) — consistent with actual physics. But `RocketDebris` does not use physics rigidbody, so its custom gravity must match visually. No bug, just undocumented divergence.

---

### L11 -- `LaunchController._camera` null reference if `Camera.main` returns null

**File:** `LaunchController.cs` lines 58-59, 119

```csharp
Vector2 worldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
```

If `Camera.main` returns null in `Awake` (no camera tagged "MainCamera"), `_camera` is null and `HandleTouchBegan` throws NRE on first click. No null check exists. In a well-configured scene this never triggers, but it is an unguarded crash path.

---

### L12 -- `RoundManager` partial class split: auto-play file calls `StopAllCoroutines()` which also stops reload delays

**File:** `round-manager-auto-play-restart-and-target.cs` lines 17, 54

Both `HandleRestart()` and `HandleAutoPlay()` call `StopAllCoroutines()`. This is intentional per the comment on line 15-16. However, `StopAllCoroutines()` stops ALL coroutines on the RoundManager including any in-flight `DelayedAction` that was waiting to call `ReloadRocket` or `ReloadAfterAutoPlay`. This is the desired behavior (restart cancels pending reload). Fine.

**However:** if the user clicks **AutoPlay** during the 1.5s reload delay after a miss, `StopAllCoroutines()` cancels the reload, then auto-play fires without the rocket being reset first (since the reload was cancelled). Looking at `HandleAutoPlay`:

```csharp
public void HandleAutoPlay()
{
    if (_obstacleSpawner == null) return;
    StopAllCoroutines();
    ...
    _launchController.RotateRocketToDirection(dir);
    _rocket.Launch(dir, force);   // launches without resetting position
```

Auto-play launches the rocket without calling `_rocket.ResetToPosition()`. If the rocket is currently mid-reload delay (grounded, invisible), `_rocket.gameObject.SetActive` is still false (set at HandleTargetHit line 93 for win, but NOT set for miss — `HandleRocketMiss` doesn't deactivate the rocket, just hides the sprites via `SetSpritesVisible(false)`).

Wait — re-reading: `HandleRocketMiss` does NOT call `SetActive(false)`. `HandleTargetHit` does call `SetActive(false)` at line 93. So after a miss, the rocket GameObject is still active but sprites are hidden. Then `ReloadRocket` calls `_rocket.ResetToPosition()` which calls `SetSpritesVisible(true)`.

If auto-play is triggered during the 1.5s miss-reload delay:
- Rocket is active (after miss)
- Rocket sprites are hidden (via `SetSpritesVisible(false)`)
- `HandleAutoPlay` launches the rocket WITHOUT calling `ResetToPosition`
- The rocket launches from its impact position, not spawn point
- Sprites remain hidden during auto-play flight

**This is a real bug.** Auto-play after a miss launches an invisible rocket from the wrong position.

**Severity:** HIGH — but only triggered by clicking auto-play within 1.5s of a miss. Reclassifying.

---

## Revised Severity Summary

After full analysis:

| ID | Finding | Severity |
|----|---------|---------|
| H2 | Dual-subscribe fragility in HandleRestart | MEDIUM |
| H3 | Script execution order implicit dependency | MEDIUM |
| **H4** | **Auto-play after miss launches invisible rocket from wrong position** | **HIGH (real bug)** |
| M1 | ReloadAfterAutoPlay skips NewRound() | MEDIUM |
| M2 | Shot counted before launch + null _roundManager | MEDIUM |
| M6 | FindWithTag returns obstacle instead of ground | MEDIUM |
| M8 | GetOffset() advances elapsed as side effect | LOW |
| M9 | Phase accumulation bug in procedural audio | LOW |
| L2 | Serialized tag fields override SSOT | LOW |
| L6 | TryUpdateBest(0) locks best score | LOW |
| L8 | [SerializeField] on runtime-instantiated component | LOW |
| L11 | Camera null crash path | LOW |

---

## CRITICAL: Auto-Play After Miss Bug (reclassified from L12 discovery)

### BUG -- Auto-play after miss fires invisible rocket from wrong position

**File:** `round-manager-auto-play-restart-and-target.cs` lines 49-73

**Reproduction:**
1. Fire rocket, it misses (hits ground)
2. Within 1.5s reload delay, click Auto-Play
3. Result: auto-play fires from impact position, sprites invisible

**Root cause:** `HandleAutoPlay` does not reset the rocket state before launching.

`HandleAutoPlay` should call `_rocket.ResetToPosition(_spawnPoint.position)` before `_rocket.Launch(dir, force)`. The auto-play button is only shown after 5 misses (`ShowHints`), which means the button could be clicked mid-reload after miss #5, #6, etc.

**Additionally:** After the auto-play click hides the auto-play button (`HideAutoPlayButton`), but does not hide the hint texts (`_angleText`, `_forceText`). These remain visible during auto-play flight. Minor cosmetic issue.

---

## Positive Observations

- Event cleanup in `OnDestroy` is comprehensive across all MonoBehaviours — no event leaks found
- `RuntimeSpriteFactory` lifecycle management (destroy texture before sprite) is correct
- `StopActiveCoroutine()` pattern in `CameraController` properly prevents coroutine accumulation
- `[DisallowMultipleComponent]` on singleton classes (AudioManager, RoundManagerHUD) is correct
- `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` on all static-state holders is thorough
- `DelayedAction` pattern in `RoundManager` is clean and DRY
- `GroundScorch.GetGroundY` using sqrMagnitude-avoiding direct math is performant
- `GameRoundTracker` as a plain C# class (not MonoBehaviour) is architecturally correct — pure data logic
- `ObstacleSpawner` trajectory calculation with a bounded attempt loop prevents infinite spawn loops

---

## Recommended Actions (Prioritized)

1. **(HIGH — Bug)** Fix auto-play after miss: add `_rocket.ResetToPosition(_spawnPoint.position)` and `SetActive(true)` at the start of `HandleAutoPlay`, before `_rocket.Launch`.
2. **(MEDIUM)** Guard `_roundManager` null before `OnShotFired()` call in `LaunchController.HandleTouchEnded`.
3. **(MEDIUM)** Fix `GroundScorch.PrepareGround`: use a dedicated tag for the ground tile (not `TagGround` shared with obstacles), or inject the ground SpriteRenderer via reference.
4. **(MEDIUM)** Document or fix `ReloadAfterAutoPlay` missing `NewRound()` call — add comment explaining intent.
5. **(LOW)** Remove `[SerializeField]` from `Rocket._groundTag` / `_targetTag`; make them `private const`.
6. **(LOW)** Add guard `if (shots <= 0) return false` to `GameRoundTracker.TryUpdateBest`.
7. **(LOW)** Remove `[Header]`/`[SerializeField]` from `ExplosionEffect` inspector fields (component is runtime-instantiated, not prefab-based).
8. **(LOW)** Add comment to `RocketDebris.Gravity = 12f` explaining intentional divergence from `Physics2D.gravity`.
9. **(LOW)** Rename `CameraScreenShake.GetOffset()` to `ConsumeShakeOffset()` or refactor to separate Tick/Read.
10. **(LOW)** Null-check `_camera` in `LaunchController.HandleTouchBegan` and `TryComputeDrag`.
11. **(LOW)** Fix phase accumulation in `ProceduralAudioClipGenerator.CreateStretch` and `CreateClick` for correct frequency sweep.
12. **(LOW)** Add hint-text hide to `HandleAutoPlay` (hints remain visible during auto-play flight).

---

## Metrics

| Metric | Value |
|--------|-------|
| Files reviewed | 18 runtime C# + 2 test files |
| Total lines analyzed | ~2,100 runtime |
| Critical issues | 0 |
| High | 1 (auto-play invisible rocket bug) |
| Medium | 5 |
| Low | 11 |
| Tests | 14 (unchanged, all passing) |
| Shader null-check (R5-H1) | Still unresolved — shader fallback added but `Hidden/InternalErrorShader` is a worse fallback than `UI/Default` |
| Files over 200 lines | 2 runtime (RoundManager split across 2 files ~280 lines total, CameraController ~255) |
| Score | 8.5 / 10 — no net change (new bugs found offset prior improvements) |

---

## Unresolved Questions

1. Is `ReloadAfterAutoPlay` intentionally skipping `NewRound()`? Should auto-play be "round-neutral"?
2. Is auto-play meant to be triggerable only at the start of a round (no rocket in flight, no pending reload)? If so, add a `!_isPendingReload` guard to `ShowHints` or `HandleAutoPlay`.
3. Should `TryUpdateBest(0)` be treated as invalid input (add guard) or is a 0-shot win theoretically possible?
4. The `Shader.Find` fallback in `RuntimeSpriteFactory` uses `Hidden/InternalErrorShader` — this shows a pink material. A better fallback is `UI/Default` or `Sprites/Default` from a Resources folder. Is this intentional for debugging visibility?
