# Kiến trúc hệ thống & Giao tiếp giữa Scripts

## Tổng quan kiến trúc

```
┌──────────────────────────────────────────────────────┐
│              RoundManager (Partial Class)             │
│     Điều khiển luồng vòng chơi & tập hợp hệ thống   │
│     Singleton-like: single instance trên scene       │
└──────┬──────────────┬──────────────┬────────────────┬┘
       │ subscribes   │ subscribes   │ subscribes   │ controls
       ▼              ▼              ▼              ▼
  ┌─────────┐   ┌─────────┐   ┌──────────┐   ┌────────────┐
  │ Rocket  │   │ Camera  │   │ Audio    │   │ Obstacles  │
  │         │   │ Control │   │ Manager  │   │ Spawner    │
  │ Events  │   │ er      │   │ (Events) │   │            │
  └────┬────┘   └────┬────┘   └──────────┘   └────────────┘
       │             │
  Input: Launch   State: Following
  Events: On     / Returning
  Rocket,        / LookingAt
  OnImpact       Target
       │             │
       └─────────┬───┘
               ▼
        ┌─────────────────┐
        │ RoundManagerHUD │
        │  (Singleton)    │
        │ UI: Win, Hints, │
        │ Stats, Buttons  │
        └─────────────────┘
```

---

## Vai trò từng Script

### RoundManager (Core Orchestrator)

- **Vai trộ:** Điều khiển luồng vòng chơi, sự kiện chính, quản lý state
- **Pattern:** MonoBehaviour trên scene (NOT singleton)
- **Sở hữu:** GameRoundTracker, auto-play logic, target randomization
- **Lắng nghe:** `Rocket.OnRocketLanded`, `Rocket.OnTargetHit`, `CameraController.OnIntroComplete`
- **Điều khiển:**
  - `CameraController.SetState()` / `Shake()`
  - `AudioManager.PlayX()` / `StartThrust()` / `StopThrust()`
  - `RoundManagerHUD.ShowWinUI()` / `UpdateStatsUI()`
  - `ObstacleSpawner.RespawnObstacles()`
  - `LaunchController.EnableInput()` / `DisableInput()`

### LaunchController (Input Handler)

- **Vai trộ:** Đọc input slingshot, tính launch direction & force
- **Sở hữu:** Input detection, drag calculation, AimArrow visual
- **Tham chiếu:** `Rocket`, `AimArrow`, `_spawnPoint`, `RoundManager`
- **Giao tiếp:** Gọi `Rocket.Launch(direction, force)` + `RoundManager.OnShotFired()`
- **Cập nhật HUD:** Gọi `RoundManagerHUD.UpdateHintTexts(angle, force)`
- **Chú ý:** Chỉ xử lý input, KHÔNG xử lý game state

### Rocket (Physics & Events)

- **Vai trộ:** Vật lý tên lửa, rotation, collision detection
- **Sở hữu:** Rigidbody2D, collision/trigger callbacks, RocketTrail integration
- **Events fired:**
  - `OnRocketLaunched` — ngay sau impulse (camera bắt đầu follow)
  - `OnRocketLanded` — chạm Ground tag (miss)
  - `OnTargetHit` — vào Target trigger (win)
  - `OnImpact<Vector2, bool, float>` — position, isHit, maxHeight (effects)
- **KHÔNG biết về:** Camera, UI, input, audio
- **Integration:**
  - `RocketTrail.StartTrail()` / `StopTrail()` / `ClearTrail()`
  - `SetSpritesVisible()` để ẩn/hiện sau impact

### CameraController (State Machine)

- **Vai trộ:** Camera movement logic, state-based positioning
- **States:**
  ```
  Intro (pan Target → Vehicle)
    ↓ (OnIntroComplete)
  Idle (waiting, home position)
    ↓ (OnRocketLaunched)
  Following (smooth follow rocket + dynamic zoom)
    ↓ (OnRocketLanded / OnTargetHit)
  Returning (smooth return to vehicle)
    ↓ (arrived)
  Idle
  
  (From Idle):
  LookingAtTarget (pan to target, pause, pan back)
    ↓ (OnLookTargetComplete)
  Idle
  ```
- **Lắng nghe:** `Rocket.OnRocketLaunched`, `Rocket.OnRocketLanded`, `Rocket.OnTargetHit`
- **Tham chiếu:** `Rocket` (follow), `_vehicleTransform` (return), `_targetTransform` (look)
- **Công cụ:** `Vector2.SmoothDamp` cho smooth following, `Vector3.Lerp` cho intro/return pans
- **Events fired:** `OnIntroComplete`, `OnLookTargetComplete` → RoundManager lắng nghe
- **Integration:** `CameraScreenShake.Shake(duration, magnitude)` cho impact feedback

