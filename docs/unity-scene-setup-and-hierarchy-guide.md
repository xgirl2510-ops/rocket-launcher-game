# Scene Setup & Hierarchy Guide

## Scene Name

`GameScene.unity` — single scene, no scene transitions.

## Full Hierarchy

```
GameScene
├── Main Camera                         ← CameraController.cs
├── --- MANAGERS ---
│   └── GameManager                     ← GameManager.cs
├── --- ENVIRONMENT ---
│   ├── Background                      ← SpriteRenderer (sky gradient)
│   ├── Ground                          ← BoxCollider2D (long platform)
│   └── Target                          ← BoxCollider2D (isTrigger)
├── --- GAMEPLAY ---
│   ├── LauncherVehicle                 ← Static, no scripts; BoxCollider2D (isTrigger=true) on Body for touch hit-detection by LaunchController
│   │   ├── Body                        ← SpriteRenderer (green rectangle)
│   │   ├── Cabin                       ← SpriteRenderer (small rectangle)
│   │   ├── WheelLeft                   ← SpriteRenderer (circle)
│   │   ├── WheelRight                  ← SpriteRenderer (circle)
│   │   └── RocketSpawnPoint            ← Empty Transform (spawn position)
│   ├── Rocket                          ← Rocket.cs, Rigidbody2D, CircleCollider2D
│   │   ├── Body                        ← SpriteRenderer (red rectangle)
│   │   └── Nose                        ← SpriteRenderer (red triangle)
│   └── AimArrow                        ← AimArrow.cs, SpriteRenderer
├── --- INPUT ---
│   └── LaunchController                ← LaunchController.cs (empty GameObject)
└── --- UI ---
    └── Canvas                          ← Canvas (Screen Space - Overlay)
        ├── WinText                     ← TextMeshProUGUI ("YOU WIN!")
        ├── RestartButton               ← Button + TextMeshProUGUI ("RESTART")
        └── MissText                    ← TextMeshProUGUI ("MISS!")
```

## Transform Values

### Main Camera

| Property | Value |
|---|---|
| Position | (0, 2, -10) |
| Projection | Orthographic |
| Size | 9 |
| Background Color | Sky blue (#87CEEB) |

### Ground

| Property | Value |
|---|---|
| Position | (20, -4, 0) |
| Scale | (80, 1, 1) |
| Tag | `Ground` |
| SpriteRenderer Color | Brown (#8B6914) |
| BoxCollider2D | Auto-size (matches sprite) |

### Target

| Property | Value |
|---|---|
| Position | (18, -2.5, 0) |
| Scale | (1.5, 1.5, 1) |
| Tag | `Target` |
| SpriteRenderer Color | Red (#FF0000) |
| BoxCollider2D | isTrigger = true, auto-size |

### LauncherVehicle

| Property | Value |
|---|---|
| Position | (-3, -3, 0) |
| Static | true (no Rigidbody) |
| BoxCollider2D | isTrigger = true, sized ~3 × 2 (larger than visual for easy mobile touch) — used by LaunchController for touch hit-detection |

#### LauncherVehicle Children

| Child | Local Position | Scale | Color |
|---|---|---|---|
| Body | (0, 0, 0) | (2, 0.8, 1) | Dark Green (#2D5016) |
| Cabin | (0.3, 0.6, 0) | (0.6, 0.6, 1) | Dark Green (#2D5016) |
| WheelLeft | (-0.6, -0.5, 0) | (0.4, 0.4, 1) | Dark Gray (#333333) |
| WheelRight | (0.6, -0.5, 0) | (0.4, 0.4, 1) | Dark Gray (#333333) |
| RocketSpawnPoint | (0, 1.0, 0) | (1, 1, 1) | — (empty) |

### Rocket

| Property | Value |
|---|---|
| Position | Same as RocketSpawnPoint (set at runtime) |
| Tag | `Player` |
| Rigidbody2D | Kinematic (initial) → Dynamic (on launch), Gravity Scale=1, Mass=1, Continuous collision |
| CircleCollider2D | Radius=0.15, Offset=(0, 0.5) |

#### Rocket Children

| Child | Local Position | Scale | Color |
|---|---|---|---|
| Body | (0, 0, 0) | (0.3, 0.8, 1) | Red (#CC0000) |
| Nose | (0, 0.55, 0) | (0.3, 0.3, 1) | Red (#CC0000) — triangle sprite |

### AimArrow

| Property | Value |
|---|---|
| Position | Same as RocketSpawnPoint |
| SpriteRenderer | Arrow/rectangle sprite, white, alpha=0.7 |
| Pivot | Bottom-center (arrow grows upward from origin) |
| Default state | Hidden (SpriteRenderer disabled) |

## Sorting Layers (back to front)

| Order | Sorting Layer | Objects |
|---|---|---|
| 0 | Background | Background sprite |
| 1 | Environment | Ground |
| 2 | Gameplay | Vehicle, Target |
| 3 | Projectile | Rocket, AimArrow |
| 4 | UI | Canvas (handled by Unity UI) |

## Tags

| Tag | Assigned To |
|---|---|
| `Untagged` | Most objects |
| `Player` | Rocket |
| `Target` | Target |
| `Ground` | Ground |

## Layers

| Layer # | Name | Assigned To |
|---|---|---|
| 0 | Default | Most objects |
| 6 | Rocket | Rocket |
| 5 | UI | Canvas |

## Canvas Setup

| Property | Value |
|---|---|
| Render Mode | Screen Space - Overlay |
| Canvas Scaler | Scale with Screen Size |
| Reference Resolution | 1080 × 1920 |
| Match | 0.5 (width/height balance) |

### WinText

| Property | Value |
|---|---|
| Anchor | Center |
| Position | (0, 200, 0) |
| Font Size | 72 |
| Color | Gold (#FFD700) |
| Alignment | Center |
| Text | "YOU WIN!" |
| Default state | `gameObject.SetActive(false)` |

### RestartButton

| Property | Value |
|---|---|
| Anchor | Center |
| Position | (0, 50, 0) |
| Size | (300, 80) |
| Font Size | 40 |
| Color | White text on Dark Green (#2D5016) background |
| Text | "RESTART" |
| Default state | `gameObject.SetActive(false)` — shown only after WIN |
| OnClick | `GameManager.Instance.ResetRound()` |

### MissText

| Property | Value |
|---|---|
| Anchor | Center |
| Position | (0, 200, 0) |
| Font Size | 60 |
| Color | White (#FFFFFF) |
| Alignment | Center |
| Text | "MISS!" |
| Default state | `gameObject.SetActive(false)` |

## Physics Settings (Edit → Project Settings → Physics 2D)

| Setting | Value |
|---|---|
| Gravity | (0, -9.81) |
| Default Contact Offset | 0.01 |
| Velocity Iterations | 8 |
| Position Iterations | 3 |

## Quality Settings

| Setting | Value |
|---|---|
| VSync Count | Don't Sync |
| Target Frame Rate | 60 (set in script) |

```csharp
// In GameManager.Awake()
Application.targetFrameRate = 60;
```
