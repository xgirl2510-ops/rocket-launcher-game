# Backend Code Review — Rocket Launcher
**Date:** 2026-04-06  
**Reviewer:** code-reviewer (backend)

---

## Code Review Summary

### Scope
- **Files reviewed:** 12
- **Lines of code analyzed:** ~1,330
- **Files:**
  - `Assets/Scripts/Core/RoundManager.cs`
  - `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs`
  - `Assets/Scripts/Core/round-manager-hud.cs`
  - `Assets/Scripts/Core/GameRoundTracker.cs`
  - `Assets/Scripts/Core/GameConstants.cs`
  - `Assets/Scripts/Core/runtime-sprite-factory.cs`
  - `Assets/Scripts/Rocket/Rocket.cs`
  - `Assets/Scripts/Launch/LaunchController.cs`
  - `Assets/Scripts/Launch/AimArrow.cs`
  - `Assets/Scripts/Audio/AudioManager.cs`
  - `Assets/Scripts/Audio/ProceduralAudioClipGenerator.cs`
  - `Assets/Scripts/Obstacles/ObstacleSpawner.cs`

### Overall Assessment
Well-structured, readable codebase with clear separation of concerns (input → round flow → HUD → audio). Event-driven architecture is mostly respected. Two issues need immediate fixes before next release: a NullReferenceException path in LaunchController and a broken audio pitch effect in AudioManager. Remaining issues are architecture/quality improvements.

### Findings by Severity
- **Critical:** 2
- **Important:** 5
- **Moderate:** 5

---

## Critical Issues

**[CRITICAL] NullReferenceException path in LaunchController — `LaunchController.cs:107`**
```csharp
RoundManagerHUD.Instance?.UpdateStatsUI(_roundManager.RoundTracker);
```
`_roundManager` has no null guard here. `RoundManagerHUD.Instance?.` applies the null check only to the singleton, not to `_roundManager`. If `_roundManager` is unassigned (inspector misconfiguration), this throws a NullReferenceException on every shot.
- **Recommendation:** Change to `_roundManager?.RoundTracker` or add an `if (_roundManager == null) return;` guard at top of `HandleTouchEnded()`.

---

**[CRITICAL] Broken pitch effect in AudioManager — `AudioManager.cs:68–71`**
```csharp
public void PlayHitTarget()
{
    _oneShotSource.pitch = 1.3f;
    if (_boomClip != null) _oneShotSource.PlayOneShot(_boomClip);
    else if (_targetHitClip != null) _oneShotSource.PlayOneShot(_targetHitClip);
    _oneShotSource.pitch = 1.0f;   // immediately reset
}
```
`PlayOneShot` is async but AudioSource pitch is read at call time. The reset on the next line executes synchronously, meaning the sound may play at 1.0 (reset already in effect by audio thread scheduling) rather than 1.3. The pitch effect is broken. Additionally, if `PlayLaunch()` or any other call fires between pitch set and reset (multi-frame timing), other clips on `_oneShotSource` will be affected.
- **Recommendation:** Use a dedicated `AudioSource` for target-hit with `pitch = 1.3f` baked in (or use `PlayOneShot` with the `volumeScale` parameter only and apply pitch via a separate AudioSource). Alternatively, create the "higher pitch" clip in `ProceduralAudioClipGenerator` at the correct pitch natively.

---

## Important Issues

**[IMPORTANT] Rocket directly calls static effects bypassing event architecture — `Rocket.cs:121`**
```csharp
RocketDebris.SpawnTargetDebris(other.transform.position);  // direct static call
OnImpact?.Invoke(transform.position, true, _maxHeight);    // event also fired (line 120)
```
Two mechanisms for the same impact: direct `RocketDebris.SpawnTargetDebris` call AND `OnImpact` event. Debris spawning should be handled by a subscriber of `OnImpact`, not wired directly in `Rocket`. This breaks the single-responsibility boundary and means Rocket has a compile-time dependency on `RocketDebris`.
- **Recommendation:** Remove the direct `RocketDebris.SpawnTargetDebris` call from `Rocket.cs`. Wire a subscriber (RoundManager or an ImpactEffectsHandler) to `OnImpact` that calls `RocketDebris.SpawnTargetDebris` when `isHit == true`.

