# AdMob Interstitial Ads Integration

**Status:** Planning
**Branch:** `feat/admob-ads`
**Research:** `plans/reports/researcher-260408-1646-google-mobile-ads-sdk-v9-research.md`

## AdMob IDs

| | iOS (production) | Test |
|---|---|---|
| App ID | `ca-app-pub-9025216789293973~8026478685` | `ca-app-pub-3940256099942544~1458002511` |
| Interstitial | `ca-app-pub-9025216789293973/2777089224` | `ca-app-pub-3940256099942544/4411468910` |

## Ad Logic

- Round 1 complete → show interstitial
- After that → every 2 completed rounds (round 3, 5, 7...)
- Formula: `roundNumber == 1 || (roundNumber > 1 && (roundNumber - 1) % 2 == 0)`

## Phases

| # | Phase | Status |
|---|-------|--------|
| 1 | [Install SDK](phase-01-install-google-mobile-ads-sdk.md) | ⬜ |
| 2 | [Create AdManager](phase-02-create-ad-manager-singleton.md) | ⬜ |
| 3 | [Integrate with RoundManager](phase-03-integrate-round-manager-ad-flow.md) | ⬜ |
| 4 | [Test on device](phase-04-device-testing-validation.md) | ⬜ |
