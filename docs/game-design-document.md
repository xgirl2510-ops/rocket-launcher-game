# Game Design Document: Rocket Launcher

## 1. Tổng quan

| Thuộc tính | Giá trị |
|---|---|
| **Tên game** | Rocket Launcher (tạm) |
| **Thể loại** | Physics / Projectile / Casual |
| **Platform** | Mobile (iOS/Android) |
| **Orientation** | Portrait |
| **Engine** | Unity |
| **Số level** | 1 |
| **Target** | Casual player |

## 2. Core Mechanic - Slingshot Launching

### 2.1 Mô tả gameplay loop

```
[Nhìn thấy xe] → [Touch & kéo lùi] → [Thấy mũi tên chỉ hướng + lực]
    → [Thả tay] → [Tên lửa bay vòng cung] → [Camera follow tên lửa]
    → [Trúng target → WIN] hoặc [Trượt → Camera về xe → Bắn lại]
```

### 2.2 Input - Kéo & Thả (Slingshot)

1. **Touch vào xe**: Bắt đầu aiming mode (Raycast + BoxCollider2D lớn hơn visual để dễ chạm trên mobile)
2. **Kéo ngón tay lùi** (về phía dưới/sau xe):
   - Xuất hiện **mũi tên** chỉ hướng bắn (hướng ngược lại với hướng kéo)
   - Mũi tên **dài hơn** = lực mạnh hơn
   - Có thể xoay góc bắn bằng cách thay đổi hướng kéo
3. **Thả tay**: Tên lửa được phóng

### 2.3 Tính toán lực & hướng

```
Hướng bắn = Ngược lại hướng kéo (launchDirection = vehiclePosition - fingerPosition).normalized
Lực bắn   = Khoảng cách kéo × hệ số lực (clamp trong khoảng min-max)
```

| Thông số | Giá trị (tuỳ chỉnh) |
|---|---|
| Khoảng kéo tối thiểu | 0.5 unit (bỏ qua nếu nhỏ hơn) |
| Khoảng kéo tối đa | 3.0 units (clamp) |
| Lực tối thiểu | 5 |
| Lực tối đa | 20 |
| Gravity | Unity default (-9.81) |

### 2.4 Quỹ đạo tên lửa

- Tên lửa bay theo **đường vòng cung (parabolic)** do gravity kéo xuống
- Sử dụng Unity **Rigidbody2D** + AddForce (hoặc set velocity) tại thời điểm thả tay
- Tên lửa **xoay theo hướng velocity** (nose luôn hướng về phía trước)
- Khi chạm đất → dừng lại (không nổ)
- Khi chạm target → WIN

## 3. Camera System

### 3.1 Trạng thái camera

| Trạng thái | Mô tả |
|---|---|
| **IDLE** | Camera focus vào xe, cố định, player thấy xe + khu vực xung quanh |
| **AIMING** | Camera vẫn focus xe, có thể zoom out nhẹ để thấy mũi tên |
| **FOLLOWING** | Sau khi thả tay, camera smooth-follow tên lửa theo cả X và Y |
| **LANDED** | Tên lửa chạm đất/target, camera dừng, hiển thị kết quả (MISS hoặc WIN) |
| **RETURNING** | Nếu miss, camera smooth quay về xe để bắn lại |

### 3.2 Thông số camera

| Thông số | Giá trị |
|---|---|
| Camera size (portrait) | 8-10 (orthographic) |
| Follow speed | Smooth damp ~0.3s |
| Follow offset Y | +2 units (nhìn hơi trước tên lửa) |
| Boundary | Không giới hạn theo tên lửa (free follow) |

## 4. Game Objects

### 4.1 Xe phóng (Launcher Vehicle)

- **Hình dạng placeholder**: Hình chữ nhật (body) + hình chữ nhật nhỏ (cabin) + 2 hình tròn (bánh xe)
- **Màu**: Xanh lá đậm (military style)
- **Vị trí**: Góc dưới-trái màn hình
- **Behavior**: Cố định, không di chuyển

