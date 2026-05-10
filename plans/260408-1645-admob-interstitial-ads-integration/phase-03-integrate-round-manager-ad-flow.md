# Phase 3: Integrate RoundManager with Ad Flow

**Priority:** High | **Status:** ⬜ Not started

## Context

- `Assets/Scripts/Core/RoundManager.cs` — main round flow
- `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs` — restart logic
- `Assets/Scripts/Core/GameRoundTracker.cs` — tracks round number

## Overview

Hook AdManager into RoundManager's restart flow. When player completes a round (hits target) and clicks Restart, check if ad should show. If yes → show ad → wait for close → then start new round. If no → start new round immediately.

## Integration Point

**File:** `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs`
**Method:** `HandleRestart()` (line ~13)

### Current Flow
```
HandleRestart() → ResetGameState() → PrepareNewRound()
```

### New Flow
```
HandleRestart() → check ShouldShowAd(currentRound)
  ├── YES → ShowAd → OnAdClosed → ResetGameState() → PrepareNewRound()
  └── NO  → ResetGameState() → PrepareNewRound()
```

## Implementation

### Changes to `round-manager-auto-play-restart-and-target.cs`

```csharp
public void HandleRestart()
{
    _isAutoPlaying = false;
    StopAllCoroutines();

    if (AudioManager.Instance != null)
        AudioManager.Instance.PlayClick();

    RoundManagerHUD.Instance?.HideWinUI();
    RoundManagerHUD.Instance?.HideHints();

    // Check if ad should show for the round just completed
    int completedRound = _roundTracker.RoundNumber;
    if (AdManager.Instance != null && AdManager.Instance.ShouldShowAd(completedRound))
    {
        AdManager.Instance.OnAdClosed -= OnAdClosedRestart;
        AdManager.Instance.OnAdClosed += OnAdClosedRestart;
        if (!AdManager.Instance.ShowInterstitialIfReady())
        {
            // Ad not ready — continue without ad
            AdManager.Instance.OnAdClosed -= OnAdClosedRestart;
            StartNewRound();
        }
    }
    else
    {
        StartNewRound();
    }
}

private void OnAdClosedRestart()
{
    AdManager.Instance.OnAdClosed -= OnAdClosedRestart;
    StartNewRound();
}

private void StartNewRound()
{
    ResetGameState();
    PrepareNewRound();
}
```

### Key Design Decisions

1. **Ad blocks restart, not win screen** — player sees win UI, clicks restart, ad shows before new round
2. **Graceful fallback** — if ad not loaded, game continues immediately
3. **No ad during auto-play** — only after manual target hit + restart
4. **Event unsubscribe** — defensive unsub before sub (same pattern as CameraController events)

## Files Modified

- `Assets/Scripts/Core/round-manager-auto-play-restart-and-target.cs` — add ad check in HandleRestart

## Success Criteria

- [ ] Ad shows between rounds at correct frequency
- [ ] Game continues if ad fails to load
- [ ] No ad during auto-play demo
- [ ] No event listener leaks (unsub in OnDestroy)
- [ ] No circular dependency — RoundManager calls AdManager, not vice versa
