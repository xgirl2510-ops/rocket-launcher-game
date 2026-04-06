# Code Review: Physics, Input & Camera Scripts

**Date:** 2026-04-06  
**Reviewer:** code-reviewer  
**Scope:** Rocket.cs, LaunchController.cs, AimArrow.cs, CameraController.cs, camera-screen-shake.cs, ObstacleSpawner.cs

---

## Code Review Summary

### Scope
- Files reviewed: 6
- Lines of code analyzed: ~570
- Review focus: Physics correctness, input edge cases, camera state machine, trajectory math, memory cleanup

### Overall Assessment

Code nhìn chung **sạch, rõ ràng, và có cấu trúc tốt**. Đã qua nhiều vòng review trước nên phần lớn vấn đề lớn đã được xử lý. Vẫn còn một số bug tiềm ẩn và điểm cải thiện đáng chú ý.

**Điểm tổng: 8.5 / 10**

---

## Critical Issues

Không có lỗi critical.

---

## High Priority Findings

### 1. `ObstacleSpawner` — Tag `Ground` gắn lên obstacle là sai về mặt gameplay logic (Medium-High)

**File:** `ObstacleSpawner.cs` dòng 169

```csharp
go.tag = GameConstants.TagGround;
```

Obstacle được tag là `"Ground"`. Điều này khiến `Rocket.OnCollisionEnter2D` xử lý va chạm với obstacle y hệt như đất (miss, không phải hit target). Đây có thể là chủ ý (obstacle = barrier gây miss), nhưng không có comment giải thích. Nếu muốn obstacle có âm thanh/effect riêng thì cần tag riêng.

**Khuyến nghị:** Thêm comment rõ ràng, hoặc tạo `TagObstacle` trong `GameConstants` nếu cần phân biệt.

---

### 2. `ObstacleSpawner` — Layer `Default` thay vì layer `"Ground"` hay layer chuyên dụng

**File:** `ObstacleSpawner.cs` dòng 170

```csharp
go.layer = LayerMask.NameToLayer("Default");
```

Nếu dùng layer-based collision matrix (rất phổ biến trong Unity), `Default` layer có thể gây collision không mong muốn với các object khác. Nên dùng layer nhất quán với các static ground objects trong scene.

---

### 3. `CameraController` — `PanCoroutine` dùng `Time.deltaTime` không nhất quán với `LateUpdate`

**File:** `CameraController.cs` dòng 233

```csharp
elapsed += Time.deltaTime;
```

`PanCoroutine` chạy trong coroutine (Update cycle), nhưng `FollowRocket()` chạy trong `LateUpdate`. Khi coroutine đang chạy và rocket vừa launched, cả hai có thể ghi đè transform trong cùng một frame (dù `SetState(Following)` gọi `StopActiveCoroutine` nên thực tế không xảy ra). Tuy nhiên nếu ai đó call `PlayIntro()` trong khi rocket đang bay (`Following` state) thì coroutine sẽ chiến đấu với `LateUpdate`. `PlayIntro()` gọi `StopActiveCoroutine` nhưng không reset state → `LateUpdate` vẫn chạy `FollowRocket()` cho đến khi `IntroCoroutine` set `_currentState = Intro`.

**Khuyến nghị:** Trong `PlayIntro()`, set `_currentState = CameraState.Intro` **trước** khi StartCoroutine để block `FollowRocket` ngay lập tức.

```csharp
public void PlayIntro()
{
    StopActiveCoroutine();
    _currentState = CameraState.Intro; // ← thêm dòng này trước StartCoroutine
    _activeCoroutine = StartCoroutine(IntroCoroutine());
}
```

---

### 4. `LaunchController` — Input mất nếu mouse rời khỏi window khi đang drag

**File:** `LaunchController.cs` dòng 56–58

```csharp
else if (Input.GetMouseButtonUp(0) && _isDragging)
    HandleTouchEnded();
```

Nếu user nhấn chuột, kéo ra ngoài window game, thả chuột bên ngoài → `GetMouseButtonUp` không được nhận → `_isDragging` bị kẹt `true`. Lần click tiếp theo vào vehicle sẽ không hoạt động vì `GetMouseButtonDown` gọi `HandleTouchBegan` set `_isDragging = true` nhưng không check state cũ.

Thực ra `HandleTouchBegan` đơn giản chỉ set `_isDragging = true` nên sẽ tự reset → không gây lỗi. Nhưng nếu `_isDragging = true` khi input disabled và re-enabled, drag state cũ còn tồn tại. `EnableInput()` không reset `_isDragging`.

**Khuyến nghị:** Reset `_isDragging = false` trong `EnableInput()`.

```csharp
public void EnableInput()
{
    _inputEnabled = true;
    _isDragging = false; // ← clear stale drag state
}
```

---

## Medium Priority Improvements

### 5. `ObstacleSpawner` — Trajectory math: `timeOfFlight` dùng `start.y - GameConstants.GroundTop` có thể âm

**File:** `ObstacleSpawner.cs` dòng 99

```csharp
float timeOfFlight = (vy + Mathf.Sqrt(vy * vy + 2f * g * Mathf.Max(0f, start.y - GameConstants.GroundTop))) / g;
```