```
    ┌──┐
    │  │        ← Cabin
┌───┴──┴───┐
│  VEHICLE  │   ← Body
└─○──────○─┘
  Wheel  Wheel
```

### 4.2 Tên lửa (Rocket)

- **Hình dạng placeholder**: Hình chữ nhật dài + tam giác (đầu)
- **Màu**: Đỏ
- **Spawn position**: Trên nóc xe
- **Physics**: Rigidbody2D, gravity scale = 1
- **Rotation**: Xoay theo velocity direction

```
  ▲
  │    ← Nose (tam giác)
┌─┤
│ │    ← Body (chữ nhật)
└─┘
```

### 4.3 Mũi tên chỉ hướng (Aim Arrow)

- **Hiển thị**: Chỉ khi đang kéo (aiming)
- **Hướng**: Ngược lại hướng kéo tay
- **Độ dài**: Tỉ lệ với lực bắn
- **Màu**: Trắng hoặc vàng, semi-transparent
- **Không có** trajectory preview — hoàn toàn dựa vào cảm giác

### 4.4 Mặt đất (Ground)

- **Hình dạng**: Dải ngang dài, có terrain gồ ghề (hoặc phẳng cho prototype)
- **Màu**: Nâu/xanh lá nhạt
- **Collider**: EdgeCollider2D hoặc BoxCollider2D dài
- **Kéo dài**: Đủ xa để tên lửa rơi xuống (vài chục units)

### 4.5 Mục tiêu (Target) — BẮT BUỘC

- **Hình dạng placeholder**: Hình vuông màu đỏ
- **Vị trí**: Cách xe một khoảng xa về bên phải (player phải aim đúng để bắn trúng)
- **Collider**: BoxCollider2D, isTrigger = true
- **Win condition**: Tên lửa chạm target → hiển thị WIN
- **Kích thước**: ~1.5 x 1.5 units (đủ thử thách nhưng không quá khó)

## 5. UI Elements

### 5.1 Layout (Portrait)

```
┌─────────────┐
│             │
│   (sky)     │
│             │
│             │
│         🎯  │  ← Target (xa)
│  ←──────    │  ← Aim arrow (khi kéo)
│  🚀         │  ← Rocket trên xe
│  🚛         │  ← Vehicle
│▓▓▓▓▓▓▓▓▓▓▓▓│  ← Ground
│             │
│             │
│             │
└─────────────┘
```

### 5.2 UI Components

| Component | Mô tả |
|---|---|
| **Win text** | Text "YOU WIN!" hiển thị khi trúng target |
| **Restart button** | Nút "RESTART" hiển thị sau khi WIN, nhấn để chơi lại từ đầu |
| **Miss text** | Text "MISS!" hiển thị khi rơi xuống đất, tự ẩn sau 1.5s, sau đó tự động reset |

## 6. Game Flow

```
┌──────────────────┐
│   Game Start     │
│  (Camera ở xe)   │
└────────┬─────────┘
         ▼
┌──────────────────┐
│  Chờ player      │
│  touch vào xe    │◄──────────────────┐
└────────┬─────────┘                   │
         ▼                             │
┌──────────────────┐                   │
│  Player kéo lùi  │                   │
│  Hiện mũi tên    │                   │
│  Chỉnh góc + lực │                   │
└────────┬─────────┘                   │
         ▼                             │
┌──────────────────┐                   │
│  Thả tay         │                   │
│  Tên lửa phóng   │                   │
│  Camera follow   │                   │
└────────┬─────────┘                   │
         ▼                             │
┌─────────┴────────┐                   │
│                  │                   │
▼                  ▼                   │
┌──────────┐  ┌──────────┐            │
│ Trúng    │  │ Trượt    │            │
│ target   │  │ (miss)   │            │
└────┬─────┘  └────┬─────┘            │
     ▼              ▼                  │
┌───────────────┐  ┌───────────────────┐     │
│ YOU WIN!      │  │ Hiện "MISS!" 1.5s │     │
│ [RESTART btn] │  │ Tự động camera    │─────┘
│ → chơi lại   │  │ về xe + reset     │
└───────────────┘  │ rocket → bắn tiếp │
                   └───────────────────┘
```

