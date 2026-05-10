# Code Review: Frontend/Visual Scripts — Post-Fix
**Date:** 2026-04-06  
**Reviewer:** code-reviewer agent  
**Branch:** main

---

## Code Review Summary

### Scope
- Files reviewed: 8
  - `Assets/Scripts/Camera/CameraController.cs`
  - `Assets/Scripts/Camera/camera-screen-shake.cs`
  - `Assets/Scripts/Effects/explosion-burst-particle-effect.cs`
  - `Assets/Scripts/Effects/ground-scorch-mark.cs`
  - `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs`
  - `Assets/Scripts/Effects/impact-effects-handler.cs`
  - `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs`
  - `Assets/Editor/rocket-launcher-scene-setup-environment-and-gameplay-objects.cs`
- Lines analyzed: ~650
- Review focus: Clean code, Unity rendering best practices, effect lifecycle

---

### Overall Assessment

Clean, well-structured code. No critical bugs. Four medium concerns (one potential editor regression, misleading annotations, missing undo coverage, time-scale fragility) and five low-priority items. Previous issues appear resolved.

---

### Critical Issues

None.

---

### High Priority Findings

None.

---

### Medium Priority Findings

**[MEDIUM] `LayerMask.NameToLayer("Rocket")` called immediately after `SetupLayer(8, "Rocket")`**  
`rocket-launcher-scene-setup-environment-and-gameplay-objects.cs:109`  
`SetupLayer` writes via `SerializedObject.ApplyModifiedProperties()` to TagManager. Unity's in-memory `LayerMask` cache may not reflect this synchronously, causing `NameToLayer("Rocket")` to return `-1`. The Rocket GO then gets `layer = -1` (invalid), breaking layer-based collision filtering silently.  
→ Replace `LayerMask.NameToLayer("Rocket")` with the known index `8` directly, matching the `SetupLayer(8, "Rocket")` call above it.

---

**[MEDIUM] `[SerializeField]` on `ExplosionEffect` instance fields — misleading, never Inspector-configurable**  
`explosion-burst-particle-effect.cs:14-19`  
`_burstCount`, `_particleLifetime`, `_startSpeed`, `_startSize` are annotated `[SerializeField]` but the class is always instantiated via the static `Spawn()` method — never placed on a prefab or in the scene. The annotations imply editor configurability that doesn't exist and add noise to any auto-generated documentation.  
→ Remove `[SerializeField]`; keep as plain `private` fields or promote to `private const` where values are fixed.

---

**[MEDIUM] TagManager edits not registered with Undo**  
`rocket-launcher-scene-auto-setup-editor-tool.cs:291-341` (`EnsureTag`, `SetupSortingLayers`, `SetupLayer`)  
All three methods modify `TagManager.asset` via `SerializedObject` without calling `Undo.RegisterCompleteObjectUndo(tagManagerTarget, "...")` before changes. Tags, sorting layers, and user layers are not reversible via Edit > Undo after a failed setup.  
→ Add `Undo.RegisterCompleteObjectUndo(tm.targetObject, "Setup Project Settings")` at the top of each method, before the first `FindProperty` call.

---

**[MEDIUM] `PanCoroutine` uses `Time.deltaTime` — freezes if time scale is 0**  
`CameraController.cs:232`  
All camera pans (`IntroCoroutine`, `ReturnToVehicleCoroutine`, `PanToTargetCoroutine`) drive elapsed time with `Time.deltaTime`. If any future pause system sets `Time.timeScale = 0`, all pans stall indefinitely.  
→ Replace `Time.deltaTime` with `Time.unscaledDeltaTime` in `PanCoroutine`. No functional change for current gameplay; safe forward-compat.

---

### Low Priority Suggestions

**[LOW] `CamTop` computed but never used**  
`rocket-launcher-scene-setup-environment-and-gameplay-objects.cs:19`  
`private static readonly float CamTop = CamY + CamOrthoSize;` — not referenced anywhere in this file or the partial-class sibling. Dead computation.  
→ Remove or mark with a comment if it's a layout anchor reserved for future use.

---

