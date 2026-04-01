# System Architecture & Script Communication

## Architecture Overview

```
┌─────────────────────────────────────────────────┐
│                  GameManager                     │
│  (Singleton — owns game state & reset logic)     │
│                                                  │
│  States: WaitingForInput → Aiming → Flying        │
│          → Win / Miss → Resetting                │
└──────┬──────────────┬───────────────┬────────────┘
       │ references   │ references    │ references
       ▼              ▼               ▼
┌──────────────┐ ┌──────────┐ ┌────────────────┐
│ LaunchController│ │  Rocket  │ │CameraController│
│              │ │          │ │                │
│ Input → Aim  │ │ Physics  │ │ State-based    │
│ → Fire       │ │ Rotation │ │ follow/return  │
└──────┬───────┘ │ Landing  │ └────────────────┘
       │         └──────────┘
       ▼
┌──────────────┐
│  AimArrow    │
│ (visual only)│
└──────────────┘
```

## Script Responsibilities

### GameManager.cs

- **Role**: Central game state controller
- **Pattern**: Singleton
- **Owns**: Game state enum, reset logic, UI references
- **Listens to**: `Rocket.OnRocketLanded`, `Rocket.OnTargetHit`
- **Controls**: Triggers camera return, shows WIN/MISS UI, spawns/resets rocket

### LaunchController.cs

- **Role**: Handle player input, calculate launch direction & force
- **Owns**: Input detection, drag calculation, launch execution
- **References**: Rocket (to apply force), AimArrow (to show/hide)
- **Communicates**: Calls `Rocket.Launch(direction, force)` on release
- **Notifies**: `GameManager` that launch happened (via event or direct call)

### Rocket.cs

- **Role**: Rocket physics, rotation, collision detection
- **Owns**: Rigidbody2D, collision/trigger callbacks
- **Events fired**:
  - `OnRocketLaunched` — when launched (camera starts following)
  - `OnRocketLanded` — when hits ground (MISS)
  - `OnTargetHit` — when hits target trigger (WIN)
- **Does NOT know about**: Camera, UI, input

### CameraController.cs

- **Role**: Camera movement state machine
- **States**: `Idle → Aiming → Following → Landed → Returning`
- **Listens to**: `Rocket.OnRocketLaunched`, `Rocket.OnRocketLanded`, `Rocket.OnTargetHit`
- **References**: Rocket transform (follow target), Vehicle transform (return target)
- **Uses**: `Vector2.SmoothDamp` for smooth following

### AimArrow.cs

- **Role**: Visual-only aim indicator
- **Controlled by**: `LaunchController`
- **Logic**: Rotate to face launch direction, scale length by force magnitude
- **No physics, no events**

## Communication Flow

### Event System (C# Actions)

```
Rocket.cs defines:
  public event Action OnRocketLaunched;
  public event Action OnRocketLanded;
  public event Action OnTargetHit;

Subscribers:
  GameManager    → OnRocketLanded, OnTargetHit
  CameraController → OnRocketLaunched, OnRocketLanded, OnTargetHit
```

### Direct References (Inspector-assigned)

```
GameManager
  ├── _launchController  → LaunchController
  ├── _rocket            → Rocket
  ├── _cameraController  → CameraController
  ├── _winText           → TextMeshProUGUI
  ├── _restartButton     → Button (shown after WIN, calls ResetRound)
  └── _missText          → TextMeshProUGUI

LaunchController
  ├── _rocket            → Rocket
  ├── _aimArrow          → AimArrow
  └── _spawnPoint        → Transform (RocketSpawnPoint)

CameraController
  ├── _rocket            → Rocket (follow target)
  └── _vehicleTransform  → Transform (return target)
```

### NO cross-references between:

- `Rocket` ↔ `CameraController` (communicate via events only)
- `Rocket` ↔ `AimArrow` (no relationship)
- `AimArrow` ↔ `CameraController` (no relationship)

## Game State Machine

```
                    ┌──────────────────┐
                    │ WaitingForInput  │◄─────────────┐
                    └────────┬─────────┘              │
                     player touches                   │
                    ┌────────▼─────────┐              │
                    │     Aiming       │              │
                    └────────┬─────────┘              │
                     player releases                  │
                    ┌────────▼─────────┐              │
                    │     Flying       │              │
                    └───┬──────────┬───┘              │
                  hit   │          │ hit ground       │
                 target │          │                   │
              ┌─────────▼──┐  ┌───▼──────────┐       │
              │    Win     │  │    Miss      │       │
              │ (end state)│  └───┬──────────┘       │
              └────────────┘      │ after 1.5s        │
                            ┌─────▼──────────┐       │
                            │   Resetting    │───────┘
                            │ (camera return │
                            │  + reset rocket)│
                            └────────────────┘
```

## Physics Layer Matrix

| | Default | Rocket | UI |
|---|---|---|---|
| **Default** | ✓ | ✓ | - |
| **Rocket** | ✓ | - | - |
| **UI** | - | - | - |

Rocket collides with Default layer (ground, environment) but not with itself or UI.

## Execution Order (recommended)

| Priority | Script | Reason |
|---|---|---|
| -100 | GameManager | Initialize first, set up state |
| 0 | LaunchController | Default — reads input in Update |
| 0 | AimArrow | Default — visual update |
| 0 | Rocket | Default — physics in FixedUpdate |
| 100 | CameraController | Execute after physics — smooth follow |
