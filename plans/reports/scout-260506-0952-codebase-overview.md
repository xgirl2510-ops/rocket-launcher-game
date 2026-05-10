# Rocket Launcher Project — Codebase Scout Overview
**Date:** 2026-05-06 | **Scope:** Very Thorough | **Quality:** 9.5/10

---

## 1. PROJECT STRUCTURE

```
/Users/Luke/Downloads/Programming/Game/
├── Assets/
│   ├── Editor/           (4 editor tools, 1 assembly)
│   ├── Scripts/          (19 runtime scripts, 2,584 LOC, 1 assembly)
│   ├── Tests/            (7 test files, 889 LOC, 1 assembly)
│   ├── Sprites/Generated/ (5 generated assets: rocket, car, ground, circles, squares)
│   ├── Scenes/           (1 main scene: GameScene.unity)
│   ├── Audio/            (3 MP3 files: launch, flight, boom)
│   └── _Recovery/        (2 backup scenes)
│
├── Packages/             (manifest.json: 43 dependencies including GoogleMobileAds)
├── ProjectSettings/      (Unity project config, iOS/Android build settings)
├── docs/                 (13 markdown docs)
├── plans/                (4 plan directories + reports folder)
├── .github/workflows/    (unity-ci-build-and-test.yml)
└── .git/                 (15 commits, active development)
```

---

## 2. RUNTIME C# SCRIPTS (Assets/Scripts/)

**Namespace:** `RocketLauncher` | **Assembly:** `RocketLauncher.Runtime.asmdef`

### Core (6 files, ~485 LOC)
| File | Purpose |
|------|---------|
| `RoundManager.cs` | Main orchestrator—manages round flow, miss/hit logic, reload, restart, auto-play |
| `round-manager-auto-play-restart-and-target.cs` | Partial class—auto-play demo, restart button, target randomization |
| `round-manager-hud.cs` | HUD singleton—score display, hints, UI callbacks |
| `GameRoundTracker.cs` | Stats tracker—persistent scores, hit/miss counts |
| `GameConstants.cs` | SSOT constants—layer indices, tags, force limits, sprite angle offset |
| `runtime-sprite-factory.cs` | Runtime sprite asset factory—generates circles/squares dynamically |

### Launch (2 files, ~219 LOC)
| File | Purpose |
|------|---------|
| `LaunchController.cs` | Touch input—drag slingshot, measure force, apply velocity |
| `AimArrow.cs` | Visual feedback—shows aim direction & magnitude |

### Rocket (1 file, ~129 LOC)
| File | Purpose |
|------|---------|
| `Rocket.cs` | Physics controller—Rigidbody2D, velocity tracking, ground/obstacle collision events |

### Camera (2 files, ~305 LOC)
| File | Purpose |
|------|---------|
| `CameraController.cs` | Camera state machine—follow rocket, dynamic zoom, pan to target |
| `camera-screen-shake.cs` | Impact feedback—VFX screen shake on collisions |

### Effects (5 files, ~576 LOC)
| File | Purpose |
|------|---------|
| `explosion-burst-particle-effect.cs` | Particle burst on impact—configurable color, lifetime |
| `ground-scorch-mark.cs` | SpriteMask craters—scorch marks on ground, height-based threshold |
| `impact-effects-handler.cs` | Decoupler—triggers all effects on collision (explosion, scorch, debris) |
| `rocket-debris-shatter-effect.cs` | Debris particles—shatters rocket on obstacles |
| `rocket-trail-particle-effect.cs` | Flight trail—particle trail behind rocket during flight |

### Audio (2 files, ~235 LOC)
| File | Purpose |
|------|---------|
| `AudioManager.cs` | Singleton—play/stop SFX, pitch variation, audio source pooling |
| `ProceduralAudioClipGenerator.cs` | Synthesis—procedural explosion, launch, impact clips |

### Obstacles (1 file, ~200 LOC)
| File | Purpose |
|------|---------|
| `ObstacleSpawner.cs` | Obstacle spawner—safe trajectory calc, spawn placement, cleanup |

### Ads (1 file, ~100 LOC)
| File | Purpose |
|------|---------|
| `AdManager.cs` | AdMob integration—interstitial & banner ads, restart flow |

---

## 3. TEST FILES (Assets/Tests/Editor/)

**Assembly:** `RocketLauncher.Tests.Editor.asmdef` | **Total:** 7 files, 889 LOC