**[LOW] Crater GameObjects are scene roots — hierarchy clutter**  
`ground-scorch-mark.cs:86`  
`new GameObject("Crater")` is created with no parent, making each crater a root GO. With 10+ rounds, this clutters the hierarchy. Existing environment parent GO is not accessible from a static class.  
→ Acceptable as-is for a static class with no scene reference; document the limitation in the `Spawn()` summary, or add an optional `Transform parent` parameter.

---

**[LOW] Class summary promises fade; no fade implemented**  
`rocket-debris-shatter-effect.cs` header comment: "Pieces **fade** and self-destruct after landing."  
After `_grounded = true`, pieces are destroyed via `Destroy(gameObject, 2f)` — they pop out instantly. No alpha lerp or color animation exists.  
→ Either implement a simple fade coroutine or correct the summary to "Pieces self-destruct after landing."

---

**[LOW] Target aspect ratio hardcoded to iPhone 15 Pro Max**  
`rocket-launcher-scene-setup-environment-and-gameplay-objects.cs:22-23`  
`private const float TargetAspect = 9f / 19.5f;` is used to place the target at `CamHalfWidth * 4f`. In the Unity Game view at a wider aspect (e.g., 16:9), the target lands off-screen-right. Harmless for release target device; confusing during PC editor testing.  
→ Add a comment: `// Layout tuned for iPhone 15 Pro Max (9:19.5). Target may appear off-screen on wider aspect ratios in editor.`

---

**[LOW] Redundant `GameObject.Find()` for same names across Wire* methods**  
`rocket-launcher-scene-auto-setup-editor-tool.cs:133,175,264,270`  
`"Rocket"` is found independently in `WireRoundManager`, `WireLaunchController`, and `WireCameraController`. Same for `"LauncherVehicle"` and `"Target"`. In editor tools this is trivial cost, but it fragments intent.  
→ Acceptable as-is given editor-only context. No action required.

---

### Positive Observations

- `CameraScreenShake` correctly uses pure read `GetOffset()` — no side effects on position.
- `CameraController.SetCameraXY()` applies shake as an additive offset — won't drift camera position on shake end. ✓
- `GroundScorch.ResetStaticState()` + `DestroyMaskVariants()` correctly destroys both Sprite and Texture2D — no GPU leak. ✓
- `RocketDebris.OnDestroy()` self-removes from `_allDebris` — no stale references. ✓
- `ImpactEffectsHandler` uses `OnEnable`/`OnDisable` for event subscription — correct lifecycle pattern. ✓
- `ExplosionEffect` renderer uses `RuntimeSpriteFactory.GetParticleMaterial()` via setter (not getter) — shared material not copied, no leak. ✓
- `ClearScene()` uses `Undo.DestroyObjectImmediate` — destruction is undoable. ✓
- `PanCoroutine` always snaps to final position post-loop — no float precision overshoot. ✓
- `StopActiveCoroutine()` null-guards prevent double-stop crashes. ✓

---

### Recommended Actions

1. **(MEDIUM)** `rocket-launcher-scene-setup-environment-and-gameplay-objects.cs:109` — replace `LayerMask.NameToLayer("Rocket")` with `8`.
2. **(MEDIUM)** `explosion-burst-particle-effect.cs:14-19` — remove `[SerializeField]` from `_burstCount`, `_particleLifetime`, `_startSpeed`, `_startSize`.
3. **(MEDIUM)** `rocket-launcher-scene-auto-setup-editor-tool.cs` — add `Undo.RegisterCompleteObjectUndo` before TagManager modifications in `EnsureTag`, `SetupSortingLayers`, `SetupLayer`.
4. **(MEDIUM)** `CameraController.cs:232` — switch `Time.deltaTime` → `Time.unscaledDeltaTime` in `PanCoroutine`.
5. **(LOW)** Remove or document `CamTop` dead constant.
6. **(LOW)** Fix `RocketDebris` summary comment to match actual behavior.

---

### Metrics
- Type Coverage: n/a (C# with explicit typing throughout)
- Linting Issues: 0 compiler errors expected
- Issues by severity: Critical 0 / High 0 / Medium 4 / Low 5

---

### Unresolved Questions

- None.
