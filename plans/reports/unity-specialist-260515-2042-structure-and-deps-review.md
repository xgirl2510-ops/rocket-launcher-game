# Unity 6 Project Structure + Deps Audit
**Date:** 2026-05-15 | **Unity:** 6000.4.0f1 | **Scope:** Read-only audit

---

## Structural blockers (real friction)

### 1. Color Space: Gamma (not Linear)
- `ProjectSettings.asset:49` → `m_ActiveColorSpace: 0` = Gamma
- **Impact:** All particle effects, gradients, and sprite compositing are wrong. Explosion/smoke colors are brighter than intended. 2D games look "washed out" in Gamma.
- **Fix:** Edit > Project Settings > Player > Rendering > Color Space → Linear. Test all sprite materials after — some may need shader recheck.

### 2. TextMeshPro: Pre-release version
- `manifest.json` → `com.unity.textmeshpro: 3.2.0-pre.12`
- Pre-release in production = undefined behavior on TMP dynamic atlas (this project already hit `ArgumentNullException` on atlas warm-up). A stable release exists.
- **Fix:** Change to `3.2.0-pre.12` → `3.2.0-pre.12` is the latest under Unity 6 bundled TMP. Verify in Package Manager if `3.2.0` stable is available; if not, stay here but be aware.
  - Actually for Unity 6000.x, TMP ships embedded — check if a standalone version is even needed or if it can be removed from manifest to use the built-in.

### 3. Legacy Input System still in use
- `LaunchController.cs:56,58,60` uses `Input.GetMouseButtonDown/GetMouseButton/GetMouseButtonUp`
- No `com.unity.inputsystem` in `manifest.json`
- **Impact:** Touch input on iOS/Android works only via old UnityEngine.Input which has no multi-touch abstraction. iOS TestFlight builds are already happening — mobile launch via mouse-sim will not work properly on device.
- **Severity:** BLOCKER for mobile, acceptable for desktop-only prototype.

### 4. Scripting backend not overridden — falls back to Mono for iOS
- `ProjectSettings.asset:714` → `scriptingBackend: {}` (empty dict = platform default)
- iOS default in Unity 6 is IL2CPP. If this is intentional, fine. If testing on device shows "Mono" in Xcode, force it: `scriptingBackend: {iPhone: 1}`.
- API compatibility level: `apiCompatibilityLevel: 6` = .NET Standard 2.1. Correct.

---

## Recommendations (ranked by ROI)

### R1 — Fix Color Space → Linear [High ROI, 1 click]
Affects every visual output. Do this before any more VFX tuning.

### R2 — Add Input System package + touch support [High ROI, ~2h]
```json
"com.unity.inputsystem": "1.11.2"
```
Wrap `LaunchController.cs` touch/mouse delta in `Touchscreen.current` + `Mouse.current` combo.
Until then, add `Input.multiTouchEnabled = true` in `Awake()` as a short-term workaround.

### R3 — File naming: document MonoBehaviour exception, don't rename [Medium ROI, 0h]
**Decision: keep PascalCase for MonoBehaviour-primary files, kebab-case for everything else.**

Rationale: Unity requires the filename to match the class name for MonoBehaviours — renaming `Rocket.cs` to `rocket.cs` breaks the component attachment in the scene and causes a missing-script error. The CLAUDE.md kebab-case rule targets non-MonoBehaviour utility files (which this project already follows correctly). The current split is already the right pattern:
- PascalCase: `Rocket.cs`, `CameraController.cs`, `AimArrow.cs`, `LaunchController.cs`, `AudioManager.cs`, `RoundManager.cs`, `AdManager.cs`, `GameConstants.cs`, `GameRoundTracker.cs` — all MonoBehaviour or static classes whose name is the primary identity
- kebab-case: all supporting scripts (effects, UI helpers, enums, partial classes)

**Action:** Add one line to `docs/code-standards.md`: "MonoBehaviour primary-class files use PascalCase to match Unity's class-name requirement. Supporting scripts use kebab-case."

