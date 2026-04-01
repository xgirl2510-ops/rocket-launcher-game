# Project Overview - Rocket Launcher

## Product Definition

| Field | Value |
|---|---|
| **Project Name** | Rocket Launcher |
| **Genre** | Physics / Projectile / Casual |
| **Platform** | Mobile (iOS & Android) |
| **Engine** | Unity 2022.3+ LTS |
| **Render Pipeline** | Built-in 2D |
| **Orientation** | Portrait (1080×1920, 9:16) |
| **Target Audience** | Casual players |
| **Current Phase** | Prototype (Phase 1 - Placeholder shapes) |

## Project Goal

Build a single-level slingshot-style rocket launcher game. Player drags back on a vehicle to aim and launch a rocket at a target. No scoring, no lives limit — just hit the target to win.

## Tech Stack

| Component | Technology |
|---|---|
| Engine | Unity 2022.3+ LTS |
| Language | C# |
| Physics | Unity 2D Physics (Rigidbody2D, Collider2D) |
| Input | Unity Touch Input (Input.GetTouch / Input.mousePosition) |
| UI | Unity UI (Canvas + TextMeshPro) |
| Version Control | Git |
| IDE | Visual Studio / Rider |

## Build Targets

| Platform | Min Version |
|---|---|
| iOS | 14.0+ |
| Android | API 24+ (Android 7.0) |
| Editor | Unity 2022.3 LTS |

## Dependencies

- **TextMeshPro** (included in Unity): For UI text rendering
- No third-party plugins required for prototype

## Key Constraints

- Single scene only (`GameScene`)
- No scoring system
- No level progression
- Unlimited shots until player hits target
- No explosion effects (prototype phase)
- No sound effects (prototype phase)
- No trajectory preview — player relies on feel

## Success Criteria (Prototype)

- [ ] Slingshot mechanic works (touch → drag → release → launch)
- [ ] Rocket flies in parabolic arc with physics
- [ ] Rocket rotates to face velocity direction
- [ ] Camera follows rocket smoothly
- [ ] Target detection triggers WIN state
- [ ] Miss triggers auto-reset and camera return
- [ ] Playable on mobile device in portrait mode
