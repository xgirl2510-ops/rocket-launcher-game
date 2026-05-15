# Logic Correctness Audit — 2026-05-15

## Scope
- Files reviewed: all 33 C# files under `Assets/Scripts/` (Ads, Audio, Camera, Core, Effects, Launch, Obstacles, Rocket)
- Lines of code analyzed: ~2,700
- Review focus: event subscribe/unsubscribe, state machine correctness, null-guards, trajectory/physics constant consistency, friendly-fire logic, coroutine safety, race conditions, tag check safety

---

## Critical Bugs

### C1 — `InterceptorMissile.OnTriggerEnter2D` uses raw `CompareTag("Player")` — tag not registered
**File:** `Assets/Scripts/Obstacles/jet-interceptor-missile-defense-projectile.cs:317`

```csharp
if (!other.CompareTag("Player")) return;
```

`TagManager.asset` contains only: `Ground`, `Target`, `LauncherVehicle`. `"Player"` is absent.  
`CompareTag` on an unregistered tag throws `UnityException: Tag is not defined` **at runtime**, causing the entire `OnTriggerEnter2D` callback to crash unhandled. Result: the missile's trigger-based kill path is **completely non-functional** — the missile will fly through the rocket on trigger overlap without detonating it.

The proximity-based kill in `ChaseStep()` (the `dist < KillRadius` path) still works via Update, so the missile can still kill, but only at very close range; the broader trigger-collider detection never fires.

**Fix:** Add `"Player"` to TagManager, or switch to `other.GetComponent<Rocket>() != null` (no tag required), consistent with how other Rocket checks are done in the codebase. Also apply `SafeCompareTag` pattern or a try-catch wrapper.

---

### C2 — `ImpactEffectsHandler` subscribes in `OnEnable` but `_rocket` is assigned via `[SerializeField]`, not yet validated at enable time
**File:** `Assets/Scripts/Effects/impact-effects-handler.cs:33-36`

```csharp
private void OnEnable()
{
    if (_rocket != null)
        _rocket.OnImpact += HandleImpact;
}
```

`OnEnable` fires before `Start`. If `_rocket` is null at `OnEnable` (mis-wired scene), no subscription happens — but more critically, if the component is **disabled and re-enabled** mid-game (e.g., via WorldPauseController or an editor tool toggle), it will double-subscribe on every re-enable after `Start` has confirmed a non-null `_rocket`. Each re-enable adds a new delegate, triggering `HandleImpact` twice per impact.

**Fix:** Subscribe once in `Start()` (consistent with how `CameraController` and `RoundManager` subscribe); remove the `OnEnable`/`OnDisable` subscribe pattern OR add a guard bool `_subscribed`.

---

## Likely Bugs

### L1 — `RocketTrajectoryPredictor` physics constants are hardcoded, diverge from live rocket when SerializedFields change
**File:** `Assets/Scripts/Obstacles/rocket-trajectory-first-hit-predictor-singleton.cs:199-202`

```csharp
const float thrustDuration = 0.6f;
const float thrustForce = 12f;
const float drag = 0.4f;
```

`RocketDiveSolver` correctly reads `_rocket.GetFlightParams()` to stay in sync with the live serialized values. The predictor uses **hardcoded** copies. If a designer tweaks `_thrustDuration`, `_thrustForce`, or `_airDrag` in the Inspector, the predictor's arc prediction diverges from the rocket's actual flight, leading it to flag the wrong jet or miss entirely.

Note: `g` is also hardcoded as `9.81f` while `GetFlightParams()` uses `Physics2D.gravity.magnitude` — these are currently the same in default Unity, but only by convention.

**Fix:** `OnRocketLaunched` already receives `launchDirection` and `launchForce`; inject `RocketFlightParams` as a 4th parameter from LaunchController, or have the Predictor call `rocket.GetFlightParams()`.

---

### L2 — `CameraController.SetState(Following)` stops the active intro/return coroutine and resets `_smoothVelocity` but does NOT reset ortho size
**File:** `Assets/Scripts/Camera/CameraController.cs:308-313`

```csharp
private void SetState(CameraState newState)
{
    StopActiveCoroutine();
    _currentState = newState;
    _smoothVelocity = Vector2.zero;
}
```

If the rocket launches while `ReturnToVehicleCoroutine` is mid-lerp (user fires immediately after landing on same frame re-enable), `StopActiveCoroutine` cuts the coroutine before `_camera.orthographicSize = orthoTo` (the terminal assignment in `PanCoroutine`). The camera is left at an intermediate zoom level. Subsequent `FollowRocket()` starts from that intermediate size, causing a visible jump in `MoveTowards` zoom on the first follow frame.

Reproducible path: land rocket → re-enable input fires immediately → player drags quickly → second rocket launches before `ReturnToVehicleCoroutine` completes 1-second return. Low probability on manual play, but nearly certain on fast auto-play sequences.