| Test File | Coverage |
|-----------|----------|
| `rocket-physics-tests.cs` | Rigidbody velocity, force application, trajectory |
| `game-constants-validation-tests.cs` | Constants correctness, tag/layer assignments |
| `rocket-debris-spawn-and-cleanup-tests.cs` | Debris instantiation, pooling, cleanup on restart |
| `game-round-tracker-tests.cs` | Score tracking, hit/miss stats, persistence |
| `round-manager-state-transition-tests.cs` | State flow: launch → flight → impact → reload |
| `obstacle-spawner-trajectory-tests.cs` | Safe spawn zones, collision prediction |
| `ground-scorch-tests.cs` | Crater spawning, height thresholds, SpriteMask rendering |

---

## 4. EDITOR TOOLS (Assets/Editor/)

**Namespace:** `RocketLauncher.Editor` | **Assembly:** `RocketLauncher.Editor.asmdef`

All tools split as partial classes of `SceneSetupTool`:

| File | Exposes | Purpose |
|------|---------|---------|
| `rocket-launcher-scene-auto-setup-editor-tool.cs` | **Menu:** Tools > Rocket Launcher > Setup Scene | Master auto-setup tool—clears scene & rebuilds all GameObjects |
| `rocket-launcher-scene-setup-environment-and-gameplay-objects.cs` | Private (partial) | Creates Ground, Target, Camera, Rocket, ObstacleSpawner |
| `rocket-launcher-scene-setup-ui-canvas-and-hud-elements.cs` | Private (partial) | Creates UI Canvas, HUD panels, text elements |
| `rocket-launcher-scene-setup-shared-gameobject-and-sprite-helpers.cs` | Private (partial) | Helper methods for GameObject/sprite creation & wiring |

---

## 5. SCENES

| Scene Path | Purpose | Status |
|-----------|---------|--------|
| `Assets/Scenes/GameScene.unity` | Main gameplay scene—slingshot launcher, rocket, obstacles, target | Active |
| `Assets/_Recovery/0.unity` | Backup | Archive |
| `Assets/_Recovery/0 (1).unity` | Backup | Archive |

---

## 6. NAMESPACES & KEY PATTERNS

**Namespace Hierarchy:**
- `RocketLauncher` — Runtime scripts
- `RocketLauncher.Editor` — Editor-only tools
- (Tests use same namespace with `[TestFixture]` attributes)

**Design Patterns:**
- Singleton: `AudioManager`, `RoundManagerHUD`
- Partial classes: `RoundManager`, `SceneSetupTool` (split responsibilities)
- Observer: Rocket events (OnGround, OnObstacle, OnFlight)
- State machine: `CameraController` (Follow → Zoom → Pan → Ready)
- Factory: `RuntimeSpriteFactory` (circles, squares)

---

## 7. DEPENDENCIES (Packages/manifest.json)

**Key Packages:**
- `com.gamelovers.mcp-unity` (MCP integration for Claude Code)
- `com.unity.test-framework` 1.4.5 (Unit testing)
- `com.unity.textmeshpro` 3.2.0-pre.12 (UI text)
- `com.unity.2d.sprite` 1.0.0 (2D sprites)
- `com.unity.ugui` 2.0.0 (UI framework)
- **Full module suite:** Physics2D, Audio, Animation, Particles, etc.

**External (not in manifest):**
- GoogleMobileAds SDK (in Assets/)
- Audio: MP3 playback (built-in)

---

## 8. NOTABLE ASSETS

### Generated Sprites (Runtime)
- `circle-100x100.asset` — Dynamically created circles
- `square-100x100.asset` — Dynamically created squares
- `circle-100x100.png`, `square-100x100.png` — Source images

### Hand-Made Assets
- `rocket2.png` (858 KB) — Rocket sprite
- `car2.png` (143 KB) — Obstacle sprite
- `ground.png` (31 MB!) — Large ground tilemap texture

### Audio
- `rocket-start.mp3` — Launch SFX
- `rocket-flight.mp3` — Thrust loop
- `rocket-boom.mp3` — Ground impact

---

## 9. DOCUMENTATION (docs/ folder)

