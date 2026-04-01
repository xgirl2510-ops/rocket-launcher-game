# Technical Specifications Per Script

## 1. GameManager.cs

### Enum

```csharp
public enum GameState
{
    WaitingForInput,
    Aiming,
    Flying,
    Win,
    Miss,
    Resetting
}
```

### Serialized Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `_launchController` | LaunchController | — | Reference to launch controller |
| `_rocket` | Rocket | — | Reference to rocket |
| `_cameraController` | CameraController | — | Reference to camera controller |
| `_winText` | TextMeshProUGUI | — | "YOU WIN!" text (hidden by default) |
| `_missText` | TextMeshProUGUI | — | "MISS!" text (hidden by default) |
| `_restartButton` | Button | — | "RESTART" button (hidden by default, shown after WIN) |
| `_missDisplayDuration` | float | 1.5f | How long MISS text shows before auto-reset triggers (seconds) — no separate reset delay |

### Public Properties

| Property | Type | Description |
|---|---|---|
| `Instance` | GameManager (static) | Singleton accessor |
| `CurrentState` | GameState | Current game state (read-only) |

### Public Methods

| Method | Signature | Description |
|---|---|---|
| `SetState` | `void SetState(GameState newState)` | Transition game state, trigger side effects |
| `ResetRound` | `void ResetRound()` | Reset rocket to spawn point, hide UI, set WaitingForInput |

### Private Methods

| Method | Description |
|---|---|
| `HandleTargetHit()` | Show win text + restart button, set Win state |
| `HandleRocketLanded()` | Show miss text, start reset coroutine |
| `ResetCoroutine()` | Wait → hide miss text → tell camera to return → reset rocket |

### Events Subscribed To

- `Rocket.OnTargetHit` → `HandleTargetHit()`
- `Rocket.OnRocketLanded` → `HandleRocketLanded()`

---

## 2. LaunchController.cs

### Serialized Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `_rocket` | Rocket | — | Rocket to launch |
| `_aimArrow` | AimArrow | — | Aim arrow visual |
| `_spawnPoint` | Transform | — | Rocket spawn position (on vehicle) |
| `_minDragDistance` | float | 0.5f | Min drag to register (units) |
| `_maxDragDistance` | float | 3.0f | Max drag distance (clamped) |
| `_minLaunchForce` | float | 5f | Force at min drag |
| `_maxLaunchForce` | float | 20f | Force at max drag |

### Private Fields

| Field | Type | Description |
|---|---|---|
| `_isDragging` | bool | Currently in drag/aim mode |
| `_dragStartPos` | Vector2 | World position where touch began |
| `_camera` | Camera | Cached main camera reference |

### Public Methods

| Method | Signature | Description |
|---|---|---|
| `EnableInput` | `void EnableInput()` | Allow player to start aiming |
| `DisableInput` | `void DisableInput()` | Block input (during flight/reset) |

### Private Methods

| Method | Description |
|---|---|
| `Update()` | Read touch input each frame |
| `HandleTouchBegan(Vector2 screenPos)` | Check if touch is on/near vehicle, start drag |
| `HandleTouchMoved(Vector2 screenPos)` | Calculate drag vector, update arrow |
| `HandleTouchEnded()` | Calculate final force & direction, call `_rocket.Launch()` |
| `CalculateLaunchDirection(Vector2 dragEnd)` | `(spawnPoint.position - dragEnd).normalized` |
| `CalculateLaunchForce(Vector2 dragEnd)` | Map drag distance to force (lerp min↔max) |

### Input Logic

```
Touch phase:
  Began  → HandleTouchBegan()  → if near vehicle: _isDragging = true
  Moved  → HandleTouchMoved()  → update aim arrow direction & scale
  Ended  → HandleTouchEnded()  → launch rocket, disable input
```

### Force Calculation

```csharp
Vector2 dragVector = (Vector2)_spawnPoint.position - touchWorldPos;
float dragDistance = Mathf.Clamp(dragVector.magnitude, 0f, _maxDragDistance);

if (dragDistance < _minDragDistance) return; // too short, ignore

float t = (dragDistance - _minDragDistance) / (_maxDragDistance - _minDragDistance);
float force = Mathf.Lerp(_minLaunchForce, _maxLaunchForce, t);
Vector2 direction = dragVector.normalized;
```

---

## 3. Rocket.cs

### Serialized Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `_groundTag` | string | "Ground" | Tag for ground detection |
| `_targetTag` | string | "Target" | Tag for target detection |

### Private Fields

| Field | Type | Description |
|---|---|---|
| `_rb` | Rigidbody2D | Cached rigidbody |
| `_isFlying` | bool | True after launch, false after landing |

### Public Events

| Event | Type | When Fired |
|---|---|---|
| `OnRocketLaunched` | `Action` | Immediately after force applied |
| `OnRocketLanded` | `Action` | When hitting ground collider |
| `OnTargetHit` | `Action` | When entering target trigger |

