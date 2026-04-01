# Phase 1: Project & Scene Setup

## Priority: HIGH | Status: Pending

## Overview
Tạo Unity project mới, setup folder structure, tạo tất cả GameObjects bằng hình khối placeholder. Kết thúc phase này: nhấn Play → thấy đúng layout game (xe, rocket, ground, target, sky) nhưng chưa có interaction.

## Key Insights
- Dùng Unity Default Sprite (square, circle) cho tất cả placeholder shapes
- Triangle sprite cho rocket nose cần tạo riêng hoặc dùng rotated square
- Canvas setup portrait 1080x1920 ngay từ đầu

## Steps

### Step 1: Tạo Unity Project
1. Mở Unity Hub → New Project
2. Chọn template **2D (Built-in Render Pipeline)**
3. Tên project: `RocketLauncher`
4. Unity version: **2022.3 LTS** trở lên

### Step 2: Tạo Folder Structure
```
Assets/
├── Scenes/
│   └── GameScene.unity (rename SampleScene)
├── Scripts/
│   ├── Core/
│   ├── Launch/
│   ├── Rocket/
│   └── Camera/
├── Prefabs/
├── Materials/
├── Sprites/
└── UI/
```
- Trong Unity: Right-click Assets → Create → Folder
- Rename scene mặc định thành `GameScene`

### Step 3: Setup Tags & Layers
**Edit → Project Settings → Tags and Layers**

Tags (thêm):
- `Target`
- `Ground`
- (Tag `Player` đã có sẵn)

Sorting Layers (thêm theo thứ tự):
1. `Background` (order 0)
2. `Environment` (order 1)
3. `Gameplay` (order 2)
4. `Projectile` (order 3)

Layers (thêm):
- Layer 6: `Rocket`

### Step 4: Camera Setup
Chọn **Main Camera** trong Hierarchy:

| Property | Value |
|---|---|
| Position | (0, 2, -10) |
| Projection | Orthographic |
| Size | 9 |
| Background Color | #87CEEB (sky blue) |

### Step 5: Tạo Environment

#### 5a. Background (optional - camera background color đã là sky)
- Skip nếu dùng camera background color

#### 5b. Ground
1. Hierarchy → Create Empty → đặt tên `--- ENVIRONMENT ---` (separator)
2. Right-click `--- ENVIRONMENT ---` → 2D Object → Sprites → Square
3. Đặt tên: `Ground`

| Property | Value |
|---|---|
| Position | (20, -4, 0) |
| Scale | (80, 1, 1) |
| Tag | `Ground` |
| Sorting Layer | `Environment` |
| Sprite Color | #8B6914 (brown) |
| BoxCollider2D | Add Component → BoxCollider2D (auto-size) |

#### 5c. Target
1. Right-click `--- ENVIRONMENT ---` → 2D Object → Sprites → Square
2. Đặt tên: `Target`

| Property | Value |
|---|---|
| Position | (18, -2.5, 0) |
| Scale | (1.5, 1.5, 1) |
| Tag | `Target` |
| Sorting Layer | `Gameplay` |
| Sprite Color | #FF0000 (red) |
| BoxCollider2D | Add Component → BoxCollider2D, **isTrigger = true** |

### Step 6: Tạo Launcher Vehicle

1. Hierarchy → Create Empty → đặt tên `--- GAMEPLAY ---` (separator)
2. Right-click `--- GAMEPLAY ---` → Create Empty → đặt tên `LauncherVehicle`

**LauncherVehicle** (parent):

| Property | Value |
|---|---|
| Position | (-3, -3, 0) |
| BoxCollider2D | Add, isTrigger = true, Size: (3, 2) — vùng touch lớn |

**Children** (Right-click LauncherVehicle → 2D Object → Sprites → Square/Circle):

| Child | Sprite | Local Position | Scale | Color | Sorting Layer |
|---|---|---|---|---|---|
| Body | Square | (0, 0, 0) | (2, 0.8, 1) | #2D5016 | Gameplay |
| Cabin | Square | (0.3, 0.6, 0) | (0.6, 0.6, 1) | #2D5016 | Gameplay |
| WheelLeft | Circle | (-0.6, -0.5, 0) | (0.4, 0.4, 1) | #333333 | Gameplay |
| WheelRight | Circle | (0.6, -0.5, 0) | (0.4, 0.4, 1) | #333333 | Gameplay |
| RocketSpawnPoint | Empty | (0, 1.0, 0) | (1, 1, 1) | — | — |

### Step 7: Tạo Rocket

1. Right-click `--- GAMEPLAY ---` → Create Empty → đặt tên `Rocket`

**Rocket** (parent):

| Property | Value |
|---|---|
| Position | (-3, -2, 0) — tạm thời, sẽ set = SpawnPoint runtime |
| Tag | `Player` |
| Layer | `Rocket` |
| Sorting Layer | `Projectile` |

Components trên Rocket (parent):
- **Rigidbody2D**: Body Type = Kinematic, Gravity Scale = 1, Mass = 1, Linear Drag = 0, Angular Drag = 0, Collision Detection = Continuous
- **CircleCollider2D**: Radius = 0.15, Offset = (0, 0.5)

**Children**:

