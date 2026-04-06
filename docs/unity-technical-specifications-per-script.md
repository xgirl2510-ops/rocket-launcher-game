# Chi tiết kỹ thuật từng Script

## Tổng quan

Rocket Launcher sử dụng namespace `RocketLauncher` cho tất cả runtime scripts, `RocketLauncher.Editor` cho editor tools, `RocketLauncher.Tests` cho unit tests.

---

## Core Scripts

### 1. RoundManager.cs (Partial Class)

**Vai trò:** Điều khiển luồng vòng chơi: xử lý bắn/bỏ lỡ, tải lại, khởi động lại, random mục tiêu, auto-play.

**Serialized Fields**

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `_rocket` | Rocket | — | Tham chiếu tên lửa |
| `_spawnPoint` | Transform | — | Vị trí sinh tên lửa |
| `_cameraController` | CameraController | — | Điều khiển camera |
| `_targetTransform` | Transform | — | Transform mục tiêu |
| `_obstacleSpawner` | ObstacleSpawner | — | Spawner chướng ngại vật |
| `_launchController` | LaunchController | — | Điều khiển bắn |
| `_reloadDelay` | float | 1.5s | Thời gian chờ sau bắn |
| `_targetMinX` / `_targetMaxX` | float | 8 / 35 | Phạm vi X mục tiêu |
| `_targetMinY` / `_targetMaxY` | float | -4 / 10 | Phạm vi Y mục tiêu |

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `OnShotFired()` | `void` | Gọi từ LaunchController sau mỗi bắn |
| `RoundTracker` | Property | Getter trả về GameRoundTracker (stats) |

**Private Methods**

| Phương thức | Mô tả |
|---|---|
| `HandleTargetHit()` | Xử lý trúng mục tiêu: âm thanh, camera shake, HUD |
| `HandleRocketMiss()` | Xử lý bỏ lỡ: tải lại vòng chơi sau `_reloadDelay` |
| `HandleAutoPlay()` | Chế độ demo tự động: random bắn với lực an toàn |
| `HandleLookTarget()` | Quay camera để nhìn mục tiêu tiếp theo |
| `RandomizeTarget()` | Random vị trí mục tiêu trong phạm vi |
| `ReloadAfterAutoPlay()` | Reset sau auto-play |

**Events Subscribed To**

- `Rocket.OnRocketLanded` → `HandleRocketMiss()`
- `Rocket.OnTargetHit` → `HandleTargetHit()`
- `CameraController.OnIntroComplete` → `OnIntroDone()`

**Private Fields**

- `_missCount` (int): Số lần bỏ lỡ liên tiếp (để hiện gợi ý)
- `_isAutoPlaying` (bool): Đang ở chế độ demo?
- `_roundTracker` (GameRoundTracker): Plain C# stats tracker

---

### 2. RoundManagerHUD.cs

**Vai trò:** Quản lý UI toàn cảnh: win text, nút bấm, hint labels, thống kê.

**Pattern:** Singleton (RoundManager access mà không cần SerializeField tròn)

**Serialized Fields**

| Field | Kiểu | Mô tả |
|---|---|---|
| `_winText` | TextMeshProUGUI | "YOU WIN!" (ẩn ban đầu) |
| `_restartButton` | Button | Bấm để restart |
| `_autoPlayButton` | Button | Bấm để auto-play demo |
| `_lookTargetButton` | Button | Bấm để quay camera nhìn mục tiêu |
| `_angleText` | TextMeshProUGUI | Hiện góc aim hiện tại |
| `_forceText` | TextMeshProUGUI | Hiện lực bắn hiện tại |
| `_statsText` | TextMeshProUGUI | Hiện thống kê (shots, best score) |
| `_roundManager` | RoundManager | Tham chiếu RoundManager |

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `ShowWinUI()` | `void` | Bật win text và restart button |
| `HideWinUI()` | `void` | Tắt win UI |
| `ShowHints()` | `void` | Bật hint labels |
| `HideHints()` | `void` | Tắt hint labels |
| `UpdateHintTexts()` | `void OnAngleForceChanged(float angle, float force)` | Cập nhật angle/force text |
| `UpdateStatsUI()` | `void` | Cập nhật thống kê từ GameRoundTracker |