**Fix:** In `SetState`, snap `_camera.orthographicSize = _defaultOrthoSize` if transitioning to `Following` from `Returning`.

---

### L3 — `HandleLauncherVehicleHit` disables rocket GO **before** 0.4s `DelayedAction(Freeze)` — debris spawned by `OnImpact` has Rigidbodies that get frozen but were spawned AFTER rocket was silenced
**File:** `Assets/Scripts/Core/RoundManager.cs:144` and `152`

```csharp
_rocket.gameObject.SetActive(false);   // line 144
StartCoroutine(DelayedAction(0.4f, WorldPauseController.Freeze));  // line 152
```

`OnImpact` fires (via `Rocket.HandleLauncherVehicleTrigger`) before `HandleLauncherVehicleHit` is called. `ImpactEffectsHandler.HandleImpact` runs synchronously within the same event dispatch, spawning debris `RocketDebris` GameObjects with their own `Update`/`FixedUpdate` movement. Freeze runs 0.4s later, setting `rb.simulated = false` on all `Rigidbody2D` — BUT `RocketDebris` uses **manual movement** (no Rigidbody), so it continues moving after freeze. The visual intention is that debris plays while the world freezes — this is fine. However, if `RocketDebris._allDebris` GameObjects are destroyed via `ClearAll()` during `HandleRestart()` (called via button click while world is frozen), there is no race because button clicks only process during `Update`, and `Update` still runs since timeScale is untouched. Low severity but worth noting.

---

### L4 — `RoundManager.HandleAutoPlay` calls `_rocket.Launch()` (not `LaunchBallistic`) but comment on `HandleAutoPlay` says it uses "player physics"
**File:** `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs:123`

```csharp
_rocket.Launch(dir, force);   // applies thrust + drag
```

The comment (line 120-122) correctly explains this is intentional — the dive solver used `GetFlightParams()` which includes thrust/drag. The **trajectory predictor** (`RocketTrajectoryPredictor`) however uses hardcoded constants (see L1 above) when `OnRocketLaunched` is called from auto-play (line 124). So auto-play trajectory prediction is doubly inconsistent: predictor hardcodes while solver reads live params.

---

## Logic Smells

### S1 — Friendly-fire upward-velocity guard uses `>= 0f` (includes exactly zero)
**File:** `Assets/Scripts/Rocket/Rocket.cs:253`

```csharp
if (_rb.linearVelocity.y >= 0f) return;
```

At the exact apex of the trajectory, `vy == 0`. This guard correctly passes (returns early → no friendly fire). However, a rocket launched **perfectly horizontal** (vy always 0 unless falling) that lands back on the vehicle would also return early and never trigger game-over, because horizontal vy never goes below 0 during flight — only after it starts falling. This is geometrically correct behavior (a horizontal shot would have to travel left and fall back), but the comment says "falling back down" — `< 0` would be more precisely expressive of that intent. Current behavior is functionally correct in practice.

---

### S2 — `RoundManager.OnDestroy` unsubscribes `OnLookTargetComplete` defensively, but a second `PrepareNewRound` call in the same frame could add a duplicate `OnIntroComplete` subscription
**File:** `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs:89-91`

```csharp
_cameraController.OnIntroComplete -= OnIntroDone;
_cameraController.OnIntroComplete += OnIntroDone;
_cameraController.PlayIntro();
```

Remove-before-add pattern correctly prevents double-subscribe. This is fine. However, `OnLookTargetComplete` in `HandleLookTarget` uses the same pattern (line 141-142) but is NOT remove-before-added again in `PrepareNewRound` — meaning if `HandleLookTarget` fires and the user immediately clicks Restart before `OnLookTargetDone` fires, the subscription stays live through `HandleRestart` → `PrepareNewRound` → `PlayIntro`. The `OnLookTargetComplete` handler will fire spuriously on the next look-target sequence (double `EnableInput` call). Low impact since `EnableInput` is idempotent, but is a subscription leak until `OnDestroy`.

---

### S3 — `RocketTrajectoryPredictor._pendingVictim` is nullable reference checked in `Update()` via `_playerRocket != null && !_playerRocket.IsFlying`, but `_playerRocket` is found lazily via `FindAnyObjectByType`
**File:** `Assets/Scripts/Obstacles/rocket-trajectory-first-hit-predictor-singleton.cs:108`

```csharp
if (_playerRocket == null) _playerRocket = Object.FindAnyObjectByType<Rocket>();
if (_playerRocket != null && !_playerRocket.IsFlying)
{
    ClearPending();
    return;
}
```

