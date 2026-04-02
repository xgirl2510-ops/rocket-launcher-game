# Code Review — Post-Refactor World-Class Assessment

**Date:** 2026-04-02
**Base SHA:** ef32d60
**Head SHA:** 5758e78
**Scope:** All runtime scripts in `Assets/Scripts/` + all editor scripts in `Assets/Editor/`

---

## Code Review Summary

### Scope
- Files reviewed: 19 (15 runtime scripts + 4 editor partial-class files)
- Lines of code analyzed: ~3,394 (game scripts only; TMP examples excluded)
- Review focus: Full codebase post-refactor audit

### Overall Assessment

The codebase has made genuine, measurable progress since the 6/10 audit. The god-class split, namespace adoption, static state guards, and material/texture leak fixes are real improvements. The architecture is now correctly layered and self-consistent. However, a cluster of medium-weight issues prevents a "world-class" rating — primarily: two texture leaks that weren't fully fixed, one logic bug (`_isAutoPlaying` not reset on restart), one fragile singleton pattern (`RoundManagerHUD`), an audio DSP correctness issue, and the old `Input` system still in use.

**Overall score: 7.5 / 10** (up from 6/10)

---

## Dimension Scores

| # | Dimension | Score | Notes |
|---|-----------|-------|-------|
| 1 | Architecture & Organization | 8.5 | Clean separation LC/RM/HUD, event-driven, namespace throughout |
| 2 | Code Quality | 8.0 | Readable, good comments, consistent style |
| 3 | Unity Best Practices | 7.0 | Input.mousePosition, Shader.Find at runtime, broken singleton pattern |
| 4 | Maintainability | 8.0 | Single-responsibility upheld, GameConstants, well-named files |
| 5 | Performance | 7.5 | No hot-path GC issues; two texture leaks remain |
| 6 | Robustness | 7.0 | _isAutoPlaying reset bug, SetState doesn't stop coroutines |
| 7 | Scalability | 7.5 | Adding new game modes is straightforward, static utilities limit multi-scene |

---

## Critical Issues

None. No data loss risks, no crashes under normal play.

---

## High Priority Findings

### H1 — `_isAutoPlaying` not reset on `HandleRestart()`
**File:** `Assets/Scripts/Core/RoundManager.cs` line 127
**Impact:** If the player clicks Restart while auto-play is in flight, `_isAutoPlaying` stays `true`. Next miss/hit fires `ReloadAfterAutoPlay()` instead of `ReloadRocket()` — HUD win-screen never shows, stats not recorded, round logic breaks.

`HandleRestart()` calls `StopAllCoroutines()` which kills the delayed-action coroutine, but never sets `_isAutoPlaying = false`.

Fix:
```csharp
public void HandleRestart()
{
    StopAllCoroutines();
    _isAutoPlaying = false;  // <-- add this
    ...
}
```

### H2 — `RocketTrail._cachedParticleMaterial` missing `[RuntimeInitializeOnLoadMethod]` reset guard
**File:** `Assets/Scripts/Effects/rocket-trail-particle-effect.cs` line 20
**Impact:** In Unity Editor, after a domain reload (script compile), the static field survives as a stale reference pointing to a destroyed object. `GetParticleMaterial()` will then return `null.Check()` = true but the material is invalid. In-editor play restarts will produce console errors or invisible trails.

Every other class with statics (`GroundScorch`, `RocketDebris`, `ObstacleSpawner`, `AudioManager`, `RoundManagerHUD`) correctly has this guard. `RocketTrail` is the only one missing it.

Fix — add to `RocketTrail`:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState()
{
    _cachedParticleMaterial = null;
}
```

### H3 — `ObstacleSpawner` and `RocketDebris` texture leaks on domain reload
**File:** `Assets/Scripts/Obstacles/ObstacleSpawner.cs` line 35 / `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs` line 42
**Impact:** Both `ResetStaticState()` null the cached `Sprite` reference but never call `Destroy(sprite.texture)` + `Destroy(sprite)`. `GroundScorch.ResetStaticState()` correctly calls `DestroySprites()`. The other two silently leak a `Texture2D` + `Sprite` asset into VRAM on each editor domain reload.

Fix for both:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStaticState()
{
    if (_cachedSprite != null)
    {
        Object.Destroy(_cachedSprite.texture);
        Object.Destroy(_cachedSprite);
        _cachedSprite = null;
    }
}
```

---

## Medium Priority Improvements

### M1 — `RoundManagerHUD` singleton: `Destroy(this)` vs `Destroy(gameObject)`
**File:** `Assets/Scripts/Core/round-manager-hud.cs` line 38
**Impact:** When a duplicate `RoundManagerHUD` is found, `Destroy(this)` only destroys the component, leaving a dangling empty GameObject in the hierarchy. Harmless now, confusing when debugging. `AudioManager` correctly uses `Destroy(gameObject)`.

