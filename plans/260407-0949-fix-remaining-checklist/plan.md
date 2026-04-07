# Plan: Fix Remaining Checklist Items

**Date:** 2026-04-07
**Branch:** main
**Status:** Draft

---

## Scope

Fix 2 actionable items from definitive audit. Skip YAGNI items (ScriptableObject, interfaces, pooling).

| # | Item | Severity | Effort |
|---|------|----------|--------|
| 1 | [5.6] Physics collision matrix not configured programmatically | Medium | ~15 min |
| 2 | [6.4] GroundScorch fallback `GameObject.Find` still exists | Low | ~10 min |

---

## Fix 1: Physics Collision Matrix Setup [5.6]

**Problem:** Editor tool sets up Rocket on layer 8, obstacles on layer 0 (Default), but never configures Physics2D collision matrix. Relies on Unity default (all layers collide). Checklist requires: "Physics layer và collision matrix chỉ bật đúng cặp cần thiết."

**Current state:**
- `rocket-launcher-scene-auto-setup-editor-tool.cs:79` — `SetupLayer(8, "Rocket")` creates layer but doesn't configure collision matrix
- Default Physics2D matrix: all layers collide with all layers

**Fix:**
Add `ConfigurePhysics2DCollisionMatrix()` to `RunCoreSetup()` after `SetupLayer()`:

```csharp
private static void ConfigurePhysics2DCollisionMatrix()
{
    // Disable all collisions for Rocket layer first
    for (int i = 0; i < 32; i++)
        Physics2D.IgnoreLayerCollision(GameConstants.RocketLayer, i, true);
    
    // Enable only needed: Rocket ↔ Default (ground + obstacles + target)
    Physics2D.IgnoreLayerCollision(GameConstants.RocketLayer, GameConstants.DefaultLayer, false);
}
```

**File:** `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs`
- Add call after line 79 (`SetupLayer(8, "Rocket")`)
- Add the method to the class

**Verification:** After Setup Scene, check Edit > Project Settings > Physics 2D > Layer Collision Matrix — Rocket (8) should only collide with Default (0).

---

## Fix 2: Remove GroundScorch Fallback Find [6.4]

**Problem:** `GroundScorch.cs:74` has `GameObject.Find(GameConstants.GroundObjectName)` as fallback when `Transform ground` param is null. This was added as defensive code but violates checklist.

**Current state:**
```csharp
// ground-scorch-mark.cs:68-76
public static void PrepareGround(Transform ground)
{
    if (_groundPrepared) return;
    // Fallback: find ground if not injected
    if (ground == null)
    {
        var groundGo = GameObject.Find(GameConstants.GroundObjectName);
        if (groundGo != null) ground = groundGo.transform;
    }
    if (ground == null) return;
    ...
}
```

**Fix:**
- Remove the fallback `GameObject.Find` block
- Add `Debug.LogWarning` (wrapped in `#if`) when ground is null — helps debug wiring issues
- Ensure `ImpactEffectsHandler._ground` is always wired by editor tool (already done)

```csharp
public static void PrepareGround(Transform ground)
{
    if (_groundPrepared) return;
    if (ground == null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning("[GroundScorch] Ground transform not provided — craters disabled.");
#endif
        return;
    }
    ...
}
```

**File:** `Assets/Scripts/Effects/ground-scorch-mark.cs`

**Verification:** Grep for `GameObject.Find` in Assets/Scripts/ — should return 0 results.

---

## Team Assignment

**Single dev** — only 2 small fixes, no file ownership conflict:
- `Assets/Editor/rocket-launcher-scene-auto-setup-editor-tool.cs` (Fix 1)
- `Assets/Scripts/Effects/ground-scorch-mark.cs` (Fix 2)

---

## Success Criteria

- [ ] Physics2D collision matrix configured: Rocket only collides with Default
- [ ] Zero `GameObject.Find` in runtime code (Assets/Scripts/)
- [ ] Existing tests still pass
- [ ] No behavior changes