**Lưu ý:** Bắn không giới hạn cho đến khi trúng target. Không có điểm số.

## 7. Technical Specs

### 7.1 Project Settings

| Setting | Value |
|---|---|
| Unity version | 2022.3+ LTS |
| Render pipeline | Built-in 2D |
| Physics | 2D (Rigidbody2D, Collider2D) |
| Resolution | 1080×1920 (9:16 portrait) |
| Target FPS | 60 |

### 7.2 Scene Hierarchy (dự kiến)

```
Scene: GameScene
├── Main Camera
├── --- ENVIRONMENT ---
│   ├── Ground (long box collider)
│   ├── Background (sky gradient sprite)
│   └── Target (red box, trigger collider)
├── --- GAMEPLAY ---
│   ├── LauncherVehicle
│   │   └── RocketSpawnPoint
│   ├── Rocket (instantiate/activate khi bắn)
│   └── AimArrow (child of vehicle hoặc separate)
├── --- UI ---
│   └── Canvas
│       ├── WinText (ẩn mặc định)
│       ├── RestartButton (ẩn mặc định, hiện sau WIN)
│       └── MissText (ẩn mặc định)
└── --- MANAGERS ---
    └── GameManager
```

### 7.3 Scripts (dự kiến)

| Script | Chức năng |
|---|---|
| `LaunchController.cs` | Xử lý touch input, tính toán hướng + lực, hiển thị mũi tên |
| `Rocket.cs` | Xử lý physics sau khi bắn, xoay theo velocity, detect landing |
| `CameraController.cs` | State machine: Idle → Following → Landed |
| `GameManager.cs` | Quản lý game flow, reset, UI |
| `AimArrow.cs` | Hiển thị mũi tên aim (scale + rotation) |

### 7.4 Physics Notes

- Dùng `Rigidbody2D.AddForce(direction * force, ForceMode2D.Impulse)` tại thời điểm launch
- Rocket xoay theo velocity: `angle = Atan2(velocity.y, velocity.x) * Rad2Deg`
- Ground dùng BoxCollider2D hoặc CompositeCollider2D
- Rocket dùng CircleCollider2D ở đầu mũi để detect va chạm

## 8. Prototype Checklist (Phase 1 - Hình khối)

- [ ] Setup Unity project 2D, portrait 1080×1920
- [ ] Tạo xe bằng hình khối (sprites cơ bản)
- [ ] Tạo tên lửa bằng hình khối
- [ ] Implement slingshot mechanic (touch → drag → release)
- [ ] Hiển thị mũi tên aim
- [ ] Tên lửa bay vòng cung (physics)
- [ ] Tên lửa xoay theo hướng bay
- [ ] Camera follow tên lửa sau khi bắn
- [ ] Ground + collision detection
- [ ] Target (hình vuông đỏ) + win detection
- [ ] Miss → camera quay về xe → bắn lại
- [ ] UI: WIN text, MISS text (auto-reset after 1.5s — no tap to retry)

## 9. Quyết định đã xác nhận

| # | Câu hỏi | Quyết định |
|---|---|---|
| 1 | Target | Có — hình vuông đỏ, trúng = WIN |
| 2 | Trajectory preview | Không — hoàn toàn dựa cảm giác |
| 3 | Tên lửa nổ? | Không — chỉ rơi dừng |
| 4 | Chướng ngại vật | Không — đường bay trống |
| 5 | Scoring | Không điểm — chỉ cần trúng target |
| 6 | Số lần bắn | Không giới hạn — bắn đến khi trúng |

## 10. Mở rộng sau (Post-prototype)

- Hiệu ứng nổ khi trúng target
- Chướng ngại vật trên đường bay
- Nhiều level với target ở vị trí khác nhau
- Thay asset hình khối bằng artwork thật
- Particle effects (khói tên lửa, vụ nổ)
- Sound effects
