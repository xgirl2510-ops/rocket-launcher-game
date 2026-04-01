# Phase 4: Camera Follow System

## Priority: HIGH | Status: Pending | Depends on: Phase 3

## Overview
Tạo `CameraController.cs` — state machine điều khiển camera: intro pan, follow rocket, dừng khi hạ cánh, quay về xe. Kết thúc phase: camera pan từ Target về Xe mỗi đầu round, follow rocket khi bắn, quay về xe khi miss.

## Deliverables
- `Assets/Scripts/Camera/CameraController.cs`

## Key Insights
- Camera dùng `LateUpdate()` (chạy sau physics)
- Follow bằng `Vector2.SmoothDamp` — mượt, không giật
- Camera Z luôn = -10 (không đổi)
- Follow offset Y = +2 (nhìn hơi trước rocket)
- Subscribe vào Rocket events để chuyển state
- **[NEW] Intro Pan**: Mỗi round camera bắt đầu ở Target → dừng 1s → pan về Xe trong 1.5s → player chơi

## Implementation Steps

### Step 1: Tạo CameraController.cs
File: `Assets/Scripts/Camera/CameraController.cs`

**Enum**:
```
CameraState { Intro, Idle, Aiming, Following, Landed, Returning }
```

**SerializedFields**:
- `_rocket` (Rocket)
- `_vehicleTransform` (Transform)
- `_targetTransform` (Transform) — for intro pan start position
- `_followSmoothTime` (0.3f)
- `_returnSmoothTime` (0.5f)
- `_followOffsetY` (2f)
- `_returnThreshold` (0.1f)
- `_introPauseDuration` (1.0f) — pause at Target before panning
- `_introPanDuration` (1.5f) — pan duration from Target to Vehicle

**Logic**:
- `Start()`: Cache `_defaultZ = transform.position.z` (-10), subscribe Rocket events
- `LateUpdate()`: Switch theo `_currentState`:
  - **Intro**: Coroutine handles — snap to Target, pause 1s, Lerp to Vehicle in 1.5s, set Idle
  - **Idle / Aiming**: Không di chuyển (camera đứng yên ở vehicle)
  - **Following**: SmoothDamp đến rocket.position + offsetY
  - **Landed**: Không di chuyển (dừng tại chỗ)
  - **Returning**: SmoothDamp về vehicleTransform.position, khi đến nơi → set Idle
- **PlayIntro()**: Public method, called by GameManager at start of each round. Starts intro coroutine.
- **OnIntroComplete**: Event fired when intro pan finishes → GameManager enables input
- **Event handlers**:
  - `OnRocketLaunched` → SetState(Following)
  - `OnRocketLanded` → SetState(Landed)
  - `OnTargetHit` → SetState(Landed)
- **ReturnToVehicle()**: Public method, set state = Returning (GameManager sẽ gọi)

### Step 2: Gắn CameraController.cs
- Select **Main Camera** → Add Component → CameraController.cs
- Drag references:
  - `_rocket` → Rocket GameObject
  - `_vehicleTransform` → LauncherVehicle (hoặc RocketSpawnPoint)

### Step 3: Set Execution Order
**Edit → Project Settings → Script Execution Order**:
- CameraController: **100** (chạy sau mọi script khác)

### Step 4: Test
- Nhấn Play → kéo thả bắn rocket → quan sát camera

## Verification Checklist

Nhấn **Play** và test:

- [ ] **Intro pan**: Nhấn Play → camera ở Target → dừng 1s → pan mượt về xe trong 1.5s
- [ ] **Intro xong**: Camera đứng yên ở xe, player có thể bắn
- [ ] **Khi bắn**: Camera bắt đầu follow rocket ngay sau khi thả tay
- [ ] **Follow mượt**: Camera di chuyển smooth, không giật, không teleport
- [ ] **Follow offset**: Camera nhìn hơi cao hơn rocket (offset Y = 2)
- [ ] **Rocket bay xa**: Camera follow theo cả X lẫn Y
- [ ] **Rocket chạm đất**: Camera dừng lại (state = Landed), không tiếp tục di chuyển
- [ ] **Không rung/giật**: Camera smooth suốt quá trình follow
- [ ] **Camera Z**: Luôn = -10 (không bị thay đổi)

Lưu ý: Camera chưa tự quay về xe (cần GameManager gọi `ReturnToVehicle()` — Phase 5). Hiện tại sau khi rocket rơi, camera dừng tại đó → đúng.

> Nếu tất cả OK → Phase 4 hoàn thành. Qua Phase 5.
