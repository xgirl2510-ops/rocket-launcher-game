# Phase 3: Slingshot Input & Aim Arrow

## Priority: HIGH | Status: Pending | Depends on: Phase 2

## Overview
Tạo `LaunchController.cs` (xử lý touch/mouse input, tính hướng + lực) và `AimArrow.cs` (hiển thị mũi tên aim). Kết thúc phase: kéo thả trên xe → thấy mũi tên → thả tay → rocket bay.

## Deliverables
- `Assets/Scripts/Launch/LaunchController.cs`
- `Assets/Scripts/Launch/AimArrow.cs`

## Key Insights
- Input: dùng `Input.mousePosition` (hoạt động cả mouse lẫn touch)
- Hướng bắn = ngược hướng kéo: `(spawnPoint - fingerWorldPos).normalized`
- Lực = map khoảng cách kéo (0.5→3.0 units) sang force (5→20)
- AimArrow: xoay theo hướng bắn, scale Y theo lực
- Vehicle cần BoxCollider2D isTrigger để Raycast detect touch

## Implementation Steps

### Step 1: Tạo AimArrow.cs
File: `Assets/Scripts/Launch/AimArrow.cs`

Script cần có:
- **SerializedFields**: `_spriteRenderer`, `_minScale` (0.5), `_maxScale` (2.0), `_color` (white alpha 0.7)
- **Show()**: Enable spriteRenderer
- **Hide()**: Disable spriteRenderer
- **UpdateArrow(Vector2 direction, float normalizedForce)**:
  - Xoay: `Atan2(dir.y, dir.x) * Rad2Deg - 90f`
  - Scale Y: `Lerp(minScale, maxScale, normalizedForce)`

### Step 2: Gắn AimArrow.cs
- Select AimArrow GameObject → Add Component → AimArrow.cs
- Drag SpriteRenderer vào field `_spriteRenderer`

### Step 3: Tạo LaunchController.cs
File: `Assets/Scripts/Launch/LaunchController.cs`

Script cần có:
- **SerializedFields**: `_rocket`, `_aimArrow`, `_spawnPoint`, `_minDragDistance` (0.5), `_maxDragDistance` (3.0), `_minLaunchForce` (5), `_maxLaunchForce` (20)
- **Private fields**: `_isDragging`, `_dragStartPos`, `_camera`, `_inputEnabled`
- **Update()**: Đọc input mỗi frame (mouse/touch)
  - Mouse down → `HandleTouchBegan()`: Raycast check xem touch có trúng vehicle không
  - Mouse drag → `HandleTouchMoved()`: Tính drag vector, update arrow
  - Mouse up → `HandleTouchEnded()`: Tính force + direction, gọi `_rocket.Launch()`
- **EnableInput() / DisableInput()**: Bật/tắt input
- **CalculateLaunchDirection()**: `(spawnPoint.position - touchWorldPos).normalized`
- **CalculateLaunchForce()**: Lerp min→max force dựa trên drag distance

### Step 4: Gắn LaunchController.cs
- Select LaunchController GameObject → Add Component → LaunchController.cs
- Drag references:
  - `_rocket` → Rocket GameObject
  - `_aimArrow` → AimArrow GameObject
  - `_spawnPoint` → RocketSpawnPoint (child of LauncherVehicle)

### Step 5: Đảm bảo Vehicle có Collider cho Raycast
- LauncherVehicle đã có BoxCollider2D (isTrigger=true, size 3x2) từ Phase 1
- LaunchController dùng `Physics2D.OverlapPoint()` hoặc Raycast để check touch trúng vehicle

## Verification Checklist

Nhấn **Play** và test:

- [ ] **Touch/click vào xe**: Bắt đầu aiming mode (không có gì xảy ra visual nếu chưa kéo)
- [ ] **Kéo lùi (xuống/ra sau xe)**: Mũi tên trắng xuất hiện từ vị trí rocket
- [ ] **Mũi tên chỉ hướng đúng**: Kéo xuống → mũi tên chỉ lên. Kéo trái → mũi tên chỉ phải
- [ ] **Mũi tên scale theo lực**: Kéo xa hơn → mũi tên dài hơn
- [ ] **Thả tay**: Mũi tên ẩn, rocket bay theo hướng mũi tên chỉ
- [ ] **Kéo quá ngắn** (< 0.5 unit): Không launch (ignore)
- [ ] **Kéo quá xa** (> 3 units): Lực bị clamp ở max, không tăng thêm
- [ ] **Click vào vùng không phải xe**: Không có gì xảy ra
- [ ] **Rocket bay vòng cung**: Vẫn đúng physics từ Phase 2
- [ ] **Bắn nhiều lần**: Sau khi rocket rơi xuống, phải tự tay nhấn Play lại (chưa có auto-reset — Phase 5 sẽ xử lý)

> Nếu tất cả OK → Phase 3 hoàn thành. Qua Phase 4.