---

**[IMPORTANT] Magic string layer name with silent failure path — `ObstacleSpawner.cs:170`**
```csharp
go.layer = LayerMask.NameToLayer("Default");
```
`LayerMask.NameToLayer` returns `-1` if the layer name is not found. Unity silently accepts -1 as a layer index and behavior is undefined. There is no assertion or log. Obstacles also use `GameConstants.TagGround` tag — if rocket's physics matrix doesn't include collision with "Default" layer, obstacles won't collide with the rocket.
- **Recommendation:** Add `const int DefaultLayer = 0;` to `GameConstants` and use `go.layer = GameConstants.DefaultLayer;` (layer 0 is always "Default" in Unity). Add `Debug.Assert(go.layer >= 0)` guard.

---

**[IMPORTANT] Best score not persisted across sessions — `GameRoundTracker.cs`**
`_bestScore` is an in-memory field. It resets every time the game is launched or play mode exits in editor.
- **Recommendation:**
```csharp
private const string BestScoreKey = "BestScore";
public void Load() => _bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
public bool TryUpdateBest(int shots)
{
    // ... existing logic ...
    if (updated) PlayerPrefs.SetInt(BestScoreKey, _bestScore);
    return updated;
}
```
Call `Load()` from `GameRoundTracker` constructor or from `RoundManager.Awake`.

---

**[IMPORTANT] Misleading variable naming causes logic confusion — `ObstacleSpawner.cs:135–136`**
```csharp
float minDistSqr = _obstacleMaxSize * 1.2f;   // NOT squared yet
minDistSqr *= minDistSqr;                       // squared here
```
The variable is named `minDistSqr` before it's squared. Any future reader will be confused by the first line: a variable named `*Sqr` holding a non-squared value.
- **Recommendation:**
```csharp
float minSeparation = _obstacleMaxSize * 1.2f;
float minSepSqr = minSeparation * minSeparation;
```

---

**[IMPORTANT] Asymmetric event subscriptions between Start and OnDestroy — `RoundManager.cs:149–150`**
```csharp
// Start() subscribes:
_cameraController.OnIntroComplete += OnIntroDone;
// (OnLookTargetComplete NOT subscribed in Start)

// OnDestroy() unsubscribes:
_cameraController.OnIntroComplete -= OnIntroDone;
_cameraController.OnLookTargetComplete -= OnLookTargetDone;  // may never have been subscribed
```
`OnLookTargetComplete` is subscribed conditionally in `HandleLookTarget()`, but always unsubscribed in `OnDestroy`. While C# silently handles removing a non-subscribed handler, this asymmetry signals incomplete subscription tracking.
- **Recommendation:** Document clearly which events are lifecycle-persistent vs. one-shot (subscribed/unsubscribed together). Either add `OnLookTargetComplete -= OnLookTargetDone` only when subscribed, or convert to a `_lookTargetSubscribed` flag pattern.

---

## Moderate Issues

**[MODERATE] Magic string "Gameplay" sorting layer — silent failure — `ObstacleSpawner.cs:178`**
```csharp
sr.sortingLayerName = "Gameplay";
```
If the "Gameplay" sorting layer doesn't exist, Unity silently falls back to "Default" — no error, no log. Obstacles may render incorrectly.
- **Recommendation:** Add to `GameConstants`: `public const string SortingLayerGameplay = "Gameplay";` and validate with `SortingLayer.NameToID` in `OnValidate`.

---

**[MODERATE] `System.Random` instead of `UnityEngine.Random` in audio generator — `ProceduralAudioClipGenerator.cs:23,103`**
```csharp
var rng = new System.Random();
```
Inconsistent with the rest of the codebase. `UnityEngine.Random.Range(-1f, 1f)` is idiomatic and avoids mixing two RNG systems.
- **Recommendation:** Replace `(float)rng.NextDouble() * 2f - 1f` with `UnityEngine.Random.Range(-1f, 1f)`. Note: `System.Random` is seeded by system clock at construction; both have equivalent randomness properties for this use case.

---