### Public Methods

| Method | Signature | Description |
|---|---|---|
| `Launch` | `void Launch(Vector2 direction, float force)` | Apply impulse force, set isFlying, fire OnRocketLaunched |
| `ResetToPosition` | `void ResetToPosition(Vector2 position)` | Move to spawn, zero velocity, set kinematic, isFlying=false |

### Private Methods

| Method | Description |
|---|---|
| `FixedUpdate()` | If flying: rotate sprite to face velocity direction |
| `OnCollisionEnter2D(Collision2D)` | If ground: stop, fire OnRocketLanded |
| `OnTriggerEnter2D(Collider2D)` | If target: fire OnTargetHit |
| `RotateToVelocity()` | `angle = Atan2(rb.velocity.y, rb.velocity.x) * Rad2Deg - 90f` |

### Physics Setup (on GameObject)

| Component | Property | Value |
|---|---|---|
| Rigidbody2D | Body Type | Kinematic (initial) → Dynamic (on launch) |
| Rigidbody2D | Gravity Scale | 1 |
| Rigidbody2D | Mass | 1 |
| Rigidbody2D | Linear Drag | 0 |
| Rigidbody2D | Angular Drag | 0 |
| Rigidbody2D | Collision Detection | Continuous |
| CircleCollider2D | Radius | 0.15 |
| CircleCollider2D | Offset | (0, 0.5) — at nose tip |

### Launch Implementation

```csharp
public void Launch(Vector2 direction, float force)
{
    _rb.bodyType = RigidbodyType2D.Dynamic;
    _rb.AddForce(direction * force, ForceMode2D.Impulse);
    _isFlying = true;
    OnRocketLaunched?.Invoke();
}
```

### Rotation Implementation

```csharp
// -90f because rocket sprite points UP by default
float angle = Mathf.Atan2(_rb.velocity.y, _rb.velocity.x) * Mathf.Rad2Deg - 90f;
transform.rotation = Quaternion.Euler(0f, 0f, angle);
```

---

## 4. CameraController.cs

### Enum

```csharp
public enum CameraState { Idle, Aiming, Following, Landed, Returning }
```

### Serialized Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `_rocket` | Rocket | — | Follow target |
| `_vehicleTransform` | Transform | — | Return target |
| `_followSmoothTime` | float | 0.3f | SmoothDamp time for following |
| `_returnSmoothTime` | float | 0.5f | SmoothDamp time for returning |
| `_followOffsetY` | float | 2f | Look-ahead offset above rocket |
| `_returnThreshold` | float | 0.1f | Distance to vehicle considered "arrived" |

### Private Fields

| Field | Type | Description |
|---|---|---|
| `_currentState` | CameraState | Current camera state |
| `_velocity` | Vector2 | SmoothDamp velocity ref |
| `_defaultZ` | float | Camera Z position (preserved) |

### Public Methods

| Method | Signature | Description |
|---|---|---|
| `SetState` | `void SetState(CameraState state)` | Change camera state |
| `ReturnToVehicle` | `void ReturnToVehicle()` | Start returning to vehicle |

### Private Methods

| Method | Description |
|---|---|
| `LateUpdate()` | State-based camera position update |
| `FollowRocket()` | SmoothDamp to rocket position + offsetY |
| `ReturnToVehiclePosition()` | SmoothDamp to vehicle, when arrived → set Idle |

### Follow Logic

```csharp
// In LateUpdate, when state == Following:
Vector2 targetPos = (Vector2)_rocket.transform.position + new Vector2(0, _followOffsetY);
Vector2 smoothed = Vector2.SmoothDamp(transform.position, targetPos, ref _velocity, _followSmoothTime);
transform.position = new Vector3(smoothed.x, smoothed.y, _defaultZ);
```

---

## 5. AimArrow.cs

### Serialized Fields

| Field | Type | Default | Description |
|---|---|---|---|
| `_spriteRenderer` | SpriteRenderer | — | Arrow sprite renderer |
| `_minScale` | float | 0.5f | Arrow length at min drag |
| `_maxScale` | float | 2.0f | Arrow length at max drag |
| `_color` | Color | White (alpha 0.7) | Arrow color |

### Public Methods

| Method | Signature | Description |
|---|---|---|
| `Show` | `void Show()` | Enable sprite renderer |
| `Hide` | `void Hide()` | Disable sprite renderer |
| `UpdateArrow` | `void UpdateArrow(Vector2 direction, float normalizedForce)` | Set rotation & scale |

### UpdateArrow Logic

```csharp
public void UpdateArrow(Vector2 direction, float normalizedForce)
{
    // Rotate to face launch direction
    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
    transform.rotation = Quaternion.Euler(0f, 0f, angle);

    // Scale length by force
    float scaleY = Mathf.Lerp(_minScale, _maxScale, normalizedForce);
    transform.localScale = new Vector3(1f, scaleY, 1f);
}
```