---

### 3. GameRoundTracker.cs

**Vai trò:** Plain C# class tracking thống kê vòng chơi (shots, best score, round number).

**Public Properties**

| Property | Kiểu | Mô tả |
|---|---|---|
| `RoundShots` | int | Số bắn trong vòng hiện tại |
| `RoundNumber` | int | Số vòng chơi |
| `BestScore` | int | Số bắn tối thiểu từng đạt được |

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `IncrementShots()` | `void` | Tăng counter bắn |
| `NewRound()` | `void` | Bắt đầu vòng mới (reset RoundShots) |
| `TryUpdateBest()` | `bool` | Cập nhật BestScore nếu RoundShots < best |
| `GetStatsText()` | `string` | Trả về text hiển thị stats |

---

### 4. GameConstants.cs

**Vai trò:** Static class — SSOT (Single Source of Truth) cho tất cả constants.

**Public Constants**

```csharp
public const float GroundTop = -5f;                        // Y position của mặt đất
public const string TagGround = "Ground";                  // Tag mặt đất
public const string TagTarget = "Target";                  // Tag mục tiêu
public const float MinLaunchForce = 5f;                    // Lực bắn tối thiểu
public const float MaxLaunchForce = 30f;                   // Lực bắn tối đa
public const float CraterSpawnHeightThreshold = 1.5f;      // Chiều cao để tạo crater
public const string GroundObjectName = "Ground";           // Tên GameObject mặt đất
```

---

### 5. RuntimeSpriteFactory.cs

**Vai trộ:** Static factory cho sprites/materials chia sẻ tại runtime.

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `GetSolidSprite()` | `Sprite` | Trả về sprite hình vuông 100x100 để vẽ chướng ngại vật |
| `GetParticleMaterial()` | `Material` | Trả về material particle (Sprites/Default shader) |

**RuntimeInitializeOnLoadMethod**
- Reset static cache trước mỗi domain reload (editor/play mode transitions)

---

## Launch Scripts

### 6. LaunchController.cs

**Vai trò:** Xử lý input slingshot: drag → aim → bắn.

**Serialized Fields**

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `_rocket` | Rocket | — | Tên lửa để bắn |
| `_aimArrow` | AimArrow | — | Visual aim |
| `_spawnPoint` | Transform | — | Vị trí spawn tên lửa (trên xe) |
| `_minDragDistance` | float | 0.5f | Min drag để register |
| `_maxDragDistance` | float | 3.0f | Max drag (clamped) |
| `_minLaunchForce` | float | GameConstants.MinLaunchForce | Lực ở min drag |
| `_maxLaunchForce` | float | GameConstants.MaxLaunchForce | Lực ở max drag |
| `_roundManager` | RoundManager | — | Để gọi OnShotFired() |

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `EnableInput()` | `void` | Cho phép bắt đầu aim |
| `DisableInput()` | `void` | Block input (trong flight/reset) |

**Private Methods**

- `Update()` – Đọc touch input mỗi frame
- `HandleTouchBegan()` – Kiểm tra touch gần vehicle, bắt đầu drag
- `HandleTouchMoved()` – Tính drag vector, cập nhật arrow
- `HandleTouchEnded()` – Tính final force & direction, gọi `_rocket.Launch()`
- `CalculateLaunchDirection()` – `(spawnPoint - touchPos).normalized`
- `CalculateLaunchForce()` – Map drag distance → force (lerp min↔max)

**Force Calculation Logic**

