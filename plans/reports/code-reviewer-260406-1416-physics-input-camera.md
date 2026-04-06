# Code Review: Physics, Input & Camera
**Scope:** Rocket.cs, LaunchController.cs, AimArrow.cs, CameraController.cs, camera-screen-shake.cs, ObstacleSpawner.cs
**Date:** 2026-04-06 | **Score: 9.0 / 10**

---

## Tổng quan

Code nhìn chung rất sạch, kiến trúc rõ ràng, đúng convention Unity. Không có lỗi nghiêm trọng. Một số điểm nhỏ cần chú ý.

---

## Vấn đề cần xem xét

### Medium

**1. `Rocket.OnCollisionEnter2D` — zero velocity trước physics settle**
```csharp
_rb.linearVelocity = Vector2.zero;
_rb.angularVelocity = 0f;
```
Set velocity trong `OnCollisionEnter2D` (callback physics) là đúng thời điểm, nhưng rocket vẫn còn `Dynamic` sau đó. Nếu có bounce (Physics Material 2D với bounciness > 0), rocket có thể bay lại sau khi `_isFlying = false`. Nên chuyển sang `Kinematic` ngay tại đây như `ResetToPosition` đã làm.

**2. `ObstacleSpawner.CalculateTrajectory` — `totalTime` có thể thiếu chính xác**
```csharp
float totalTime = Mathf.Min(Mathf.Abs(dx) / Mathf.Max(Mathf.Abs(vx), 0.1f), timeOfFlight * 1.2f);
```
`timeOfFlight` được tính từ `start.y - GameConstants.GroundTop` thay vì từ `start.y` đến `target.y`. Nếu target nằm trên không (Y > GroundTop), arc có thể bị cắt ngắn, safe zone không cover hết trajectory → obstacle có thể chặn đường đi của rocket. Nên dùng thời gian thực đến target.

**3. `CameraController.SetState` gọi `StopActiveCoroutine` từ event handler**

`HandleRocketLaunched/Landed/HitTarget` được gọi từ event (main thread, đúng). Tuy nhiên nếu `SetState(Following)` được gọi trong khi `IntroCoroutine` đang chạy, coroutine bị stop đột ngột và `OnIntroComplete` không bao giờ được fire → có thể LaunchController không bao giờ nhận `EnableInput`. Hiện tại flow được kiểm soát bởi RoundManager nên ít xảy ra, nhưng là điểm dễ tạo bug ẩn.

**4. `CameraScreenShake.Update` chạy khi không cần thiết**

Khi `_elapsed >= _duration`, `Update` vẫn chạy mỗi frame chỉ để set `_currentOffset = Vector2.zero`. Nên thêm flag `_isShaking` để early-exit.

### Low

**5. `LaunchController` — `Camera.main` được gọi lại mỗi frame trong Update**
```csharp
if (_camera == null) _camera = Camera.main;
```
`Camera.main` dùng `FindObjectsByType` nội bộ — tốn CPU nếu gọi nhiều. Nhưng đây chỉ là fallback khi `_camera == null` (rất hiếm), nên tác động thực tế không đáng kể.

**6. `ObstacleSpawner.CreateObstacle` — layer hardcode**
```csharp
go.layer = LayerMask.NameToLayer("Default");
```
Các script khác dùng `GameConstants` cho layer. Nên dùng `GameConstants.LayerObstacle` hoặc constant tương đương để SSOT.

**7. `AimArrow` — không reset rotation khi Hide**

Không ảnh hưởng gameplay nhưng nếu arrow được reuse, rotation cũ sẽ giữ nguyên cho đến `UpdateArrow` tiếp theo.

---

## Điểm tốt

- `FixedUpdate` / `LateUpdate` / `Update` phân chia đúng: physics trong `FixedUpdate`, camera trong `LateUpdate`, input trong `Update`.
- `_rb.MoveRotation` trong `FixedUpdate` — chuẩn xác, không dùng `transform.rotation` trực tiếp trong physics loop.
- `StopActiveCoroutine` pattern ngăn coroutine race condition tốt.
- `PanCoroutine` DRY, dùng chung cho intro/return/look-target.
- `OnDestroy` unsubscribe events — không memory leak.
- `CameraScreenShake` tách riêng, pure read `GetOffset()` không side-effect.
- Trajectory math trong `ObstacleSpawner` dùng discriminant đúng cách, có fallback `theta = 60°` khi degenerate.
- `TryComputeDrag` normalize lực đúng: `(clamped - min) / (max - min)`.

---

## Khuyến nghị ưu tiên

1. **[Medium]** `Rocket.OnCollisionEnter2D`: thêm `_rb.bodyType = RigidbodyType2D.Kinematic` sau khi zero velocity để tránh bounce edge case.
2. **[Medium]** `ObstacleSpawner`: xem lại `totalTime` — tính thời gian đến `target.y` thay vì chỉ đến `GroundTop`.
3. **[Low]** `GameConstants`: thêm `LayerObstacle` constant, cập nhật `ObstacleSpawner.CreateObstacle`.
4. **[Low]** `CameraScreenShake`: thêm `_isShaking` flag để `Update` early-exit.

---

## Câu hỏi chưa giải quyết

- `GameConstants.GroundTop` có giá trị cụ thể là bao nhiêu? Nếu target luôn ở dưới GroundTop thì vấn đề #2 không xảy ra.
- Obstacle có Physics Material 2D không? Nếu không, vấn đề #1 (bounce) không thực tế.