### R4 — Remove orphan/unused packages [Low ROI, 30min]
These packages are in manifest but have zero usage in the codebase:
| Package | Why suspect | Action |
|---------|-------------|--------|
| `com.unity.multiplayer.center` (1.0.1) | Single-player game, no netcode | Remove |
| `com.unity.modules.cloth` | No SkinnedMeshRenderer/Cloth in 2D | Remove |
| `com.unity.modules.vehicles` | No WheelCollider usage | Remove |
| `com.unity.modules.terrain` + `terrainphysics` | No Terrain objects | Remove |
| `com.unity.modules.vr` | No XR | Remove |
| `com.unity.modules.wind` | No WindZone | Remove |
| `com.unity.modules.video` | No VideoPlayer | Remove |

Note: modules prefixed `com.unity.modules.*` are built-in and safe to remove from manifest — they won't break existing components that reference them, only exclude their compile symbols.

### R5 — TMP: switch from standalone to Unity 6 bundled [Low ROI, 15min]
Unity 6 bundles TMP as a built-in package. Having `com.unity.textmeshpro: 3.2.0-pre.12` as an explicit dependency can conflict. Remove the explicit entry and let Unity use its built-in version unless a specific pre.12 feature is required.

### R6 — Color scorch / crater gradient re-check after Linear fix [Medium, 1h]
After switching to Linear color space, runtime-generated sprite textures in `ground-scorch-mark.cs` and `runtime-sprite-factory.cs` may need gamma-correction adjustments since `Color` values specified as sRGB will render differently under Linear.

---

## Nice-to-haves

### N1 — asmdef split: NOT warranted for this project
Current layout is already correct:
- `Assets/Scripts/RocketLauncher.Runtime.asmdef` — all runtime code
- `Assets/Editor/RocketLauncher.Editor.asmdef` — editor-only tools (correct `includePlatforms: Editor`)
- `Assets/Tests/Editor/RocketLauncher.Tests.Editor.asmdef` — test assembly with `UNITY_INCLUDE_TESTS` guard

A further Runtime→Feature split (e.g., `RocketLauncher.Effects`, `RocketLauncher.Obstacles`) would only help if compile times become painful or if features need to be conditionally stripped. At 60 scripts this is YAGNI. The three-assembly structure is correct.

### N2 — `Object.FindObjectsByType` in world-pause-controller (fine as-is)
- `world-pause-controller-...cs:36,48` uses `Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None)` — this IS the Unity 6 non-deprecated API (successor to `FindObjectsOfType`). No action needed.
- `jet-interceptor-launcher-rocket-defense-system.cs:49` comment already acknowledges `FindAnyObjectByType` is Unity 6 successor. Fine.

### N3 — Singleton pattern acceptable at this scale
`AudioManager.Instance`, `AdManager.Instance`, `RoundManagerHUD.Instance`, `RocketTrajectoryPredictor.Instance` — 4 singletons. For a single-scene mobile game this is appropriate. ServiceLocator would add complexity without benefit. No action needed.

### N4 — `gcIncremental: 1` already enabled
`ProjectSettings.asset:725` — incremental GC is on. Good.

### N5 — Test Framework 1.4.5
Current version is `1.4.5`. Latest for Unity 6 is `1.4.5`. Up to date.

---

## Summary table

| Area | Status | Action |
|------|--------|--------|
| Color Space | BLOCKER — Gamma | Switch to Linear immediately |
| Input System | BLOCKER for mobile | Add package + wrap LaunchController |
| TMP version | Caution — pre-release | Switch to bundled or verify stable |
| asmdef structure | Correct (3 assemblies) | No change needed |
| File naming | Acceptable split | Document exception in code-standards.md |
| Deprecated APIs | None found | FindObjectsByType usage is correct Unity 6 API |
| Orphan packages | 7 candidates | Remove to slim build |
| Scripting backend | Unspecified (defaults OK) | Verify IL2CPP on iOS in Xcode |
| API compat level | .NET Standard 2.1 | Correct |
| Incremental GC | Enabled | Good |
| Singletons | 4 instances | Acceptable at project scale |
| Legacy Input.cs | 3 call sites in LaunchController | Replace for mobile |

---

## Unresolved questions
- Is iOS the primary shipping target or just TestFlight testing? (determines urgency of Input System migration)
- Is TMP `3.2.0-pre.12` being kept for a specific dynamic font feature, or can it be dropped for the bundled version?
- After Linear color space switch: have explosion/scorch colors been authored assuming Gamma? They will visually darken — confirm with Luke before switching in an active dev sprint.