| File | Purpose |
|------|---------|
| `codebase-summary.md` | (17 KB) Codebase overview, file inventory, LOC breakdown |
| `project-overview-pdr.md` | (11 KB) Project design review—features, architecture, review notes |
| `unity-technical-specifications-per-script.md` | (17 KB) Detailed per-script API & serialized fields |
| `unity-system-architecture-and-script-communication.md` | (16 KB) System flow, event model, communication patterns |
| `development-roadmap.md` | (8.2 KB) Planned features, milestones, priority matrix |
| `project-changelog.md` | (14 KB) Version history, bug fixes, features added |
| `game-design-document.md` | (11 KB) GDD—mechanics, balance, art style |
| `art-style-guide.md` | (6.9 KB) Visual style, color palettes, asset guidelines |
| `unity-code-checklist.md` | (5.0 KB) Code standards checklist—XML docs, null guards, naming |
| `unity-code-standards-and-conventions.md` | (3.8 KB) Naming conventions, code style, best practices |
| `unity-scene-setup-and-hierarchy-guide.md` | (5.8 KB) Scene hierarchy, layer/tag setup, prefab structure |
| `tmux-claude-teams-setup-guide.md` | (6.0 KB) Team setup instructions for parallel development |
| `design-steps.md` | (3.0 KB) Design iteration process |
| **graphic/prompt/*** | AI art generation prompts (rocket, obstacles, ground, launcher, target, sky, scene mockup) |

---

## 10. PLANS & REPORTS (plans/ folder)

### Active Plan Directories
| Directory | Date | Purpose |
|-----------|------|---------|
| `260401-1043-rocket-launcher-prototype` | Apr 1 | Initial prototype planning |
| `260406-1543-checklist-p1-p4-fixes` | Apr 6 | Code review & compliance fixes |
| `260407-0949-fix-remaining-checklist` | Apr 7 | Final checklist resolution |
| `260408-1645-admob-interstitial-ads-integration` | Apr 8 | Ad integration work |

### Reports Archive (20+ completed reports)
- **Code Reviews:** Full codebase review, physics/input/camera, core flow, effects/audio, post-fix verification
- **Testing:** Test coverage analysis (61 tests)
- **Documentation:** Missing docs creation, updates
- **Research:** Vietnamese IME fixes, Google Mobile Ads SDK v9, CCPM analysis
- **Audits:** Full 10-of-10 audit, clean code reviews (frontend/backend)

---

## 11. RECENT ACTIVITY (Git Log, Last 15 Commits)

| Commit | Date | Focus |
|--------|------|-------|
| `257adef` | Apr 17 | iOS TestFlight build settings |
| `09492af` | Apr 17 | Physics2D collision matrix, remove GameObject.Find |
| `53e65ce` | Apr 17 | XML docs for constants, Debug guards |
| `1a998b7` | Apr 16 | Crater visibility fallback (Find) |
| `efcfd1c` | Apr 16 | Checklist compliance—split methods, XML docs, remove Find |
| `a26f0f9` | Apr 15 | Null guard consistency, RocketLayer constant |
| `d30e532` | Apr 15 | Audio pitch leak, GC allocs, OnValidate, Undo |
| `f1302df` | Apr 14 | Code review fixes—null guards, pitch bug, decoupled effects |
| `78ef239` | Apr 13 | Split god classes, trajectory math, extract screen shake |
| `2800d99` | Apr 13 | DRY sprite/material factory, unit tests, CI |

**Trend:** Quality-focused refactoring. Recent work: code standards compliance, iOS build, collision matrix, null safety.

---

## 12. CODE QUALITY METRICS

- **Total Runtime LOC:** 2,584 (Assets/Scripts/)
- **Test LOC:** 889 (Assets/Tests/)
- **Test Count:** 61 unit tests (across 7 test files)
- **Test Coverage:** Core gameplay (physics, state, stats), effects, audio, obstacles
- **Code Review Score:** 9.0–9.5/10 (per docs)
- **XML Documentation:** Comprehensive on all public APIs
- **Static Analysis:** Null guards, no magic numbers, layer/tag constants extracted
- **CI/CD:** GitHub Actions (build & test workflow)

---

## 13. ARCHITECTURE HIGHLIGHTS

**Decoupled Design:**
- `RoundManager` orchestrates; individual systems (Camera, Audio, Effects) handle own state
- Rocket emits events (OnGround, OnObstacle, OnFlight); listeners subscribe
- `ImpactEffectsHandler` decouples collision logic from effect spawning
- `RuntimeSpriteFactory` centralizes dynamic sprite generation

**Physics Safe:**
- `ObstacleSpawner` calculates safe trajectory before spawn
- Physics2D collision matrix configured in ProjectSettings
- Ground defined by constant `GameConstants.GroundTop` (SSOT)

**Mobile-Ready:**
- Touch input via `LaunchController`
- AdMob interstitial & banner ads
- iOS/Android build settings configured
- Low GC allocs (audio pooling, sprite reuse)

---

## 14. UNRESOLVED QUESTIONS / NOTES

1. **Ground texture size** — 31 MB ground.png is very large; consider atlas/tiling optimization
2. **GoogleMobileAds integration** — Recently added (Apr 8 plan); verify live ad serving not in test mode
3. **Sprite generation** — `RuntimeSpriteFactory` creates circles/squares at runtime; confirm performance profile
4. **Physics2D tweaks** — Recent collision matrix fix (09492af); test multi-obstacle scenarios
5. **iOS TestFlight** — Build settings just updated (257adef); confirm provisioning profile, ad ID setup

---

**Report Generated:** 2026-05-06 09:52 | **Scan Depth:** Complete | **Status:** Ready for feature development
