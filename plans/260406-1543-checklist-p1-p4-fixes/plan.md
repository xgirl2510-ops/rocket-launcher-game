# Plan: Fix Checklist Violations P1-P4

**Date:** 2026-04-06
**Branch:** main
**Status:** Draft

---

## Overview

Fix 4 priority violations from Unity Code Checklist audit (sections 1-4, 5-8).
Skip YAGNI items (interfaces, ScriptableObjects, SO event channels, object pooling).

## Phases

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Split 6 long methods (>30 lines) | Pending |
| 2 | Extract ~10 magic numbers → named constants | Pending |
| 3 | Add XML docs for ~50 public members | Pending |
| 4 | Remove `GameObject.Find` from GroundScorch | Pending |

---

## Phase 1: Split Long Methods (P1)

**6 methods >30 lines cần tách:**

### 1.1 `CreateTrailParticleSystem()` — ~54 lines
- **File:** `Assets/Scripts/Effects/rocket-trail-particle-effect.cs:50`
- **Split:** ConfigureTrailMain() + ConfigureTrailEmission() + ConfigureTrailColorOverLifetime() + ConfigureTrailSizeOverLifetime()

### 1.2 `CalculateTrajectory()` — ~49 lines
- **File:** `Assets/Scripts/Obstacles/ObstacleSpawner.cs:63`
- **Split:** SolveOptimalAngle() + SampleTrajectoryPoints()

### 1.3 `ConfigureParticleSystem()` — ~46 lines
- **File:** `Assets/Scripts/Effects/explosion-burst-particle-effect.cs:55`
- **Split:** ConfigureExplosionMain() + ConfigureExplosionEmission() + ConfigureExplosionColorOverLifetime()

### 1.4 `GroundScorch.Spawn()` — ~46 lines
- **File:** `Assets/Scripts/Effects/ground-scorch-mark.cs:67`
- **Split:** CalculateCraterScale() + CreateCraterGameObject() + RegisterCrater()

### 1.5 `HandleRestart()` — ~36 lines
- **File:** `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs:12`
- **Split:** ResetGameState() + PrepareNewRound()

### 1.6 `SpawnObstaclesAvoidingTrajectory()` — ~35 lines
- **File:** `Assets/Scripts/Obstacles/ObstacleSpawner.cs:114`
- **Split:** GenerateRandomPosition() + IsPositionValid()

**File ownership (dev-backend):**
- Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs
- Assets/Scripts/Obstacles/ObstacleSpawner.cs

**File ownership (dev-frontend):**
- Assets/Scripts/Effects/rocket-trail-particle-effect.cs
- Assets/Scripts/Effects/explosion-burst-particle-effect.cs
- Assets/Scripts/Effects/ground-scorch-mark.cs

---

## Phase 2: Extract Magic Numbers (P3)

**~10 unnamed numeric literals → named constants:**

| File | Line | Value | Proposed Name |
|------|------|-------|---------------|
| AudioManager.cs | 70 | `1.3f` | `TargetHitPitchMultiplier` |
| RocketDebris.cs | 90 | `15f, 165f` | `MinDebrisAngle, MaxDebrisAngle` |
| RocketDebris.cs | 124 | `2f` | `DebrisLifetime` |
| GroundScorch.cs | 76-81 | `15f, 30f` | `SmallCraterThreshold, LargeCraterThreshold` |
| GroundScorch.cs | 76-81 | `0.8f-2.5f` | `MinCraterScale, MaxCraterScale` |
| ObstacleSpawner.cs | 121 | `20` | `MaxSpawnAttemptsPerObstacle` |
| Rocket.cs | 88 | `0.01f` | `MinVelocitySqr` |
| Multiple | various | `-90f` | `SpriteAngleOffset` (trong GameConstants) |

**File ownership (dev-backend):**
- AudioManager.cs, Rocket.cs, ObstacleSpawner.cs

**File ownership (dev-frontend):**
- RocketDebris.cs, GroundScorch.cs

---

## Phase 3: XML Docs (P2)

**~50 public members cần `/// <summary>`:**

