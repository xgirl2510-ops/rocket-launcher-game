# Code Review: Post-Fix Codebase Review

**Date:** 2026-04-02
**Reviewer:** code-reviewer
**Scope:** Full codebase review after bug-fix commit `747c0ff`
**Base:** `848eff9` | **Head:** `747c0ff`

---

## Previous Issue Verification

### CRITICAL Issues

| # | Issue | Status | Verdict |
|---|-------|--------|---------|
| 1 | Static `_allDebris` leak | **FIXED** | `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` added at line 20 of `rocket-debris-shatter-effect.cs`. List clears on domain reload. Correct. |
| 2 | Uncancelled `Invoke` | **FIXED** | `CancelInvoke()` added at top of both `HandleRestart()` (line 294) and `HandleAutoPlay()` (line 361) in `LaunchController.cs`. Prevents stale `ReloadRocket`/`ReloadAfterAutoPlay` from firing after state reset. |
| 3 | Magic constant `GroundY = -5f` | **PARTIALLY FIXED** | `GameConstants.cs` created, `RocketDebris` references it. **BUT** the editor tool `rocket-launcher-scene-setup-environment-and-gameplay-objects.cs` still computes `GroundTop` independently via `CamBottom + GroundVisibleHeight` (line 27). These happen to produce the same value (-5) but are **not linked** to `GameConstants.GroundTop`. If one changes, the other drifts silently. |

### IMPORTANT Issues

| # | Issue | Status | Verdict |
|---|-------|--------|---------|
| 4 | ObstacleSpawner texture leak | **FIXED** | `_cachedSquareSprite` static field added (line 30). `CreateSquareSprite()` now returns cached sprite on subsequent calls. Texture+sprite created only once per app session. |
| 5 | Identical hit sounds | **PARTIALLY FIXED** | `AudioManager` now has `_groundHitClip` (procedural) and `PlayHitGround()` falls back to it when `_boomClip` is null (line 57). `PlayHitTarget()` still plays only `_boomClip` (line 63). `CreateTargetHit()` exists in `ProceduralAudioClipGenerator` but is **never called**. When mp3 `_boomClip` is assigned, both ground and target play the **exact same clip**. Differentiation only works when mp3 is missing. |
| 6 | Drag vector duplication | **FIXED** | `TryComputeDrag()` helper extracted (lines 193-212). Both `HandleTouchMoved` and `HandleTouchEnded` use it. Clean DRY improvement. |
| 7 | Null-check `_aimArrow` in DisableInput | **FIXED** | `_aimArrow?.Hide()` at line 452. Also added null checks in `HandleTouchMoved` (line 142) and `HandleTouchEnded` (line 166). Consistent. |
| 8 | Unused MissText | **FIXED** | `CreateTMPLabel(..., "MissText", ...)` removed from UI setup. No references remain in codebase. |

**Score: 6/8 fully fixed, 2/8 partially fixed.**

---

## NEW Issues Introduced by Fixes

### HIGH: `_cachedSquareSprite` in ObstacleSpawner not cleared on domain reload

`ObstacleSpawner._cachedSquareSprite` is `static` (line 30) but has **no** `[RuntimeInitializeOnLoadMethod]` reset. In Editor with domain reload disabled, the sprite reference can become stale (pointing to destroyed Unity object). This is the **exact same class of bug** that was fixed for `_allDebris`.

**File:** `/Users/Luke/Downloads/Programming/Game/Assets/Scripts/Obstacles/ObstacleSpawner.cs`
**Fix:**
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState() => _cachedSquareSprite = null;
```

### MEDIUM: `AudioManager.Instance` not cleared on domain reload

Singleton pattern with `public static AudioManager Instance` (line 9) but no `[RuntimeInitializeOnLoadMethod]` to clear it. Same stale-reference risk in Editor play mode.

**File:** `/Users/Luke/Downloads/Programming/Game/Assets/Scripts/Audio/AudioManager.cs`
**Fix:**
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState() => Instance = null;
```

---

## Remaining Issues (Pre-existing, Not Part of This Fix)

### HIGH Priority

1. **Event subscription leak in `CameraController.Start()`** (lines 71-73)
   Lambda subscriptions (`() => SetState(...)`) to `_rocket.OnRocketLaunched/Landed/Hit` are **never unsubscribed**. If `CameraController` is destroyed and recreated (e.g., scene reload), old subscriptions pile up. Unlike `LaunchController` which uses named methods and unsubscribes in `OnIntroDone`, these lambdas cannot be unsubscribed. Should use named methods + `OnDestroy` cleanup.

2. **No `OnDestroy` in `LaunchController`** — event subscriptions to `_rocket.OnRocketLanded` and `_rocket.OnTargetHit` (lines 79-80) are never unsubscribed. If the LC is destroyed before the rocket, the rocket holds dead references.

3. **Debris texture leak per-spawn** — `RocketDebris.Spawn()` (lines 53-59) creates a **new `Texture2D` and `Sprite` for EVERY debris piece** (16 per explosion). With ~16 pieces per miss, this allocates 16 textures that are never explicitly destroyed. The `Destroy(gameObject)` in `OnDestroy` destroys the GO but not the orphaned `Texture2D` asset. Should use a shared static sprite like `ObstacleSpawner` does.

4. **`RocketDebris.OnDestroy` does `_allDebris.Remove(gameObject)`** — This is O(n) per debris piece. With 16 debris, `ClearAll()` calls `Destroy()` which triggers `OnDestroy` which calls `Remove` for each, making it O(n^2). Not critical at n=16 but bad pattern. `ClearAll` already clears the list, so `OnDestroy` does redundant work.