### RoundManagerHUD (UI Controller)

- **Vai trộ:** Quản lý toàn bộ UI, buttons, stats display
- **Pattern:** Singleton (RoundManager access mà không cần SerializeField)
- **Sở hữu:** Win text, buttons (restart, auto-play, look-target), hint labels, stats text
- **Gọi từ:** RoundManager (game events), LaunchController (hint updates)
- **Button actions:**
  - Restart → gọi `RoundManager.HandleRestart()`
  - AutoPlay → gọi `RoundManager.HandleAutoPlay()`
  - LookTarget → gọi `RoundManager.HandleLookTarget()`

### AudioManager (Audio Singleton)

- **Vai trộ:** Quản lý tất cả audio game (MP3 + procedural)
- **Pattern:** Singleton (trên scene riêng, được RoundManager gọi)
- **MP3 clips:** Launch, Thrust loop, Boom
- **Procedural clips:** Win jingle, target hit, click, stretch, ground hit
- **Gọi từ:** RoundManager (game events), LaunchController (UI feedback)
- **Methods:**
  - `PlayLaunch()` — bắt đầu launch
  - `StartThrust()` — loop thrust sound
  - `StopThrust()` — dừng thrust
  - `PlayHitTarget()` — trúng mục tiêu (chime)
  - `PlayHitGround()` — chạm mặt đất (boom)
  - `PlayWin()` — win jingle

### ImpactEffectsHandler (Effects Decoupler)

- **Vai trộ:** Subscribes `Rocket.OnImpact` → spawns effects
- **Implementation:** Gọi `ExplosionEffect.Spawn()`, `RocketDebris.Spawn()`, `GroundScorch.Spawn()`
- **Decoupling:** Rocket KHÔNG biết về effects, chỉ fire event

### GameRoundTracker (Stats Tracker)

- **Vai trộ:** Plain C# class tracking stats (không MonoBehaviour)
- **Sở hữu:** RoundManager giữ instance
- **Properties:** RoundShots, RoundNumber, BestScore
- **Gọi từ:** RoundManager (game flow), RoundManagerHUD (stats display)

### GameConstants (SSOT)

- **Vai trộ:** Single source of truth cho tất cả constants
- **Static access:** `GameConstants.MinLaunchForce`, `GameConstants.GroundTop`, etc.
- **Dùng bởi:** LaunchController, Rocket, RoundManager, GroundScorch, ObstacleSpawner

### ObstacleSpawner

- **Vai trộ:** Spawn & manage chướng ngại vật
- **Gọi từ:** RoundManager.HandleAutoPlay() để random spawn
- **Methods:** `RespawnObstacles()`, tính `SafeLaunchDirection`, `SafeLaunchForce`
- **Trajectory:** Quadratic discriminant method để check collision

---

## Luồng giao tiếp

### Launch Flow

```
Player drags on vehicle
    ↓
LaunchController.HandleTouchMoved()
    ├─ Calculate angle & force
    └─ AimArrow.UpdateArrow(direction, force)
    └─ RoundManagerHUD.UpdateHintTexts(angle, force)
    
Player releases touch
    ↓
LaunchController.HandleTouchEnded()
    ├─ Rocket.Launch(direction, force)
    │   ├─ Fire OnRocketLaunched
    │   ├─ RocketTrail.StartTrail()
    │   └─ Rigidbody becomes Dynamic
    │
    └─ RoundManager.OnShotFired()
        └─ GameRoundTracker.IncrementShots()
    
    └─ AudioManager.PlayLaunch()
    └─ AudioManager.StartThrust()
    
CameraController lắng nghe OnRocketLaunched
    └─ SetState(Following) + Start follow & zoom
```

### Hit/Miss Flow (Ground)

```
Rocket collides with Ground
    ↓
Rocket.OnCollisionEnter2D(Ground)
    ├─ Fire OnImpact(position, false, maxHeight)
    └─ Fire OnRocketLanded
    
ImpactEffectsHandler lắng nghe OnImpact
    ├─ ExplosionEffect.Spawn(position, isHit: false)
    ├─ RocketDebris.Spawn(position)
    └─ GroundScorch.Spawn(position, maxHeight)
    
CameraController lắng nghe OnRocketLanded
    └─ SetState(Returning) + Pan back to vehicle
    
RoundManager lắng nghe OnRocketLanded
    └─ HandleRocketMiss()
        ├─ AudioManager.StopThrust()
        ├─ AudioManager.PlayHitGround()
        ├─ CameraController.Shake()
        └─ Start DelayedAction(_reloadDelay, Reload)
            └─ Rocket.ResetToPosition()
            └─ RandomizeTarget()
            └─ RocketDebris.ClearAll()
            └─ GroundScorch.ClearAll()
            └─ LaunchController.EnableInput()
```