**[MODERATE] `Camera.main` null fallback in Update — `LaunchController.cs:50`**
```csharp
if (_camera == null) _camera = Camera.main;
```
`Camera.main` calls `FindObjectOfType<Camera>` under the hood — O(n) scene traversal. Guarded by null check so it only runs when `_camera` is null (should be never after `Awake`). But if camera is ever destroyed mid-session (e.g., scene reload edge case), this runs every frame. `_camera` should be validated in `Awake`; the Update fallback should include a warning.
- **Recommendation:**
```csharp
// In Awake:
_camera = Camera.main;
if (_camera == null) Debug.LogError("[LaunchController] No main camera found.", this);

// Remove the null fallback from Update.
```

---

**[MODERATE] Unidiomatic pixel fill loop in RuntimeSpriteFactory — `runtime-sprite-factory.cs:37–38`**
```csharp
var pixels = new Color[16];
for (int i = 0; i < 16; i++) pixels[i] = Color.white;
```
Unity 6 (`Texture2D.Fill`) and `System.Array.Fill` are both available and cleaner.
- **Recommendation:**
```csharp
// Option A (Unity 6+):
tex.Fill(Color.white);

// Option B:
var pixels = new Color[16];
System.Array.Fill(pixels, Color.white);
tex.SetPixels(pixels);
```

---

**[MODERATE] Defensive unsubscribe-before-subscribe is unnecessary — `round-manager-auto-play-restart-and-target.cs:38–39`**
```csharp
_cameraController.OnIntroComplete -= OnIntroDone;
_cameraController.OnIntroComplete += OnIntroDone;
```
`OnIntroDone` self-unsubscribes (`_cameraController.OnIntroComplete -= OnIntroDone;` at line 93). The defensive pattern here implies uncertainty about subscription state. The pattern works but the comment in HandleRestart should explain why it's needed (or it should be removed if not needed).
- **Recommendation:** Add a comment: `// Defensive: guard against restart during intro animation before OnIntroDone fires.` This case is real (user can click restart during intro), so the pattern IS necessary. Add the comment so future readers understand the intent.

---

## Positive Observations
- `RuntimeSpriteFactory` singleton with `ResetStaticState` on domain reload — correct Unity domain reload handling.
- `AudioManager` and `RoundManagerHUD` both use `ResetStaticState` pattern — no singleton leaks between play sessions.
- `ObstacleSpawner.CalculateTrajectory` properly clamps force to `GameConstants` range before solving angle — avoids degenerate physics cases.
- `RoundManager.OnDestroy` calls `StopAllCoroutines()` first — correct lifecycle cleanup.
- `OnValidate` guarded with `gameObject.scene.isLoaded` — avoids false warnings during scene setup tool.
- Partial class split of `RoundManager` is clean and well-documented.

---

## Recommended Actions (Priority Order)
1. **Fix NullRef in LaunchController:107** — Add `_roundManager?` null guard.
2. **Fix broken pitch in AudioManager:68–71** — Separate AudioSource for pitched target-hit sound.
3. **Decouple Rocket from RocketDebris** — Move `SpawnTargetDebris` call to `OnImpact` subscriber.
4. **Persist best score** — Add `PlayerPrefs` save/load to `GameRoundTracker`.
5. **Fix layer constant** — Replace `LayerMask.NameToLayer("Default")` with `const int DefaultLayer = 0` in `GameConstants`.
6. **Fix variable naming** — Rename `minDistSqr` in `ObstacleSpawner.cs:135`.
7. **Add sorting layer validation** — `OnValidate` check for "Gameplay" layer.
8. **Add comment to defensive unsubscribe** — Clarify intent in `HandleRestart`.

---

## Metrics
- **Files reviewed:** 12
- **LOC analyzed:** ~1,330
- **Critical findings:** 2
- **Important findings:** 5
- **Moderate findings:** 5

---

## Unresolved Questions
- Is the obstacle layer intended to be "Default" (layer 0) and does the Physics 2D collision matrix explicitly allow Rocket (layer 8) ↔ Default collisions? If not, obstacles silently don't collide with the rocket.
- Is the "Gameplay" sorting layer defined in Project Settings > Tags and Layers? If not, obstacle sprites render on Default layer silently.