```csharp
Vector2 dragVector = (Vector2)_spawnPoint.position - touchWorldPos;
float dragDistance = Mathf.Clamp(dragVector.magnitude, 0f, _maxDragDistance);

if (dragDistance < _minDragDistance) return;

float t = (dragDistance - _minDragDistance) / (_maxDragDistance - _minDragDistance);
float force = Mathf.Lerp(_minLaunchForce, _maxLaunchForce, t);
Vector2 direction = dragVector.normalized;
```

---

### 7. AimArrow.cs

**Vai trò:** Visual-only aim indicator.

**Serialized Fields**

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `_spriteRenderer` | SpriteRenderer | — | Arrow sprite renderer |
| `_minScale` | float | 0.5f | Chiều dài mũi tên ở min drag |
| `_maxScale` | float | 2.0f | Chiều dài mũi tên ở max drag |
| `_color` | Color | White (alpha 0.7) | Màu mũi tên |

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `Show()` | `void` | Bật sprite renderer |
| `Hide()` | `void` | Tắt sprite renderer |
| `UpdateArrow()` | `void (Vector2 direction, float normalizedForce)` | Cập nhật rotation & scale |

---

## Rocket & Physics

### 8. Rocket.cs

**Vai trò:** Vật lý tên lửa: impulse launch, rotate to velocity, ground/target collision detection.

**Serialized Fields**

| Field | Kiểu | Mô tả |
|---|---|---|
| (Không có — dùng GameConstants và default configs) | — | — |

**Public Events**

| Event | Kiểu | Khi nào |
|---|---|---|
| `OnRocketLaunched` | `Action` | Ngay sau khi apply force |
| `OnRocketLanded` | `Action` | Khi chạm mặt đất |
| `OnTargetHit` | `Action` | Khi vào target trigger |
| `OnImpact` | `Action<Vector2, bool, float>` | Khi impact (position, isHit, maxHeight) |

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `Launch()` | `void (Vector2 direction, float force)` | Apply impulse, set flying, fire OnRocketLaunched |
| `ResetToPosition()` | `void (Vector2 position)` | Reset về spawn, zero velocity, kinematic |
| `IsFlying` | Property | Trả về có đang bay không? |

**Private Methods**

- `FixedUpdate()` – Rotate sprite to face velocity, track _maxHeight
- `RotateToVelocity()` – `angle = Atan2(vel.y, vel.x) * Rad2Deg - 90f`
- `OnCollisionEnter2D()` – Nếu chạm Ground: stop, fire OnImpact, OnRocketLanded
- `OnTriggerEnter2D()` – Nếu vào Target: fire OnImpact, OnTargetHit
- `SetSpritesVisible()` – Ẩn/hiện sprite khi reset

**Rigidbody2D Setup**

| Component | Property | Giá trị |
|---|---|---|
| Rigidbody2D | Body Type | Kinematic (ban đầu) → Dynamic (launch) |
| Rigidbody2D | Gravity Scale | 1 |
| Rigidbody2D | Collision Detection | Continuous |
| CircleCollider2D | Radius | 0.15 |
| CircleCollider2D | Offset | (0, 0.5) — ở mũi tên |

---

## Camera Scripts

### 9. CameraController.cs

**Vai trò:** State machine camera: intro pan (Target → Vehicle), follow rocket, return to vehicle, dynamic zoom.

**Camera States**

```csharp
public enum CameraState { Intro, Idle, Following, Returning, LookingAtTarget }
```

