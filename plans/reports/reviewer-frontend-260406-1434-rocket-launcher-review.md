# Frontend Code Review — Rocket Launcher Game
**Date:** 2026-04-06  
**Reviewer:** code-reviewer (frontend)  
**Branch:** main

---

## Code Review Summary

### Scope
- **Files reviewed:** 8 source files + sprite assets
  - `Assets/Scripts/Camera/CameraController.cs` (256 lines)
  - `Assets/Scripts/Camera/camera-screen-shake.cs` (48 lines)
  - `Assets/Scripts/Effects/explosion-burst-particle-effect.cs` (103 lines)
  - `Assets/Scripts/Effects/ground-scorch-mark.cs` (196 lines)
  - `Assets/Scripts/Effects/rocket-debris-shatter-effect.cs` (133 lines)
  - `Assets/Scripts/Effects/impact-effects-handler.cs` (34 lines)
  - `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs` (374 lines)
  - `Assets/Editor/rocket-launcher-scene-setup-environment-and-gameplay-objects.cs` (153 lines)
  - `Assets/Sprites/Generated/` (circle-100x100.png, square-100x100.png + .asset files)
- **Lines analyzed:** ~1,297
- **Updated plans:** N/A (no plan file provided)

### Overall Assessment
Frontend/visual code is well-structured and clean overall. Memory management is solid — shared textures/materials use `RuntimeSpriteFactory`, all static state is reset via `[RuntimeInitializeOnLoadMethod]`, and crater/debris cleanup paths are properly implemented. Two genuinely actionable issues found: a non-deterministic sorting layer ID that can break across .NET environments, and a missing null-check warning in `ImpactEffectsHandler`. The rest are moderate/low polish items.

---

## Critical Issues

**None found.**

Memory audit: no leaks detected. `RuntimeSpriteFactory` shared sprite/material are destroyed on domain reload. `GroundScorch._maskVariants` (8× Texture2D + Sprite) destroyed in `DestroyMaskVariants()`. `ExplosionEffect` uses shared material (not leaked on GO destroy). `RocketDebris._allDebris` tracked and cleaned on `ClearAll()` + `OnDestroy()`.

---

## Important Findings

**[IMPORTANT] Non-deterministic sorting layer uniqueID**  
`rocket-launcher-scene-auto-setup-editor-tool.cs:317` — `entry.FindPropertyRelative("uniqueID").intValue = name.GetHashCode()`  
C# `string.GetHashCode()` is randomized per-process since .NET Core (and Unity uses IL2CPP/Mono which have their own behavior). This means the same layer name can produce different IDs across Editor sessions. If IDs collide with existing built-in layers or each other, `SpriteRenderer.sortingLayerName` lookups silently fail (renderer draws on wrong layer).  
**Fix:** Use a fixed deterministic constant per layer name, e.g.:
```csharp
private static readonly System.Collections.Generic.Dictionary<string, int> LayerIDs = new()
{
    { "Background",  100 },
    { "Environment", 200 },
    { "Gameplay",    300 },
    { "Projectile",  400 },
};
entry.FindPropertyRelative("uniqueID").intValue = LayerIDs[name];
```

**[IMPORTANT] `ImpactEffectsHandler` has no null/missing-reference feedback**  
`impact-effects-handler.cs:11-16` — `_rocket` is a `[SerializeField]` field. If the editor tool fails to wire it (e.g., `ImpactEffectsHandler._rocket` property name changed), `OnEnable` silently subscribes nothing. All impact effects (explosion, debris, craters) are silently disabled with no log message. The editor tool does wire it via `SerializedObject` (`rocket-launcher-scene-setup-environment-and-gameplay-objects.cs:129-132`), but there's no runtime guard.  
**Fix:** Add `Start()` null check:
```csharp
private void Start()
{
    if (_rocket == null)
        Debug.LogError("[ImpactEffectsHandler] _rocket not assigned — impact effects disabled.", this);
}
```

---

## Moderate Priority Improvements

**[MODERATE] `BuildMaskSprite()` texture missing `wrapMode = Clamp`**  
`ground-scorch-mark.cs:167-191` — `new Texture2D(size, size)` uses default `wrapMode = Repeat`. SpriteMask alpha cutoff rendering can sample edge pixels via bilinear filtering (default), potentially causing 1-pixel wrap-around artifacts on crater mask edges at certain scales.  
**Fix:** Add `tex.wrapMode = TextureWrapMode.Clamp;` before `tex.Apply()` (line 189).

**[MODERATE] `SetupAudio()` silently skips missing audio clips**  
`rocket-launcher-scene-auto-setup-editor-tool.cs:107-113` — If `Assets/Audio/rocket-start.mp3` etc. don't exist, `AssetDatabase.LoadAssetAtPath` returns null and clips are silently skipped. No warning is emitted, leaving the user wondering why audio doesn't work.  
**Fix:** Add warnings per missing clip:
```csharp
if (launchClip == null) Debug.LogWarning("[SceneSetupTool] Missing: Assets/Audio/rocket-start.mp3");
```

**[MODERATE] `m_SortingLayers` direct SerializedObject write is fragile**  
`rocket-launcher-scene-auto-setup-editor-tool.cs:303-321` — Writing directly into `m_SortingLayers` via `SerializedObject` bypasses Unity's public API. The internal property name `m_SortingLayers` could change in a future Unity version. Not a current Unity 6 issue, but worth noting for maintainability.  
**Recommendation:** Document the dependency on the internal property name with a comment.