| Child | Sprite | Local Position | Scale | Color | Sorting Layer |
|---|---|---|---|---|---|
| Body | Square | (0, 0, 0) | (0.3, 0.8, 1) | #CC0000 | Projectile |
| Nose | Square (rotated 45°) | (0, 0.55, 0) | (0.21, 0.21, 1) | #CC0000 | Projectile |

> **Note Nose**: Dùng Square xoay 45° (Z rotation = 45) scale nhỏ để giả tam giác. Hoặc tạo triangle sprite riêng nếu muốn đẹp hơn.

### Step 8: Tạo AimArrow

1. Right-click `--- GAMEPLAY ---` → 2D Object → Sprites → Square → đặt tên `AimArrow`

| Property | Value |
|---|---|
| Position | (-3, -2, 0) — same as RocketSpawnPoint |
| Scale | (0.15, 1, 1) — thanh mỏng dài |
| Color | White, Alpha = 0.7 (178/255) |
| Sorting Layer | `Projectile` |
| SpriteRenderer | **Disabled** (ẩn mặc định) |

> Pivot: Đặt sprite pivot = Bottom Center trong Sprite Editor (hoặc offset position)

### Step 9: Tạo Input GameObject

1. Hierarchy → Create Empty → đặt tên `--- INPUT ---` (separator)
2. Right-click → Create Empty → đặt tên `LaunchController`
- Chưa gắn script (Phase 3)

### Step 10: Tạo Canvas & UI

1. Hierarchy → Create Empty → đặt tên `--- UI ---` (separator)
2. Right-click `--- UI ---` → UI → Canvas

**Canvas settings**:

| Property | Value |
|---|---|
| Render Mode | Screen Space - Overlay |
| Canvas Scaler → UI Scale Mode | Scale With Screen Size |
| Reference Resolution | 1080 × 1920 |
| Match | 0.5 |

**Children** (Right-click Canvas → UI → Text - TextMeshPro):

#### WinText
| Property | Value |
|---|---|
| Anchor | Center |
| Position | (0, 200, 0) |
| Font Size | 72 |
| Color | #FFD700 (gold) |
| Alignment | Center |
| Text | "YOU WIN!" |
| GameObject | **SetActive = false** |

#### MissText
| Property | Value |
|---|---|
| Anchor | Center |
| Position | (0, 200, 0) |
| Font Size | 60 |
| Color | #FFFFFF (white) |
| Alignment | Center |
| Text | "MISS!" |
| GameObject | **SetActive = false** |

#### RestartButton
1. Right-click Canvas → UI → Button - TextMeshPro → đặt tên `RestartButton`

| Property | Value |
|---|---|
| Anchor | Center |
| Position | (0, 50, 0) |
| Size | (300, 80) |
| Image Color | #2D5016 (dark green) |
| Child Text → Text | "RESTART" |
| Child Text → Font Size | 40 |
| Child Text → Color | White |
| GameObject | **SetActive = false** |

### Step 11: Tạo Managers

1. Hierarchy → Create Empty → đặt tên `--- MANAGERS ---` (separator)
2. Right-click → Create Empty → đặt tên `GameManager`
- Chưa gắn script (Phase 5)

### Step 12: Project Settings

**Edit → Project Settings → Physics 2D**:
| Setting | Value |
|---|---|
| Gravity Y | -9.81 |

**Edit → Project Settings → Quality**:
| Setting | Value |
|---|---|
| VSync Count | Don't Sync |

**Game view**: Set resolution = 1080x1920 (hoặc 9:16 portrait)

### Step 13: Save
- Ctrl+S để save scene
- File → Save Project

## Final Hierarchy (expected)
```
GameScene
├── Main Camera
├── --- MANAGERS ---
│   └── GameManager
├── --- ENVIRONMENT ---
│   ├── Ground
│   └── Target
├── --- GAMEPLAY ---
│   ├── LauncherVehicle
│   │   ├── Body
│   │   ├── Cabin
│   │   ├── WheelLeft
│   │   ├── WheelRight
│   │   └── RocketSpawnPoint
│   ├── Rocket
│   │   ├── Body
│   │   └── Nose
│   └── AimArrow
├── --- INPUT ---
│   └── LaunchController
└── --- UI ---
    └── Canvas
        ├── WinText (inactive)
        ├── MissText (inactive)
        └── RestartButton (inactive)
```

## Verification Checklist

Nhấn **Play** và kiểm tra:

- [ ] **Background**: Nền trời xanh (#87CEEB)
- [ ] **Ground**: Dải nâu nằm ngang phía dưới màn hình
- [ ] **Vehicle**: Xe xanh lá ở góc dưới-trái, có 2 bánh xe tròn đen
- [ ] **Rocket**: Hình chữ nhật đỏ nhỏ nằm trên nóc xe
- [ ] **Target**: Hình vuông đỏ ở xa bên phải (có thể phải kéo Scene view sang phải để thấy)
- [ ] **AimArrow**: KHÔNG thấy (đã ẩn) → đúng
- [ ] **UI**: KHÔNG thấy WIN/MISS/RESTART text → đúng (đã inactive)
- [ ] **No errors**: Console không có lỗi đỏ
- [ ] **Game view**: Portrait mode (cao hơn rộng)
- [ ] **Camera position**: Thấy xe + ground, không thấy target (target ở xa → đúng, player phải bắn xa)

> Nếu tất cả OK → Phase 1 hoàn thành. Qua Phase 2.