### Hit Target Flow

```
Rocket enters Target trigger
    ↓
Rocket.OnTriggerEnter2D(Target)
    ├─ Fire OnImpact(position, true, maxHeight)
    └─ Fire OnTargetHit
    
ImpactEffectsHandler lắng nghe OnImpact
    ├─ ExplosionEffect.Spawn(position, isHit: true)  [gold explosion]
    ├─ RocketDebris.Spawn(position)
    └─ GroundScorch.Spawn(position, maxHeight)
    
CameraController lắng nghe OnTargetHit
    └─ (optional state change)
    
RoundManager lắng nghe OnTargetHit
    └─ HandleTargetHit()
        ├─ AudioManager.StopThrust()
        ├─ AudioManager.PlayHitTarget()
        ├─ AudioManager.PlayWin()
        ├─ CameraController.Shake()
        ├─ GameRoundTracker.TryUpdateBest()
        └─ If NOT auto-playing:
            └─ Rocket.gameObject.SetActive(false)
            └─ RoundManagerHUD.ShowWinUI()
            └─ RoundManagerHUD.UpdateStatsUI()
            
        └─ If auto-playing:
            └─ Start DelayedAction(_reloadDelay, ReloadAfterAutoPlay)
```

### Auto-Play Demo Flow

```
User clicks AutoPlay button
    ↓
RoundManagerHUD.OnAutoPlayClicked()
    └─ RoundManager.HandleAutoPlay()
        ├─ Set _isAutoPlaying = true
        ├─ Disable input
        ├─ Start coroutine: repeat RandomLaunch()
            ├─ RandomLaunch()
            │   ├─ GetSafeDirection() từ ObstacleSpawner
            │   ├─ GetSafeForce()
            │   ├─ Rocket.Launch(safeDir, safeForce)
            │   └─ OnShotFired()
            │
            └─ Wait for OnTargetHit event
                └─ HandleTargetHit() [auto-play path]
                    └─ Start DelayedAction(_reloadDelay, ReloadAfterAutoPlay)
                        ├─ RocketDebris.ClearAll()
                        ├─ GroundScorch.ClearAll()
                        ├─ Rocket.ResetToPosition()
                        ├─ RandomizeTarget()
                        └─ Loop (đặt Rocket active lại, bắt đầu launch kế tiếp)
```

---

## Event System (C# Actions)

### Rocket Events (Broadcasting)

```csharp
Rocket định nghĩa:
  public event Action OnRocketLaunched;
  public event Action OnRocketLanded;
  public event Action OnTargetHit;
  public event Action<Vector2, bool, float> OnImpact;

Subscribers:
  CameraController       → OnRocketLaunched, OnRocketLanded, OnTargetHit
  RoundManager           → OnRocketLanded, OnTargetHit
  ImpactEffectsHandler   → OnImpact
  RocketTrail            → (internal, called by Rocket)
```

### CameraController Events

```csharp
CameraController định nghĩa:
  public event Action OnIntroComplete;
  public event Action OnLookTargetComplete;

Subscribers:
  RoundManager           → OnIntroComplete (để enable input)
                        → OnLookTargetComplete (optional state reset)
```

### Direct References (Inspector-assigned)

```
RoundManager (orchestrator)
  ├─ _rocket              → Rocket
  ├─ _spawnPoint          → Transform
  ├─ _cameraController    → CameraController
  ├─ _targetTransform     → Transform
  ├─ _obstacleSpawner     → ObstacleSpawner
  └─ _launchController    → LaunchController

LaunchController (input)
  ├─ _rocket              → Rocket
  ├─ _aimArrow            → AimArrow
  ├─ _spawnPoint          → Transform
  └─ _roundManager        → RoundManager

CameraController (camera)
  ├─ _rocket              → Rocket (follow target)
  ├─ _vehicleTransform    → Transform (return target)
  └─ _targetTransform     → Transform (look target)

RoundManagerHUD (UI singleton)
  └─ _roundManager        → RoundManager

AudioManager (audio singleton)
  └─ (no SerializeFields, plays via method calls)
```

### NO Cross-References (Decoupling)