### MEDIUM Priority

5. **`LaunchController` at 454 lines** — exceeds the project's 200-line guideline. Should split into: input handling, UI management, and game state/flow control.

6. **`CameraController` at 273 lines** — exceeds 200-line guideline. Pan coroutines are structurally identical (same lerp+SmoothStep pattern repeated 3 times: Intro, Return, PanToTarget). Extract a `PanCoroutine(from, to, duration)` helper.

7. **`ProceduralAudioClipGenerator` at 208 lines** — marginally over limit. Each clip generator follows an identical pattern (create clip, fill samples in loop, return). Acceptable for now since splitting would reduce readability.

8. **Editor tool `GroundTop` not derived from `GameConstants`** — The editor partial class computes `GroundTop = CamBottom + GroundVisibleHeight` (line 27). This is editor-only code and can't reference runtime `GameConstants` directly (it's in a different assembly). Consider: (a) making `GameConstants` an editor-shared file, or (b) adding a compile-time assertion.

9. **Magic number `2f` for `_homeY` in `CameraController`** — hardcoded default for `[SerializeField] private float _homeY = 2f` (line 24), same as `CamY` in editor tool but not linked. If camera Y changes, this drifts.

10. **`PanToTargetCoroutine` uses magic `2f` seconds wait** (line 222) — should be a `[SerializeField]` for tunability.

### LOW Priority

11. **`CreateTargetHit()` in `ProceduralAudioClipGenerator` is dead code** — fully implemented method (lines 101-139) never called anywhere. Either wire it into `AudioManager.PlayHitTarget()` or delete it.

12. **No `using` guard on `Random` in `ProceduralAudioClipGenerator`** — uses `Random.value` which is `UnityEngine.Random`. Fine but fragile if `System` namespace is ever added.

13. **`Debug.Log` left in production code** — `CameraController.IntroCoroutine()` line 119: `Debug.Log("[CameraController] Intro complete")`. Should be removed or wrapped in `#if UNITY_EDITOR`.

14. **`_totalShots` tracked but never displayed** — `LaunchController` increments `_totalShots` (line 177) but `UpdateStatsUI` only shows `_roundShots`. Either display it or remove it.

---

## Area Ratings

| Area | Score | Notes |
|------|-------|-------|
| **Architecture / Separation of Concerns** | 7/10 | Good event-driven design. Rocket fires events, controllers subscribe. Editor tool well-split into partial classes. BUT `LaunchController` is a god class (input + UI + game flow + audio). Camera lambdas are a smell. |
| **Maintainability / File Size** | 6/10 | 3 files exceed 200-line project standard (454, 273, 208 lines). `LaunchController` needs splitting most urgently. DRY improvement with `TryComputeDrag` is good. Camera pan code still has 3 near-identical coroutines. |
| **Code Quality (YAGNI/KISS/DRY)** | 7/10 | `TryComputeDrag` is clean DRY. `GameConstants` is KISS. Dead `CreateTargetHit()` violates YAGNI. Unused `_totalShots` violates YAGNI. Debris texture-per-piece violates DRY (should share sprite like `ObstacleSpawner`). |
| **Unity C# Conventions** | 7/10 | Correct `[SerializeField] private _camelCase`. Events use `OnPascalCase`. Physics in `FixedUpdate`, camera in `LateUpdate`. File names use kebab-case (project convention, not Unity convention but intentional). Missing `OnDestroy` cleanup is a convention violation. |
| **Potential Issues / Bugs** | 6/10 | Two new stale-static bugs (`_cachedSquareSprite`, `Instance`). Event leak in `CameraController`. Texture leak in debris. `PlayHitTarget` vs `PlayHitGround` still sound identical when mp3 present. |

---

## Overall Score: 6.6 / 10

**Verdict: Not "World Class" yet.** The fix commit addressed the right issues but introduced one new bug of the same category it was fixing (stale static without `[RuntimeInitializeOnLoadMethod]`). Two partially-fixed issues mean the original problems persist in edge cases. The codebase has solid architectural bones (event-driven, partial classes for editor) but suffers from missing cleanup patterns (`OnDestroy`), file size violations, and dead code.

### Path to 8+/10

1. Add `[RuntimeInitializeOnLoadMethod]` to **all** static fields: `ObstacleSpawner._cachedSquareSprite`, `AudioManager.Instance`
2. Add `OnDestroy` to `LaunchController` and `CameraController` to unsubscribe events
3. Wire `CreateTargetHit()` into `AudioManager.PlayHitTarget()` so hits sound different
4. Share a static sprite in `RocketDebris.Spawn()` instead of creating texture per-piece
5. Split `LaunchController` into 2-3 files (input, UI, game flow)
6. Extract common pan coroutine in `CameraController`

### Path to 9+/10

7. Link editor `GroundTop` to `GameConstants` (or add assertion)
8. Remove dead code (`_totalShots`, `Debug.Log` in production)
9. Make camera wait duration configurable (`[SerializeField]`)
10. All files under 200 lines

---

## Unresolved Questions

1. Is `ObstacleSpawner._cachedSquareSprite` intentionally static-without-reset, or was the pattern just missed? (Likely missed — same bug class as the fixed `_allDebris` issue.)
2. Should `CreateTargetHit()` be wired in, or was it left as dead code intentionally for future use? If future use, it should be documented with a `// TODO:` comment.
3. The editor tool computes `GroundTop` via `CamBottom + GroundVisibleHeight`. Since editor code is in a separate assembly, is there a preferred pattern for sharing constants between runtime and editor assemblies in this project?