Fix:
```csharp
if (Instance != null && Instance != this)
{
    Destroy(gameObject);  // was: Destroy(this)
    return;
}
```

### M2 — Old `Input` system (`Input.mousePosition`, `Input.GetMouseButton`)
**File:** `Assets/Scripts/Launch/LaunchController.cs` lines 43–53
**Impact:** Legacy `Input` is deprecated in Unity 6. It works in the editor but does not support the new Input System's touch abstraction for iOS/Android, preventing proper multi-touch and preventing migration to `InputSystem.EnhancedTouch`. For a mobile-targeted slingshot game this is a real gap.

Not a blocker for current desktop testing, but blocks mobile deployment.

Recommended path: wrap behind an interface or migrate to `InputSystem.TouchPhase` / `UnityEngine.InputSystem.EnhancedTouch.Touch`.

### M3 — `Shader.Find("Sprites/Default")` called lazily at runtime
**File:** `Assets/Scripts/Effects/rocket-trail-particle-effect.cs` line 66
**Impact:** `Shader.Find` performs a string lookup across all loaded shaders. Called once (lazy-cached), so not a per-frame issue, but it fires mid-gameplay on first rocket launch. On some platforms this can cause a hitch. Unity best practice: assign shader via `[SerializeField]` or use `Resources.Load` with a pre-assigned material asset.

Quick fix (no serialization needed):
```csharp
// Replace Shader.Find at first-use with a built-in reference
_cachedParticleMaterial = new Material(Shader.Find("Sprites/Default"));
// Or expose via [SerializeField] on the MonoBehaviour
```
This is low-risk at current scale but worth noting.

### M4 — `CameraController.SetState()` doesn't stop active coroutines
**File:** `Assets/Scripts/Camera/CameraController.cs` lines 238–242
**Impact:** `HandleRocketLaunched/Landed/HitTarget` all call `SetState()` which only changes `_currentState` and resets `_smoothVelocity`. If `HandleRocketLaunched` fires while an intro/return coroutine is still executing (edge case: manual test, or if input is enabled too early), the coroutine continues modifying camera position while `LateUpdate` also moves it. Current flow prevents this in practice (intro always completes before input is enabled), but it's an invisible time bomb.

Fix: call `StopActiveCoroutine()` inside `SetState()`.

### M5 — Audio DSP phase accumulation bug in `CreateLaunchWhoosh`, `CreateStretch`, `CreateClick`
**File:** `Assets/Scripts/Audio/ProceduralAudioClipGenerator.cs` lines 26, 135, 154
**Impact:** These use `freq * t * duration` instead of a running phase accumulator. For a frequency-swept sound like `CreateLaunchWhoosh`, this produces an incorrect waveform — the instantaneous frequency is `d/dt(freq(t) * t * duration)` which is non-linear and doesn't match the intended sweep. The perceptual result is a slightly warped pitch envelope. Not a crash but the sounds don't match their docstring description precisely.

`CreateThrustLoop` and `CreateGroundHit` use `(float)i / SampleRate` as phase which is correct.

Correct sweep pattern:
```csharp
float phaseAccumulator = 0f;
for (int i = 0; i < samples; i++)
{
    float t = (float)i / samples;
    float freq = Mathf.Lerp(startFreq, endFreq, t);
    phaseAccumulator += freq / SampleRate;
    data[i] = Mathf.Sin(2f * Mathf.PI * phaseAccumulator) * envelope;
}
```

### M6 — `GameRoundTracker.TryUpdateBest` uses 0 as sentinel for "no best"
**File:** `Assets/Scripts/Core/GameRoundTracker.cs` line 30
**Impact:** `_bestScore == 0` as "unset" means a 0-shot win (impossible with current mechanics, but still semantically wrong). A hole-in-one mechanic added later would break this. Better to use `int? _bestScore = null` or `int.MaxValue` as initial value.

Minor, low risk with current game design.

---

## Low Priority Suggestions

### L1 — `RoundManager.HandleRestart()` event subscription pattern
Lines 151–152: unsubscribe then re-subscribe `OnIntroComplete`. This is correct but the pattern slightly unusual — a `bool _introSubscribed` guard or using `-=` only once in `OnIntroDone` (already done) would be cleaner. Current code is safe.

### L2 — `FollowRocket()` allocates two `Vector2` structs per `LateUpdate`
**File:** `CameraController.cs` lines 131–135
These are value-type stack allocations — no heap GC pressure in C#. Not an actual problem. Mentioning only because it superficially looks like an allocation. Confirmed safe.