If the rocket GO is destroyed mid-flight (e.g., someone calls `Destroy(rocketGO)` instead of `SetActive(false)`, which doesn't happen currently but is one mistake away), `_playerRocket` becomes a destroyed-but-non-null C# reference. `_playerRocket.IsFlying` would then throw `MissingReferenceException`. The existing codebase always uses `SetActive(false)` not `Destroy` for the rocket, so this is not currently broken, but it is fragile.

---

### S4 — `WorldPauseController.Freeze` iterates ALL Rigidbody2D in the scene, including the interceptor missile Rigidbody
**File:** `Assets/Scripts/Core/world-pause-controller-freeze-physics-particles-audio-without-timescale.cs:36-38`

The interceptor missile (`InterceptorMissile`) uses a Kinematic Rigidbody2D (gravity 0). `Freeze` sets `simulated=false` on ALL rigidbodies — but `InterceptorMissile` moves via `transform.position +=` in `Update`, NOT via Rigidbody forces. Setting `simulated=false` on a kinematic-body that moves via transform does nothing to stop it. The missile therefore continues flying **after freeze** while all other physics stops.

In practice, `HandleLauncherVehicleHit` means the rocket is already dead — no active missiles. The missile's `_target` check at the top of `Update` (`!_target.gameObject.activeInHierarchy`) will catch the deactivated rocket GO and `Destroy(gameObject)` the missile quickly. So this is harmless in practice, but the comment in `WorldPauseController` says "stops physics" which is slightly misleading for kinematic transform-movers.

---

### S5 — `RocketDiveSolver.SimulateAndScore` applies thrust in direction of initial impulse (`dir`), not `vel.normalized`
**File:** `Assets/Scripts/Obstacles/rocket-dive-trajectory-solver-brute-force.cs:147`

```csharp
if (t < p.ThrustDuration)
    vel += dir * p.ThrustForce * SimDt;
```

The live `Rocket.FixedUpdate` applies thrust in `_thrustDirection` which is set to `direction.normalized` at launch — so both solver and rocket use the initial launch direction for thrust, not the current velocity direction. This is consistent. However, `FindFirstJetInRange` (the predictor) does the same correctly (line 225: `thrustDir = vel.normalized` at step 0 = launch direction). No divergence, but worth noting the three implementations are independent.

---

### S6 — `GroundScorch._groundPrepared` is a static bool, but `ClearAll()` resets it to `false`
**File:** `Assets/Scripts/Effects/ground-scorch-mark.cs:301`

```csharp
_groundPrepared = false;
```

On `ClearAll()` at round restart, `PrepareGround` will be called again on the first crater spawn of the new round — this sets `sr.maskInteraction = VisibleOutsideMask` on the ground sprite again. That's idempotent and harmless, but creates one unnecessary `GetComponent<SpriteRenderer>` call per round. Not a bug, just noise.

---

### S7 — `AdManager.OnAdClosed` event is fired by `HandleAdFailed` (ad display failure) — `OnAdClosedRestart` in RoundManager will start a new round even if the ad never appeared
**File:** `Assets/Scripts/Ads/AdManager.cs:126`

```csharp
OnAdClosed?.Invoke();  // inside HandleAdFailed
```

This is intentional per the comment ("treat as closed so game continues"), but if `HandleAdFailed` fires BEFORE `ShowInterstitialIfReady()` returns (impossible in current flow since Show() blocks until next callback), or fires spuriously, `StartNewRound()` could be called twice. The `-=` before `+=` in `HandleRestart` prevents double subscription for a single restart, so the actual risk is theoretical here. Acceptable.

---

## Summary Table

| ID | Severity | File | Line | Impact |
|----|----------|------|------|--------|
| C1 | Critical | `jet-interceptor-missile-defense-projectile.cs` | 317 | Trigger kill path broken — `UnityException` on every trigger overlap |
| C2 | Critical | `impact-effects-handler.cs` | 33-36 | Double-subscribe on `OnImpact` if component toggled at runtime |
| L1 | High | `rocket-trajectory-first-hit-predictor-singleton.cs` | 199-202 | Predictor arc diverges from rocket if thrust/drag tweaked in Inspector |
| L2 | Medium | `CameraController.cs` | 308-313 | Stale ortho zoom after rocket-launch-during-return coroutine |
| L3 | Low | `RoundManager.cs` | 144, 152 | Debris continues after freeze (cosmetic, correct by design) |
| L4 | Low | `round-manager-auto-play-restart-and-target.cs` | 123-124 | Auto-play predictor still uses hardcoded constants (same root as L1) |
| S1-S7 | Smell | various | — | Fragile but not currently broken |

---

## Recommended Actions (Priority Order)

1. **Register `"Player"` tag in TagManager** (or replace `CompareTag("Player")` with `GetComponent<Rocket>() != null` in `InterceptorMissile.OnTriggerEnter2D`) — **blocks the trigger kill path today**.
2. **Move `ImpactEffectsHandler` subscription to `Start()` / `OnDestroy()`** — eliminate potential double-subscribe.
3. **Inject `RocketFlightParams` into `RocketTrajectoryPredictor.OnRocketLaunched`** — keep predictor in sync with rocket SerializedField tuning.
4. **Snap ortho size in `CameraController.SetState(Following)`** — cosmetic zoom glitch on rapid re-launch.

---

*No plan file was specified; no plan update performed.*
