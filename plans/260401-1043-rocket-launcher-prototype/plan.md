# Rocket Launcher Prototype - Implementation Plan

## Overview
Build a complete slingshot-style rocket launcher game using **placeholder shapes only**. Each phase produces a testable build in Unity. Assets replaced later.

## Reference Docs
- [Game Design Document](../../docs/game-design-document.md)
- [Tech Specs Per Script](../../docs/unity-technical-specifications-per-script.md)
- [Scene Setup Guide](../../docs/unity-scene-setup-and-hierarchy-guide.md)
- [System Architecture](../../docs/unity-system-architecture-and-script-communication.md)
- [Code Standards](../../docs/unity-code-standards-and-conventions.md)

## Phases

| # | Phase | Scripts | Status |
|---|-------|---------|--------|
| 1 | [Project & Scene Setup](phase-01-project-and-scene-setup.md) | None (visual only) | Pending |
| 2 | [Rocket Physics](phase-02-rocket-physics.md) | `Rocket.cs` | Pending |
| 3 | [Slingshot Input & Aim](phase-03-slingshot-input-and-aim-arrow.md) | `LaunchController.cs`, `AimArrow.cs` | Pending |
| 4 | [Camera System](phase-04-camera-follow-system.md) | `CameraController.cs` | Pending |
| 5 | [GameManager & UI](phase-05-game-manager-ui-and-full-loop.md) | `GameManager.cs` | Pending |
| 6 | [Polish & Mobile](phase-06-polish-tuning-and-mobile-testing.md) | Tuning only | Pending |

## Approach
- **Phase 1**: Tạo project, scene, tất cả GameObjects bằng hình khối → nhấn Play thấy đúng layout
- **Phase 2**: Rocket.cs → test bắn hardcoded, rocket bay vòng cung, xoay theo hướng, chạm đất dừng
- **Phase 3**: LaunchController + AimArrow → kéo thả trên xe để bắn rocket thật
- **Phase 4**: CameraController → camera follow rocket, quay về xe khi miss
- **Phase 5**: GameManager + UI → full game loop: WIN/MISS/RESTART
- **Phase 6**: Tinh chỉnh physics, camera feel, test mobile

## Key Principle
> Code đúng trước → test kỹ → mới qua phase sau. Asset thay sau khi game hoàn chỉnh.