**Serialized Fields**

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `_rocket` | Rocket | — | Follow target |
| `_vehicleTransform` | Transform | — | Return target |
| `_targetTransform` | Transform | — | Look target (LookingAtTarget state) |
| `_introPauseDuration` | float | 1.0s | Pause trước intro pan |
| `_introPanDuration` | float | 1.5s | Thời gian pan từ target tới vehicle |
| `_homeY` | float | 2f | Y position khi quay về home |
| `_followSmoothTime` | float | 0.12f | SmoothDamp time khi follow |
| `_followOffsetY` | float | 2f | Look-ahead Y offset trên rocket |
| `_maxOrthoSize` | float | 25f | Max ortho size khi zoom out |
| `_zoomOutSpeed` | float | 5f | Tốc độ zoom out dựa khoảng cách |
| `_zoomMaxDistance` | float | 40f | Khoảng cách để reach max ortho |
| `_returnDuration` | float | 1.0s | Thời gian quay về vehicle |
| `_lookTargetPanDuration` | float | 1.0s | Thời gian pan tới mục tiêu |
| `_lookTargetPauseDuration` | float | 2.0s | Pause khi nhìn mục tiêu |

**Public Events**

| Event | Kiểu | Khi nào |
|---|---|---|
| `OnIntroComplete` | `Action` | Intro pan kết thúc |
| `OnLookTargetComplete` | `Action` | Quay lại từ mục tiêu |

**Public Methods**

- `SetState()` – Chuyển state
- `PanToTarget()` – Pan camera tới mục tiêu
- `FollowRocket()` – Bắt đầu follow rocket
- `ReturnToVehicle()` – Quay về vehicle

**State Transitions**

```
Intro → Idle (waiting for input)
     → Following (OnRocketLaunched)
     → Returning (OnRocketLanded)
     → LookingAtTarget (nút look target)
     → Returning → Idle
```

---

### 10. CameraScreenShake.cs

**Vai trò:** Decoupled screen shake effect component.

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `Shake()` | `void (float duration, float magnitude)` | Shake camera trong duration |
| `GetOffset()` | `Vector3` | Trả về offset hiện tại để apply |

**Implementation**
- Decaying random offset được apply trong LateUpdate
- Gọi từ RoundManager.HandleTargetHit()

---

## Effects Scripts

### 11. ExplosionEffect.cs

**Vai trò:** Static spawner cho burst explosion visuals.

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `Spawn()` | `static void (Vector2 position, bool isHit)` | Spawn explosion (gold cho hit, grey cho miss) |

**Implementation**
- Instantiate particle prefab
- Auto-destroy sau lifetime kết thúc

---

### 12. GroundScorch.cs

**Vai trộ:** Static class quản lý crater/scorch marks sử dụng SpriteMask.

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `Spawn()` | `static void (Vector2 impactPosition, float maxHeight)` | Spawn crater tại vị trí |
| `GetGroundY()` | `static float (float x)` | Trả về ground Y tại X position |
| `ClearAll()` | `static void()` | Xóa tất cả craters (reset scene) |

**Implementation**
- 8 jagged SpriteMask prefabs
- Depth scales với impact height
- Gọi từ ImpactEffectsHandler

---

### 13. ImpactEffectsHandler.cs

**Vai trộ:** Decoupler subscribes Rocket.OnImpact → spawns explosion + debris + crater.

**Public Methods**
- `OnImpact()` – Subscribed to Rocket.OnImpact event

**Implementation**
- Calls ExplosionEffect.Spawn()
- Calls RocketDebris.Spawn()
- Calls GroundScorch.Spawn()

---

### 14. RocketDebris.cs

**Vai trò:** Static debris spawner với gravity-aware ground detection.

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `Spawn()` | `static void (Vector2 impactPosition)` | Spawn debris shards |
| `SpawnDirtDebris()` | `static void (Vector2 position, int count)` | Spawn dirt particles |
| `SpawnTargetDebris()` | `static void (Vector2 position, int count)` | Spawn target pieces |
| `ClearAll()` | `static void()` | Xóa tất cả debris (reset scene) |

**Implementation**
- Manual FixedUpdate gravity (không dùng Rigidbody gravity để tối ưu)
- Uses GroundScorch.GetGroundY() để tính ground position

---

### 15. RocketTrail.cs