| File | Count | Members |
|------|-------|---------|
| AudioManager.cs | 10 | Instance, PlayLaunch, PlayHitGround, PlayHitTarget, PlayStretch, PlayClick, PlayWin, StartThrust, StopThrust |
| RoundManagerHUD.cs | 8 | Instance, ShowWinUI, HideWinUI, ShowHints, HideHints, HideAutoPlayButton, UpdateHintTexts, UpdateStatsUI |
| Rocket.cs | 6 | OnRocketLaunched, OnRocketLanded, OnTargetHit, OnImpact, IsFlying, Launch, ResetToPosition |
| CameraController.cs | 6 | CameraState, OnIntroComplete, OnLookTargetComplete, PlayIntro, ReturnToVehicle, PanToTarget, Shake |
| GroundScorch.cs | 4 | Spawn, GetGroundY, ClearAll, CraterData fields |
| RocketDebris.cs | 4 | Spawn, SpawnDirtDebris, SpawnTargetDebris, ClearAll |
| ProceduralAudioClipGenerator.cs | 5 | CreateGroundHit, CreateWinJingle, CreateStretch, CreateTargetHit, CreateClick |
| RocketTrail.cs | 3 | StartTrail, StopTrail, ClearTrail |
| ExplosionEffect.cs | 1 | Spawn |
| AimArrow.cs | 3 | Show, Hide, UpdateArrow |
| LaunchController.cs | 3 | RotateRocketToDirection, EnableInput, DisableInput |
| RoundManager.cs | 1 | OnShotFired |
| GameRoundTracker.cs | 3 | RoundShots, RoundNumber, BestScore |
| RuntimeSpriteFactory.cs | 2 | GetSolidSprite, GetParticleMaterial |
| round-manager-auto-play.cs | 3 | HandleRestart, HandleAutoPlay, HandleLookTarget |
| CameraScreenShake.cs | 1 | Shake |

**File ownership (dev-backend):** AudioManager, RoundManager*, LaunchController, GameRoundTracker, RuntimeSpriteFactory, ProceduralAudioClipGenerator, Rocket, round-manager-auto-play
**File ownership (dev-frontend):** CameraController, CameraScreenShake, GroundScorch, RocketDebris, RocketTrail, ExplosionEffect, AimArrow, RoundManagerHUD

---

## Phase 4: Remove GameObject.Find (P4)

**File:** `Assets/Scripts/Effects/ground-scorch-mark.cs:58`
```csharp
var ground = GameObject.Find(GameConstants.GroundObjectName);
```

**Fix:**
- Change `PrepareGround()` → `PrepareGround(Transform ground)`
- Remove `GameObject.Find` call
- Update caller (likely `ImpactEffectsHandler` or `RoundManager`) to pass ground ref
- Add `[SerializeField] private Transform _ground;` to caller
- Wire in editor setup tool

**Files affected:**
- ground-scorch-mark.cs (change signature)
- Caller file (add field + pass ref)
- Editor setup tool (wire the reference)

---

## Team Split

| Dev | Files | Tasks |
|-----|-------|-------|
| **dev-backend** | AudioManager, Rocket, ObstacleSpawner, RoundManager*, LaunchController, GameRoundTracker, RuntimeSpriteFactory, ProceduralAudioClipGenerator | Phase 1 (1.2, 1.5, 1.6) + Phase 2 (backend files) + Phase 3 (backend docs) |
| **dev-frontend** | CameraController, CameraScreenShake, GroundScorch, RocketDebris, RocketTrail, ExplosionEffect, AimArrow, RoundManagerHUD, ImpactEffectsHandler, Editor tools | Phase 1 (1.1, 1.3, 1.4) + Phase 2 (frontend files) + Phase 3 (frontend docs) + Phase 4 |

**No file overlap** — parallel execution safe.

---

## Success Criteria

- [ ] Zero methods >30 lines
- [ ] Zero unnamed magic numbers in gameplay code
- [ ] All public members have XML doc comments
- [ ] No `GameObject.Find` in runtime code
- [ ] All existing tests still pass
- [ ] No behavior changes — restructure only
