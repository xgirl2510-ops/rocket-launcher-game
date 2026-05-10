# Phase 2: Create AdManager Singleton

**Priority:** High | **Status:** ⬜ Not started

## Context

- [Research report](../reports/researcher-260408-1646-google-mobile-ads-sdk-v9-research.md)
- [Code standards](../../docs/unity-code-standards-and-conventions.md)
- [Code checklist](../../docs/unity-code-checklist.md)

## Overview

Create `AdManager.cs` singleton under `Assets/Scripts/Ads/`. Follows same singleton pattern as `AudioManager` and `RoundManagerHUD`. Handles SDK init, interstitial load/show lifecycle, and round-based ad frequency logic.

## Architecture

```
AdManager (singleton)
├── Initialize SDK (once, in Start)
├── LoadInterstitial() — async, callback-based
├── ShowInterstitialIfReady() — public, called by RoundManager
├── ShouldShowAd(roundNumber) — frequency logic
└── Event: OnAdClosed — signals RoundManager to continue
```

## File: `Assets/Scripts/Ads/AdManager.cs`

### Requirements

- Namespace: `RocketLauncher`
- Singleton: same pattern as AudioManager (no DontDestroyOnLoad — single scene)
- `[SerializeField] private` for all fields
- XML doc comments on all public members
- `#if UNITY_EDITOR` guards on Debug.Log
- Platform-aware ad unit IDs (iOS vs Android)
- Use test IDs in debug builds, production IDs in release builds

### Ad Frequency Logic

```csharp
/// Returns true if an ad should show after the given completed round.
/// Round 1 → always show. After that → every 2 rounds (3, 5, 7...).
public bool ShouldShowAd(int completedRound)
{
    if (completedRound <= 0) return false;
    if (completedRound == 1) return true;
    return (completedRound - 1) % 2 == 0;
}
```

### Interstitial Lifecycle

1. `Start()` → `MobileAds.Initialize()` → `LoadInterstitial()`
2. `ShowInterstitialIfReady()` called by RoundManager
3. `OnAdFullScreenContentClosed` → destroy old ad → preload next → fire `OnAdClosed` event
4. If ad not loaded / failed → skip ad, continue game flow

### Key Conventions (from checklist)

- No `GameObject.Find()` — singleton accessed via `AdManager.Instance`
- Cache all references in Awake/Start
- Coroutine only for async wait — not state machine
- `[RuntimeInitializeOnLoadMethod]` to reset static state (domain reload safety)

## Success Criteria

- [ ] AdManager.cs compiles without errors
- [ ] Singleton pattern with domain reload safety
- [ ] SDK initializes on Start
- [ ] Interstitial loads via static `InterstitialAd.Load()` (v9 API)
- [ ] `ShouldShowAd()` logic: round 1, then every 2 rounds
- [ ] `OnAdClosed` event for RoundManager to hook into
- [ ] Graceful fallback if ad not ready (game continues)
- [ ] Memory cleanup via `Destroy()` on old ads
- [ ] XML docs on all public members