- `Rocket` ↔ `CameraController` — communicate via events only
- `Rocket` ↔ `AudioManager` — communicate via RoundManager
- `Rocket` ↔ `ImpactEffectsHandler` — communicate via OnImpact event
- `AimArrow` ↔ `CameraController` — no relationship
- `LaunchController` ↔ `CameraController` — no direct reference (only via RoundManager)

---

## Physics Layers & Collisions

| Layer | Purpose | Collides With |
|---|---|---|
| Default (0) | Ground, environment, obstacles | Rocket |
| Rocket (8) | Rocket physics | Default (ground) + Target (trigger) |
| UI (5) | Canvas | (no physics) |

**Rocket Layer Setup:**
- CircleCollider2D: layer = Rocket (8)
- Rigidbody2D: Collision Detection = Continuous
- Ground tag: "Ground"
- Target tag: "Target" (with IsTrigger = true)

---

## Script Execution Order

| Priority | Script | Reason |
|---|---|---|
| -100 | RoundManager | Initialize early, set up references |
| 0 | LaunchController | Default — reads input in Update |
| 0 | Rocket | Default — physics in FixedUpdate |
| 0 | AimArrow | Default — visual update |
| 100 | CameraController | Execute after physics — smooth follow in LateUpdate |

---

## Assembly Definitions

```
RocketLauncher.Runtime.asmdef
  ├─ Rocket.cs
  ├─ RoundManager.cs
  ├─ LaunchController.cs
  ├─ CameraController.cs
  ├─ AudioManager.cs
  ├─ ObstacleSpawner.cs
  └─ ... (tất cả runtime scripts)

RocketLauncher.Editor.asmdef
  └─ Depends on RocketLauncher.Runtime
  ├─ rocket-launcher-scene-auto-setup-editor-tool.cs
  ├─ rocket-launcher-scene-setup-environment-and-gameplay-objects.cs
  └─ ... (editor tools)

RocketLauncher.Tests.Editor.asmdef
  └─ Depends on RocketLauncher.Runtime
  ├─ game-constants-validation-tests.cs
  ├─ game-round-tracker-tests.cs
  ├─ ground-scorch-tests.cs
  ├─ obstacle-spawner-trajectory-tests.cs
  └─ ... (unit tests)
```

---

## Startup Sequence

```
Scene Load
  ↓
Awake (all scripts)
  ├─ Rocket.Awake()      — cache Rigidbody2D, RocketTrail, SpriteRenderers
  ├─ RoundManager.Awake() — RandomizeTarget()
  ├─ CameraController.Awake() — cache Camera
  ├─ AudioManager.Awake()    — set Instance, create AudioSources
  └─ RoundManagerHUD.Awake() — set Instance, hide initial UI
  
Start (all scripts)
  ├─ RoundManager.Start()
  │   ├─ Subscribe to Rocket events
  │   └─ Subscribe to CameraController.OnIntroComplete
  │
  ├─ CameraController.Start()
  │   └─ Begin intro pan (Target → Vehicle)
  │
  └─ LaunchController.Start()
      └─ (waiting for LaunchController.EnableInput from RoundManager)
      
CameraController intro pan completes
  ↓
CameraController.OnIntroComplete fires
  ↓
RoundManager.OnIntroDone()
  └─ LaunchController.EnableInput()
  
Player can now aim & launch
```

---

## Key Design Patterns

### Event-Driven Communication
- Rocket fires events, RoundManager/CameraController subscribe
- Decouples physics from game logic & camera

### Singleton Pattern (Careful)
- `RoundManagerHUD`: Single-scene UI singleton
- `AudioManager`: Single-scene audio singleton
- `GameConstants`: Static utility class
- `RuntimeSpriteFactory`: Static factory

### State Machine
- `CameraController`: Explicit state enum + state-based Update logic

### Dependency Injection (Partial)
- RoundManager wires all references via Inspector (SerializeField)
- Avoids late-binding, enables easy scene debugging

### Decoupling via Effects Handler
- `ImpactEffectsHandler`: Subscribes Rocket.OnImpact → effects
- Rocket KHÔNG export effects dependency

---

## Summary

**Luồng chính:** LaunchController (input) → Rocket (physics + events) → RoundManager (logic) → Camera/Audio/UI/Effects

**KHÔNG có vòng lặp tròn:** Mỗi script có mục đích rõ ràng, không cross-reference, communicate via events hoặc direct method calls một chiều.

**Singleton cẩn thận:** Chỉ UI (HUD) và Audio sử dụng singleton (single-scene). Gameplay logic (RoundManager) là MonoBehaviour thường.
