# Consolidated Code Review — Path to 10/10

**Date:** 2026-04-03 | **Agents:** 3 parallel reviewers | **Files:** 22 C# (~2,100 LOC)
**Current score:** 8.0–8.5/10

---

## Deduplicated Findings (30 unique, merged from 3 agents)

### BUGS (fix immediately)

| ID | Sev | File | Issue |
|----|-----|------|-------|
| B1 | **HIGH** | `round-manager-auto-play-restart-and-target.cs:49-73` | Auto-play after miss fires invisible rocket from wrong position. `StopAllCoroutines()` cancels reload, then `Launch()` without `ResetToPosition()` — rocket launches from impact spot with hidden sprites |
| B2 | **HIGH** | `rocket-debris-shatter-effect.cs:111-124` | Debris never self-destructs after grounding. `_allDebris` list grows unbounded until `ClearAll()`. Long miss streaks = memory leak + draw call pressure |
| B3 | **HIGH** | `ObstacleSpawner.cs:92-108` | Trajectory degenerate when target far right (theta→90°, vx→0). `totalTime` hits 250+ sec, safe-zone points cluster near launch site, obstacles spawn in flight path |
| B4 | **MEDIUM** | `ground-scorch-mark.cs:55` | `FindWithTag("Ground")` can return an obstacle (obstacles share `TagGround`). Crater mask wires to wrong renderer |
| B5 | **MEDIUM** | `round-manager-auto-play-restart-and-target.cs:112-124` | `ReloadAfterAutoPlay` skips `NewRound()` — HUD shows stale round/shots after auto-play |

### CODE QUALITY (should fix)

| ID | Sev | File | Issue |
|----|-----|------|-------|
| Q1 | **HIGH** | `camera-screen-shake.cs:28-33` | `GetOffset()` mutates `_elapsed` — side-effect in a read method. Double-call per frame = shake decays 2x fast. Move timer to `Update()` |
| Q2 | **HIGH** | `ground-scorch-mark.cs` | `ClearAll()` doesn't reset `_groundPrepared`. Editor play-without-domain-reload breaks crater wiring |
| Q3 | **MEDIUM** | `LaunchController.cs:101` | No null-check on `_roundManager` before `OnShotFired()` — NRE if unwired |
| Q4 | **MEDIUM** | `LaunchController.cs:32` | `Camera.main` cached in Awake, stale if Setup Scene runs during Play |
| Q5 | **MEDIUM** | `Rocket.cs` | `_groundTag`/`_targetTag` are `[SerializeField]` — can diverge from `GameConstants` SSOT. Make `private const` |
| Q6 | **MEDIUM** | `ObstacleSpawner.cs` | No `OnDestroy` — spawned obstacles orphaned if component destroyed |
| Q7 | **MEDIUM** | `camera-screen-shake.cs` | No `OnDisable` guard — shake runs when component disabled |
| Q8 | **LOW** | `explosion-burst-particle-effect.cs` | `[SerializeField]`/`[Header]` on runtime-instantiated component — no Inspector ever sees them |
| Q9 | **LOW** | `runtime-sprite-factory.cs:57` | Fallback shader `Hidden/InternalErrorShader` (pink) — use `UI/Default` instead |
| Q10 | **LOW** | `GameRoundTracker.cs` | `TryUpdateBest(0)` sets best=0, permanently shows "Best --". Add `shots <= 0` guard |
| Q11 | **LOW** | `round-manager-auto-play-restart-and-target.cs` | `HandleAutoPlay` doesn't hide hint texts (angle/force) during flight |
| Q12 | **LOW** | `ProceduralAudioClipGenerator.cs:64-68` | Phase resets at note boundary → audible click in win jingle |
| Q13 | **LOW** | `ProceduralAudioClipGenerator.cs:34` | `Random.value` in audio gen consumes Unity random stream — affects determinism |
| Q14 | **LOW** | `AimArrow.cs:49-50` | Reads `localScale.x` every frame instead of caching initial |

### ARCHITECTURE GAPS (for 10/10)

| ID | Sev | Category | What's Missing |
|----|-----|----------|----------------|
| A1 | **HIGH** | Input | Legacy `Input` API — no multi-touch, breaks WebGL mobile |
| A2 | **HIGH** | Testing | Only 15% coverage (2/13 classes). Need: ObstacleSpawner, Rocket, AudioManager, CameraController, GroundScorch, LaunchController |
| A3 | **MEDIUM** | Pooling | `ExplosionEffect`, `RocketDebris`, `GroundScorch` all Instantiate+Destroy per shot. GC spikes on mobile |
| A4 | **MEDIUM** | Config | `GameConstants` static class — recompile to change. ScriptableObject would be designer-friendly |
| A5 | **MEDIUM** | Audio | `PlayHitGround` and `PlayHitTarget` play same clip. No differentiation |
| A6 | **MEDIUM** | Performance | `GroundScorch.BuildMaskSprite()` does 32K `SetPixel` calls on main thread on first impact. Frame hitch on mobile |
| A7 | **MEDIUM** | Tags | Obstacles share `TagGround` — ambiguous. Need separate tag or layer |
| A8 | **LOW** | Singletons | `AudioManager`/`RoundManagerHUD` — no `DontDestroyOnLoad`. Single-scene OK, but undocumented |
| A9 | **LOW** | Build | `Sprites/Default` shader must be in Always Included Shaders or particles go pink |
| A10 | **LOW** | Localization | All UI strings hardcoded in C# |
| A11 | **LOW** | Test naming | Uses PascalCase not `test_system_scenario_expected` |

---

## Priority Action Plan

### Sprint 1 — Fix Bugs (score → 9.0)

1. **B1** — `HandleAutoPlay`: add `ResetToPosition` + `SetActive(true)` before `Launch()`
2. **B2** — `RocketDebris`: add `Destroy(gameObject, 2f)` after grounding
3. **B3** — `ObstacleSpawner`: clamp `totalTime` to physics max, re-solve angle when force clamped
4. **Q1** — `CameraScreenShake`: move `_elapsed` to `Update()`, make `GetOffset()` pure
5. **Q2** — `GroundScorch.ClearAll()`: add `_groundPrepared = false`
6. **B4** — Give ground tile distinct tag (e.g. `"GroundTile"`) or inject renderer by reference
7. **B5** — `ReloadAfterAutoPlay`: add `NewRound()` call or document intent

### Sprint 2 — Code Quality (score → 9.5)

8. **Q3–Q7** — Null guards, const tags, OnDestroy/OnDisable guards
9. **Q8–Q14** — Low-priority cleanup (SerializeField removal, fallback shader, phase fix)
10. **A5** — Differentiate hit/miss audio
11. **A7** — Separate obstacle tag/layer

### Sprint 3 — Architecture (score → 10.0)

12. **A2** — Unit tests for remaining 11 classes
13. **A1** — Input System migration (if mobile target)
14. **A3** — Object pooling for effects
15. **A4** — ScriptableObject config
16. **A6** — Pre-warm crater textures

---

## Unresolved Questions

1. Is mobile (iOS/Android, WebGL mobile) a target platform? If yes, A1 and A6 escalate to CRITICAL
2. Is hitting an obstacle intentionally terminal (same as ground)? No design doc confirms
3. Is `ReloadAfterAutoPlay` intentionally skipping `NewRound()`?
4. Should auto-play be triggerable only when no reload is pending?

---

*Reports: `code-reviewer-260403-0047-deep-architecture-quality-audit.md`, `code-reviewer-260403-0048-effects-camera-obstacles-editor-audit.md`, `code-reviewer-260403-0048-cross-cutting-integration-gap-analysis.md`*