**[MODERATE] `ExplosionEffect` tuning fields should be `const` or `static readonly`**  
`explosion-burst-particle-effect.cs:15-18` — `_burstCount`, `_particleLifetime`, `_startSpeed`, `_startSize` are private instance fields initialized with literals and never changed per-instance. They allocate memory per `ExplosionEffect` instance (4 fields × sizeof(float/int)) and cannot be tuned in the Inspector (no `[SerializeField]`).  
**Fix:** Change to `private const` / `private static readonly` or add `[SerializeField]` to make them Inspector-tunable:
```csharp
[SerializeField] private int _burstCount = 30;
[SerializeField] private float _particleLifetime = 0.6f;
```

---

## Low Priority Suggestions

**[LOW] `RocketDebris` lands abruptly — no fade before destroy**  
`rocket-debris-shatter-effect.cs:124` — `Destroy(gameObject, 2f)` with no alpha fade. Grounded debris pieces disappear instantly after 2 seconds. A simple alpha fade coroutine would improve visual polish.

**[LOW] `BuildMaskSprite()` — `filterMode` set after `Apply()`**  
`ground-scorch-mark.cs:190-191` — `tex.filterMode = FilterMode.Point` is set after `tex.Apply()`. Setting it before `Apply()` avoids an unnecessary re-upload to GPU. Move to line before `tex.Apply()`.

**[LOW] `PanCoroutine` ortho sentinel `-1f` is implicit contract**  
`CameraController.cs:228-229` — Using `-1f` as a sentinel for "no ortho lerp" is an implicit API contract not documented in the method signature. Valid ortho sizes are always > 0 in Unity, so `-1f` works, but a nullable parameter would be more explicit:
```csharp
private IEnumerator PanCoroutine(Vector2 from, Vector2 to, float duration,
    float? orthoFrom = null, float? orthoTo = null)
```

**[LOW] `TargetX` magic multiplier**  
`rocket-launcher-scene-setup-environment-and-gameplay-objects.cs:33` — `TargetX = CamHalfWidth * 4f` has no comment. The `4f` multiplier places the target well beyond the visible camera boundary (intentional — camera follows rocket to reach it), but this intent isn't documented.  
**Fix:** Add inline comment: `// target starts 4× camera half-width to the right (off-screen; RoundManager repositions it)`

---

## Positive Observations

- `CameraController` state machine is clean: single `_activeCoroutine` prevents coroutine races, `StopActiveCoroutine()` called consistently before state transitions.
- `CameraScreenShake` is correctly decoupled — pure `GetOffset()` read with no side effects; `OnDisable()` properly zeros offset.
- `SetCameraXY()` integrates shake offset cleanly without any risk of NaN (verified: `_elapsed >= _duration` guard on init state 0 >= 0 is safe).
- `GroundScorch` parallel lists (`_allCraters`, `_craters`) → struct approach is clean. `GetGroundY()` correctly accounts for crater depth for debris landing.
- `RuntimeSpriteFactory` shared sprite/material pattern eliminates per-instance GPU resource duplication across 4 effect systems.
- `ImpactEffectsHandler` correctly uses `OnEnable`/`OnDisable` (not `Start`/`OnDestroy`) for event subscription — survives GO deactivation cycles.
- All static state uses `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` — correct earliest lifecycle hook, compatible with Enter Play Mode settings.
- Editor tool wires `ImpactEffectsHandler._rocket` via `SerializedObject` rather than `GetComponent<Rocket>()` assignment — correct for private `[SerializeField]` fields.

---

## Recommended Actions

1. **Fix sorting layer IDs** — Replace `GetHashCode()` with fixed constants in `SetupSortingLayers()`. Prevents silent rendering-order bugs across Editor restarts.
2. **Add null check log to `ImpactEffectsHandler.Start()`** — One-line fix, prevents silent effect failure when wiring is broken.
3. **Add `tex.wrapMode = Clamp` in `BuildMaskSprite()`** — One-line fix before `tex.Apply()`.
4. **Add missing audio clip warnings in `SetupAudio()`** — Three one-line `Debug.LogWarning` calls.
5. **Convert `ExplosionEffect` tuning fields to `[SerializeField]`** — Makes particle tuning accessible in Inspector without code changes.

---

## Metrics

- **Type Coverage:** N/A (C# with Unity types, no TypeScript)
- **Test Coverage:** Effects logic not unit-tested (visual/runtime only); `GroundScorch` has `ground-scorch-tests.cs` (unreviewed here)
- **Linting Issues:** 0 syntax errors; all files compile cleanly
- **Findings by severity:** Critical: 0 · Important: 2 · Moderate: 4 · Low: 4

---

## Unresolved Questions

1. `Assets/Sprites/Generated/circle-100x100.png` and `square-100x100.png` — these pre-baked sprites exist but `RuntimeSpriteFactory` generates sprites at runtime (4×4 px). Are the Generated sprites referenced anywhere, or are they orphaned assets from a previous approach?
2. `TargetAspect = 9f / 19.5f` comment says "iPhone 15 Pro Max aspect 19.5:9" — confirms portrait orientation. Is this intentional for all target platforms, or should the layout support landscape as well?