Công thức này tính thời gian rơi xuống `GroundTop`. Nếu `start.y < GameConstants.GroundTop` (spawn point dưới đất), `Mathf.Max(0f, ...)` bảo vệ đúng. Tuy nhiên `totalTime` sau đó là `Mathf.Min(...)` của hai giá trị, nếu `vx` gần 0 thì `Mathf.Abs(dx) / Mathf.Max(Mathf.Abs(vx), 0.1f)` có thể rất lớn → fallback về `timeOfFlight * 1.2f` đúng.

Không có bug nhưng logic hơi phức tạp. Thêm comment giải thích từng nhánh.

---

### 6. `Rocket` — Không handle collision với obstacle trên không

**File:** `Rocket.cs` dòng 94–108

`OnCollisionEnter2D` chỉ check `CompareTag(GroundTag)`. Nếu obstacle dùng tag khác thì rocket xuyên qua (không kích hoạt effect). Vì hiện tại obstacle dùng tag `Ground`, vấn đề này không xảy ra — nhưng dễ bị break nếu ai đổi tag obstacle.

---

### 7. `CameraScreenShake` — Update chạy mọi frame dù không shake

**File:** `camera-screen-shake.cs` dòng 25–34

`Update()` chạy mọi frame kể cả khi không shake (`_elapsed >= _duration`). Chi phí thấp nhưng không cần thiết. Có thể disable component khi không dùng, hoặc dùng early return sau khi set offset về zero (đã có, nhưng Update vẫn invoke).

Minor: OK với game nhỏ này.

---

### 8. `AimArrow` — Không validate `_minScale < _maxScale`

**File:** `AimArrow.cs` dòng 13–14

Nếu designer set `_minScale > _maxScale` trong Inspector, `Mathf.Lerp` sẽ vẫn hoạt động nhưng arrow scale sẽ giảm khi kéo mạnh hơn — confusing. Thêm `OnValidate` guard.

---

## Low Priority Suggestions

### 9. `LaunchController` — `_camera = Camera.main` trong Update mỗi frame khi null

**File:** `LaunchController.cs` dòng 50

```csharp
if (_camera == null) _camera = Camera.main;
```

`Camera.main` dùng `FindGameObjectWithTag` nội bộ — O(n) lookup. Nên assign một lần trong `Start()` thay vì check mỗi frame. Đã có trong `Awake()` nhưng guard trong Update có thể hide lỗi thiếu camera.

---

### 10. `ObstacleSpawner` — `_safeTrajectory` là public field được cache giữa các round

`_safeTrajectory` được set trong `RespawnObstacles()`. Nếu `IsInSafeZone` được gọi trước `RespawnObstacles()`, nó trả `false` (đúng). Không có bug, chỉ cần chú ý thứ tự gọi.

---

## Positive Observations

- **Rocket.cs**: Kinematic/Dynamic switch pattern rất clean. `MoveRotation` trong `FixedUpdate` đúng chuẩn Unity physics. Event-driven design tốt.
- **LaunchController.cs**: SRP tốt — chỉ xử lý input, không biết gì về game flow. `TryComputeDrag` là pattern rõ ràng.
- **CameraController.cs**: `_activeCoroutine` guard ngăn race condition coroutine — thiết kế đúng. `PanCoroutine` DRY helper tốt. `OnDestroy` unsubscribe event — đúng.
- **CameraScreenShake**: Tách riêng thành component độc lập, `GetOffset()` pure read — design tốt, không side-effect.
- **ObstacleSpawner**: Trajectory-aware obstacle placement là feature thông minh. sqrMagnitude cho distance check — đúng về performance.
- `OnValidate` guards cho tất cả các script chính — tốt.

---

## Recommended Actions

1. **(Medium)** `CameraController.PlayIntro()` — set `_currentState = Intro` trước `StartCoroutine`
2. **(Medium)** `LaunchController.EnableInput()` — thêm `_isDragging = false`
3. **(Low)** `ObstacleSpawner.CreateObstacle()` — thêm comment về tại sao tag = Ground, hoặc tách thành `TagObstacle`
4. **(Low)** `AimArrow.OnValidate()` — thêm guard `_minScale < _maxScale`
5. **(Info)** Xem xét layer cho obstacle: nên nhất quán với ground layer, không phải `Default`

---

## Metrics

- Type Coverage: ~95% (tất cả field có type rõ ràng)
- Linting Issues: 0 syntax errors phát hiện
- Coroutine race conditions: 1 tiềm ẩn (thấp nghiêm trọng)
- Input edge cases: 1 (drag state khi re-enable input)
- Memory leaks: Không phát hiện (`ClearObstacles` và `OnDestroy` clean)

---

## Unresolved Questions

1. Obstacle có tag `Ground` là chủ ý hay accident? Nếu chủ ý thì cần comment giải thích để tránh confusion khi maintain.
2. `GameConstants.GroundTop` được dùng trong trajectory math — giá trị này có được sync với thực tế position của ground trong scene không?
3. Auto-play mode có gọi `PlayIntro()` trong khi rocket đang bay không? Nếu có, bug race condition ở mục #3 cần fix ngay.