**Vai trộ:** Particle trail manager.

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `StartTrail()` | `void` | Bắt đầu emit particles |
| `StopTrail()` | `void` | Dừng emit |
| `ClearTrail()` | `void` | Xóa particles còn lại |

**Serialized Fields**

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|
| `_emissionRate` | float | 40 | Particles/second |
| `_particleLifetime` | float | 0.4s | Lifetime của mỗi particle |

**Color Gradient**
- Red → Orange → Grey (theo particle age)

---

## Audio Scripts

### 16. AudioManager.cs

**Vai trò:** Singleton quản lý toàn bộ audio game.

**Pattern:** Singleton (RoundManager calls without direct refs)

**Serialized Fields**

| Field | Kiểu | Mô tả |
|---|---|---|
| `_launchClip` | AudioClip | MP3 launch sound |
| `_thrustClip` | AudioClip | MP3 flight thrust loop |
| `_boomClip` | AudioClip | MP3 ground impact boom |

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `PlayLaunch()` | `void` | Play launch sound |
| `PlayHitGround()` | `void` | Play ground impact sound |
| `PlayHitTarget()` | `void` | Play target hit sound (procedural) |
| `PlayStretch()` | `void` | Play stretch UI feedback (procedural) |
| `PlayClick()` | `void` | Play button click (procedural) |
| `PlayWin()` | `void` | Play win jingle (procedural) |
| `StartThrust()` | `void` | Loop thrust sound |
| `StopThrust()` | `void` | Stop thrust loop |

**Private Implementation**
- `_oneShotSource`: Plays launch/boom clips
- `_thrustSource`: Loops thrust audio
- Procedural clips sinh tạo on-demand từ ProceduralAudioClipGenerator

---

### 17. ProceduralAudioClipGenerator.cs

**Vai trò:** Static generator cho procedural audio clips.

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `CreateGroundHit()` | `static AudioClip` | Thump-like boom sound |
| `CreateWinJingle()` | `static AudioClip` | Ascending tone melody |
| `CreateStretch()` | `static AudioClip` | Sweep sound cho UI feedback |
| `CreateTargetHit()` | `static AudioClip` | Chime-like hit sound |
| `CreateClick()` | `static AudioClip` | Pop click cho buttons |

**Audio Specs**
- 44100Hz sample rate
- Mono format
- Procedural synthesis (không dùng file)

---

## Obstacles Scripts

### 18. ObstacleSpawner.cs

**Vai trộ:** Spawn và manage chướng ngại vật.

**Serialized Fields**

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `_obstacleCount` | int | 6 | Số chướng ngại vật |
| `_safeRadius` | float | 1.5f | Radius nơi không có chướng ngại vật |
| `_obstacleGridCellSize` | float | 2.0f | Cell size cho grid spawning |

**Public Methods**

| Phương thức | Chữ ký | Mô tả |
|---|---|---|
| `RespawnObstacles()` | `void` | Xóa cũ, spawn obstacles mới |
| `SafeLaunchDirection` | Property | Direction không đi qua chướng ngại |
| `SafeLaunchForce` | Property | Force để vượt qua chướng ngại |

**Trajectory Calculation**
- Quadratic discriminant method để check intersection
- Tính safe direction nếu launch bị block

---

## Summary

**Tổng cộng:** 18 scripts trong namespace RocketLauncher
- **Core:** 5 scripts (RoundManager, RoundManagerHUD, GameRoundTracker, GameConstants, RuntimeSpriteFactory)
- **Launch:** 2 scripts (LaunchController, AimArrow)
- **Physics:** 1 script (Rocket)
- **Camera:** 2 scripts (CameraController, CameraScreenShake)
- **Effects:** 5 scripts (ExplosionEffect, GroundScorch, ImpactEffectsHandler, RocketDebris, RocketTrail)
- **Audio:** 2 scripts (AudioManager, ProceduralAudioClipGenerator)
- **Obstacles:** 1 script (ObstacleSpawner)