### L3 — `ObstacleSpawner.CalculateTrajectory()` force estimation uses magic `1.5f` multiplier
Line 75: `vSquared = g * (dy + Mathf.Sqrt(dx*dx + dy*dy)) * 1.5f`. The 1.5× fudge factor gives a high arc but is not derived from physics first principles. A comment explaining "empirical 1.5× factor for high-arc guarantee" would prevent future confusion.

### L4 — Kebab-case file naming inconsistency
`round-manager-hud.cs` → class `RoundManagerHUD` is a mixed naming convention. Unity requires the filename to match the class name for MonoBehaviours when working with prefabs/serialization. This works because it's found by namespace+class, not filename — but the Unity Editor UI may flag mismatches and it deviates from the PascalCase convention used by all other MonoBehaviour files.

### L5 — `ProceduralAudioClipGenerator.CreateLaunchWhoosh()` is unreferenced
**File:** `AudioManager.cs` — `AudioManager` uses `.mp3` files for launch/thrust, and `CreateLaunchWhoosh()` / `CreateThrustLoop()` in `ProceduralAudioClipGenerator` are never called. Dead code. Low clutter risk but violates YAGNI.

---

## Positive Observations

- **God-class split is excellent.** `LaunchController` (input only), `RoundManager` (game flow), `RoundManagerHUD` (UI) is a textbook SRP application. Each class is small, focused, and independently testable.
- **Event cleanup in `OnDestroy` is thorough.** All three event consumers (`RoundManager`, `CameraController`, `RoundManagerHUD`) properly unsubscribe. No event leaks.
- **`RuntimeInitializeOnLoadMethod` usage** in 5 out of 6 classes that need it is commendable and rarely done correctly by Unity developers.
- **`GameConstants` as single source of truth** for tags and layout magic numbers is good DRY practice.
- **`GameRoundTracker` as a plain C# class** (not MonoBehaviour) is the right pattern — pure data model, zero Unity dependency.
- **Coroutine race prevention in `CameraController`** via `_activeCoroutine` handle is solid.
- **`GroundScorch.DestroySprites()`** with explicit texture + sprite `Destroy()` calls is the correct GPU memory cleanup pattern — the standard that `ObstacleSpawner` and `RocketDebris` should follow.
- **`ObstacleSpawner` trajectory math** is genuinely good: the discriminant check, high-angle solution, and `sqrMagnitude` for overlap detection are all professional-grade.
- **`ProceduralAudioClipGenerator`** as a pure static class with no Unity lifecycle coupling is clean and portable.
- **Editor tool partial class split** across 4 focused files is maintainable and keeps individual files under 370 lines.

---

## Recommended Actions

1. **[High, 5min]** `RoundManager.HandleRestart()` — add `_isAutoPlaying = false;` (H1)
2. **[High, 5min]** `RocketTrail` — add `[RuntimeInitializeOnLoadMethod]` reset for `_cachedParticleMaterial` (H2)
3. **[High, 15min]** `ObstacleSpawner` + `RocketDebris` — add proper `Destroy(sprite.texture)` + `Destroy(sprite)` in reset guards, matching `GroundScorch` pattern (H3)
4. **[Medium, 2min]** `RoundManagerHUD` — change `Destroy(this)` to `Destroy(gameObject)` (M1)
5. **[Medium, 30min]** `CameraController.SetState()` — call `StopActiveCoroutine()` inside (M4)
6. **[Medium, 2min]** Remove unreferenced `CreateLaunchWhoosh()` and `CreateThrustLoop()` from `ProceduralAudioClipGenerator` (L5)
7. **[Low, backlog]** Migrate `Input.mousePosition` to `UnityEngine.InputSystem` for mobile readiness (M2)
8. **[Low, backlog]** Fix DSP phase accumulation in swept-frequency procedural audio (M5)

---

## Metrics

| Metric | Value |
|--------|-------|
| Files reviewed | 19 |
| Total LOC | ~3,394 |
| Critical issues | 0 |
| High priority | 3 |
| Medium priority | 6 |
| Low priority | 5 |
| TODO/FIXME comments remaining | 0 |
| Domain-reload guards: statics covered | 5/6 (RocketTrail missing) |

---

## Unresolved Questions

1. **Mobile target confirmed?** If iOS/Android is in scope, `Input.mousePosition` migration (M2) moves from "backlog" to "High" immediately.
2. **Single-scene assumption:** `GroundScorch`, `RocketDebris`, `ObstacleSpawner` are all static utilities. If a second scene is ever loaded additively, their state persists incorrectly. Is multi-scene out of scope permanently?
3. **`ProceduralAudioClipGenerator.CreateLaunchWhoosh` / `CreateThrustLoop`** — were these intentionally kept as fallback stubs, or dead code to be removed?
