# Project Changelog - Rocket Launcher Game

Detailed record of all significant changes, features, fixes từ initial commit → current.

Format: [Semantic Versioning](https://semver.org/) + [Conventional Commits](https://www.conventionalcommits.org/)

---

## [v1.0.1] - 2026-04-08

Checklist compliance pass + iOS TestFlight configuration.

### Added

**iOS Build Configuration**
- TestFlight build settings configured (commit 257adef)
- iOS platform support in ProjectSettings

### Changed

**Physics2D**
- Collision matrix properly configured for Rocket/Obstacle layers (commit 09492af)
- Removed runtime `GameObject.Find()` calls — use serialized refs or `transform.Find()` (commit 09492af)

**Code Quality**
- XML documentation on all public members in GameConstants (commit 53e65ce)
- `Debug.LogError` wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` guards (commit 53e65ce)
- Checklist compliance: split long methods, extract constants, remove Find (commit efcfd1c)
- Null guard consistency: fail-fast pattern, `RocketLayer` constant (commit a26f0f9)

### Fixed

- Crater visibility: fallback Find when ground ref not wired in scene (commit 1a998b7)

---

## [v1.0] - 2026-04-06

Production release. Code quality 9.0-9.5/10, 61 unit tests, CI pipeline complete.

### Added

**Code Quality & Tests**
- 61 unit tests across 7 test files (game-constants, game-round-tracker, ground-scorch, obstacle-spawner-trajectory, rocket-debris, rocket-physics, round-manager-state-transition)
- GitHub Actions CI pipeline: auto-run tests on push
- OnValidate editor-time validation hooks
- Type-safe C# event system (no string-based messaging)
- Comprehensive code documentation (codebase-summary, system-architecture, code-standards)

**Refactoring & Modularization**
- Split `RoundManager` god class → 3 partial files: RoundManager.cs (core), round-manager-auto-play-restart-and-target.cs, round-manager-hud.cs
- Split `SceneSetupTool` → 4 partial files: main entry, environment objects, sprite/material helpers, UI canvas
- Extracted `RuntimeSpriteFactory`: centralized sprite/material caching (prevent GPU leaks)
- Extracted `PanCoroutine` DRY helper: reusable camera panning (easing, duration, callback)
- Extracted `GameRoundTracker`: stateless data holder (plain C#, no MonoBehaviour)
- Extracted `ScreenShakeComponent`: decoupled from CameraController

**Physics & Trajectory**
- Fixed trajectory math: parabolic solver for obstacle-safe spawn
- High-angle obstacle spawner: spawn above/below safe zone
- Rocket velocity-based rotation: rb.rotation = atan2(velocity)
- Collision overlap circle detection: accurate impact point

**Memory & Performance**
- Texture cache via RuntimeSpriteFactory (zero GPU leaks)
- Coroutine cancellation: StopAllCoroutines on destroy
- Rigidbody persistence: prevent domain reload corruption
- Static leak fixes: proper cleanup in OnDestroy
- Max debris cap: 100 pieces (prevent memory explosion)
- Max scorch marks: 20 craters (prevent GPU vertex overload)

### Changed

**Constants & Magic Numbers**
- All magic numbers → GameConstants.cs SSOT
- Force range: [5, 30] (MinLaunchForce, MaxLaunchForce)
- Ground Y: -5 (GroundTop constant)
- Crater threshold: 5 units
- Target spawn range: X(8-35), Y(-4 to 10)
- Obstacle safe buffer: 3 units

**Layer & Tag Structure**
- Rocket layer: 8 (custom user layer, not built-in)
- Obstacles layer: 9 (separate physics check)
- Tags: Ground, Target, Vehicle, Rocket (immutable enum-style)

**Audio System**
- Differentiated ground vs target hit audio
- Graceful fallback: no crash if mp3 clip missing
- ProceduralAudioClipGenerator: 44100Hz waveform synthesis
- Audio ducking: thrust loop cancels on stop

**Camera Behavior**
- PanCoroutine: shared easing + duration + callback
- Multi-state machine: Intro→Idle→Following→Returning→LookingAtTarget
- Dynamic zoom: based on rocket distance
- Return-to-vehicle: smooth interpolation (not instant snap)

**Editor Tools**
- SerializedObject wiring: safe access to private [SerializeField] fields
- Inactive GO lookup: transform.Find() not GameObject.Find()
- Asset factory: generate sprites if missing (fallback graceful)

### Fixed

**Critical Bugs (commit 2290cfd)**
- Texture leak: RuntimeSpriteFactory cache (shared instances)
- Coroutine race: StopAllCoroutines on entity destroy
- Dead code: unused variables, commented-out methods
- Domain reload corruption: static field initialization guards

**Layer Regression (commit 2f6b7ff)**
- Rocket layer mismatch: explicitly use layer 8
- Obstacle layer isolation: layer 9 separate from rocket detection
- Physics collision: correct LayerMask in overlap checks

**Auto-Play Force Clamp (commit 5c475bc)**
- Force clamping: [MinLaunchForce, MaxLaunchForce] bounds
- Material cleanup: reuse cached materials (not create per frame)
- DRY camera pans: PanCoroutine shared helper
- Constants dedup: GameConstants SSOT

**Trajectory Math (commit 2f6b7ff)**
- Parabolic trajectory solver: correct obstacle avoidance
- Safe zone calculation: proper clearance buffer
- High-angle spawn: reliable above/below safe zone placement

**Audio Fallback (commit 2f6b7ff)**
- Missing clip handling: don't throw NullReferenceException
- Graceful degradation: game playable without mp3 assets
- Procedural fallback: synthetic sounds if clip not found

**Debris Memory (commit 2290cfd)**
- Debris cleanup: auto-destroy after lifetime
- Registry tracking: prevent unbounded lists
- Crater ground Y: accurate debris landing height

**Screen Shake (commit 78ef239)**
- Extracted to decoupled component
- Side-effect isolation: not in CameraController
- Duration/magnitude tuning: independent from camera logic

---

## [v0.9] - 2026-03-28

Feature-complete. All core gameplay implemented, UI hints added.

### Added

**UI & UX Enhancements**
- Hint system: angle/force tips after 5 misses
- Stats UI: shots/round, total shots, best round score
- Win text display with restart button
- "Look Target" button: camera pans to target
- Visual aim arrow: scale + rotate by force magnitude

**Round Tracking**
- GameRoundTracker: plain C# data holder
- Round counter, shot counter, best score tracking
- HUD stats update on each round

### Fixed

**HUD State Management**
- Compact canvas layout (no overlapping UI)
- Button responsiveness: restart/look-target interactions
- Win text visibility: proper lifetime management

---

## [v0.8] - 2026-03-15

Obstacles & challenges implemented. Auto-play demo added.

### Added

**Obstacle System**
- Random spawning: 2-4 obstacles per round
- Safe trajectory solver: avoid player's parabolic path
- High-angle obstacle placement: avoid blocking first attempt
- Dynamic difficulty: obstacle positions vary per round

**Auto-Play Demo**
- Automatic restart loop: 2-5s repeat
- Demo mode: shows successful launch sequences
- User interrupt: tap to override auto-play

### Fixed

**Trajectory Calculations**
- Parabolic path prediction: accurate safety zone
- Obstacle avoidance: reliable high-angle placement
- Force-angle coupling: correct physics simulation

---

## [v0.7] - 2026-02-25

Audio system complete. All sound events integrated.

### Added

**Audio Manager & Sounds**
- Singleton AudioManager: lazy initialization, graceful fallback
- MP3 audio: launch whoosh, thrust loop, hit/boom effects
- Procedural synthesis: UI sounds (win jingle, stretch, click, target hit)
- 44100Hz waveform generation: sine/square/sawtooth oscillators
- Audio differentiation: ground hit vs target hit unique sounds
- Thrust loop control: start on launch, stop on land

### Fixed

**Audio Integration**
- Event subscription: Rocket.OnRocketLaunched → PlayLaunch()
- Thrust management: StopThrust() on landing
- Win audio: PlayWin() on target hit
- Fallback handling: no crash if mp3 clip missing

---

## [v0.6] - 2026-02-10

Particle effects & visual polish complete.

### Added

**Particle & Visual Effects**
- Explosion burst: gold particles on target hit, grey on miss
- Rocket debris shatter: physics-driven pieces on impact
- Crater holes: SpriteMask-based ground damage
- Rocket trail: red→orange→grey smoke gradient during flight
- Screen shake: magnitude + duration on impact

**Debris System**
- Manual rigidbody debris: realistic physics behavior
- Crater-aware landing: GetGroundY() from GroundScorch for accurate positioning
- Auto-cleanup: lifetime-based destruction
- Registry tracking: prevent unbounded debris lists

**Ground Scorch**
- SpriteMask crater holes: visual damage indicators
- Depth scaling: crater size based on flight height
- Max crater limit: 20 marks per round (prevent GPU overload)
- ClearAll() cleanup: reset on round restart

### Changed

**Rocket Trail Color Gradient**
- Near rocket (0%): red #FF0000
- Mid-trail (50%): orange #FF8800
- Tail (100%): dark grey #333333

### Fixed

**Particle Cleanup**
- Trail destroy: Destroy() on rocket land
- Debris lifetime: auto-remove after timeout
- Burst particles: one-shot destroy after duration
- Scorch marks: ClearAll() on round restart

---

## [v0.5] - 2026-01-20

Core mechanics polished. Camera, round flow, restart logic complete.

### Added

**Camera System**
- Multi-state camera: Intro→Idle→Following→Returning→LookingAtTarget
- Intro pan: initial camera positioning
- Rocket follow: dynamic zoom based on distance
- Return-to-vehicle: smooth interpolation back to start
- Look target: camera pan to target on button press
- Events: OnIntroComplete, OnLookTargetComplete

**Round Flow**
- Miss detection: rocket below ground threshold
- Hit detection: rocket touches target collider
- Reload: reset rocket position for next shot
- Restart: full round reset (round++, target respawn)
- Auto-play demo: 2-5s loop for unattended gameplay

**HUD Basics**
- Win text display
- Restart button
- Shots counter
- Round counter

### Changed

**Physics Behavior**
- Interpolation enabled: smooth camera tracking
- Velocity-based rotation: rocket faces travel direction
- Collision overlap: accurate impact detection

### Fixed

**Camera Movement**
- Smooth return: Lerp interpolation (not instant)
- Pan coroutine: shared DRY helper (easing, duration, callback)
- Follow lag: Rigidbody2D interpolation removes jank
- Target pan: accurate camera positioning

**Round Reset**
- Rocket position: reset to vehicle
- Target spawn: randomize position each round
- Physics state: velocity = 0 on reload

---

## [v0.4] - 2026-01-05

Slingshot input, rocket physics, win/miss detection complete.

### Added

**Launch System**
- Slingshot input: drag to aim, release to launch
- Force scaling: release distance → impulse magnitude
- Angle calculation: drag direction → rotation
- Visual feedback: aim arrow (rotates + scales by force)
- Force bounds: clamp to [MinLaunchForce, MaxLaunchForce]

**Rocket Physics**
- Rigidbody2D component: gravity-driven trajectory
- Impulse launch: AddForce(force, angle) on release
- Velocity-based rotation: rb.MoveRotation(atan2(velocity))
- Collision detection: overlap circle at rocket position
- Physical properties: drag, mass, gravity scale

**Win/Miss Detection**
- Win: rocket collides with target (overlap)
- Miss: rocket falls below ground threshold (GroundTop = -5)
- Event firing: OnRocketLaunched, OnRocketLanded, OnTargetHit
- Round completion: trigger UI + camera transitions

### Changed

**Vehicle Positioning**
- Spawn point: left side of ground
- Slingshot origin: center of vehicle sprite
- Launch direction: angle from vehicle forward

---

## [v0.3] - 2025-12-20

Editor scene auto-setup tool. Asset generation.

### Added

**Editor Tools**
- Scene setup tool: Tools > Rocket Launcher > Setup Scene
- Auto-instantiate: Ground, Target, Vehicle, Rocket, Camera, Canvas
- Asset factory: generate white sprites (circle, square 100x100)
- Reference wiring: SerializedObject for private fields
- Validation: CheckReferences, DebugLog on setup complete

**Generated Assets**
- White circle sprite: 100x100 pixels, procedural texture
- White square sprite: 100x100 pixels, procedural texture
- Default material: Sprites/Default shader
- Sprite caching: prevent duplicate asset creation

**Hierarchy Setup**
- Ground quad: positioned at Y=-5
- Target sprite: randomized X position
- Vehicle sprite: left side, ready for launch
- Rocket rigidbody: child of vehicle, layer 8
- Camera: configured for follow
- Canvas: RenderMode ScreenSpaceOverlay

---

## [v0.2] - 2025-12-10

Project scaffolding. Core namespace, assembly definitions, test framework.

### Added

**Project Structure**
- Assembly definitions: RocketLauncher.Runtime, RocketLauncher.Editor, RocketLauncher.Tests.Editor
- Namespace: RocketLauncher (all scripts)
- Folder structure: Assets/Scripts/{Audio,Camera,Core,Effects,Launch,Obstacles,Rocket}, Assets/Editor, Assets/Tests/Editor

**Test Framework**
- NUnit integration
- Test fixtures: GameRoundTrackerTests, RocketPhysicsTests, etc.
- Test assembly: RocketLauncher.Tests.Editor

**GitHub & CI**
- Repository: https://github.com/xgirl2510-ops/rocket-launcher-game
- GitHub Actions: auto-run tests on push
- .gitignore: exclude Library/, Builds/, *.log

---

## [v0.1] - 2025-12-01

Project initialization. Unity 6 (6000.4.0f1) 2D scene created.

### Added

**Initial Setup**
- Unity version: 6000.4.0f1
- 2D rendering pipeline
- Default scene: Rocket Launcher
- Basic ground quad, target sprite, rocket prefab
- Camera main configured
- Physics2D settings: gravity, default material

### Notes

- Project created from 2D template
- Physics2D enabled for rocket trajectory
- Sprite sorting configured for layer-based rendering
- Input manager configured for launch controls

---

## Unresolved Questions

- Object pooling strategy: defer until performance bottleneck
- Input System migration: defer for mobile phase
- Audio mixer groups: current flat hierarchy sufficient?
- ScriptableObject config: priority for v2.0 content expansion?

