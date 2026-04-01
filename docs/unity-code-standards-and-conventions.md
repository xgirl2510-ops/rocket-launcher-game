# Unity Code Standards & Conventions

## C# Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Class | PascalCase | `LaunchController`, `GameManager` |
| Public method | PascalCase | `LaunchRocket()`, `ResetGame()` |
| Private method | PascalCase | `CalculateForce()` |
| Public field | camelCase | `launchForce`, `maxDragDistance` |
| Private field | _camelCase (underscore prefix) | `_rigidbody`, `_isAiming` |
| SerializedField | _camelCase with [SerializeField] | `[SerializeField] private float _launchForceMultiplier` |
| Constant | UPPER_SNAKE_CASE | `MAX_LAUNCH_FORCE` |
| Enum | PascalCase | `GameState.Playing` |
| Interface | IPascalCase | `ILaunchable` |
| Event | On + PascalCase | `OnRocketLanded`, `OnTargetHit` |

## Project Folder Structure

```
Assets/
├── Scenes/
│   └── GameScene.unity
├── Scripts/
│   ├── Core/
│   │   └── GameManager.cs
│   ├── Launch/
│   │   ├── LaunchController.cs
│   │   └── AimArrow.cs
│   ├── Rocket/
│   │   └── Rocket.cs
│   └── Camera/
│       └── CameraController.cs
├── Prefabs/
│   ├── Rocket.prefab
│   └── AimArrow.prefab
├── Materials/
│   └── (placeholder materials)
├── Sprites/
│   └── (placeholder sprites if needed)
└── UI/
    └── (UI assets)
```

## Coding Patterns

### Singleton (GameManager only)

```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
}
```

### State Machine (Camera, Game Flow)

Use enum-based state machines for simple state management:

```csharp
public enum CameraState { Idle, Aiming, Following, Landed, Returning }

private CameraState _currentState = CameraState.Idle;
```

### Event-Driven Communication

Use C# events/Actions for decoupled communication between scripts:

```csharp
// In Rocket.cs
public event Action OnRocketLanded;
public event Action OnTargetHit;

// In GameManager.cs — subscribe
rocket.OnRocketLanded += HandleRocketLanded;
```

## Coding Rules

1. **One class per file** — file name matches class name
2. **No God classes** — each script has a single responsibility
3. **[SerializeField] over public fields** — expose to Inspector without breaking encapsulation
4. **Cache component references** in `Awake()` or `Start()`
5. **Use `CompareTag()` instead of `==` for tag comparison** — better performance
6. **No `Find()` or `FindObjectOfType()` at runtime** — use Inspector references or events
7. **Physics in `FixedUpdate()`**, input in `Update()`
8. **Use `Vector2` for all 2D operations** — not `Vector3`

## Unity-Specific Guidelines

### Inspector Organization

- Group related fields with `[Header("Section Name")]`
- Add tooltips with `[Tooltip("Description")]`
- Use `[Range(min, max)]` for tunable values

```csharp
[Header("Launch Settings")]
[SerializeField, Range(5f, 20f)] private float _maxLaunchForce = 20f;
[SerializeField, Range(0.5f, 3f)] private float _maxDragDistance = 3f;
```

### Collision & Trigger Handling

- Ground: `OnCollisionEnter2D` (physics collision)
- Target: `OnTriggerEnter2D` (trigger detection, no physics response)

### Physics Settings

- Rigidbody2D: Use `ForceMode2D.Impulse` for one-time launch
- Gravity Scale: 1 (default)
- Collision Detection: Continuous for rocket (fast-moving object)

### Tag & Layer Setup

| Tag | Used By |
|---|---|
| `Player` | Rocket |
| `Target` | Target object |
| `Ground` | Ground collider |

| Layer | Used By |
|---|---|
| `Default` | Most objects |
| `Rocket` | Rocket (for selective collision if needed) |
| `UI` | UI elements |
