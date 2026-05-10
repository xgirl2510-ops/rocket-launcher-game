# Phase 4: Device Testing & Validation

**Priority:** High | **Status:** ⬜ Not started

## Overview

AdMob ads do NOT work in Unity Editor — must test on real device. Use Google's test Ad Unit IDs during development to avoid policy violations.

## Test Strategy

### In Editor (compile-only)
- [ ] No compile errors after SDK import + code changes
- [ ] AdManager initializes without exceptions (SDK init works in Editor but ads won't load)
- [ ] Game flow works with AdManager present (graceful fallback when ad not ready)
- [ ] Round counter increments correctly

### On Device (iOS)
- [ ] Build to iOS device via Xcode
- [ ] Test ad appears after round 1 completion + restart
- [ ] Test ad appears after round 3, 5, 7...
- [ ] No ad after round 2, 4, 6...
- [ ] Game resumes correctly after ad closes
- [ ] Game works when ad fails to load (airplane mode test)
- [ ] No memory leak after multiple ad show cycles

## Test Ad IDs (MUST use during development)

| Platform | ID |
|----------|-----|
| iOS Interstitial | `ca-app-pub-3940256099942544/4411468910` |
| Android Interstitial | `ca-app-pub-3940256099942544/1033173712` |

## Pre-Release Checklist

- [ ] Switch from test IDs to production IDs
- [ ] iOS App ID: `ca-app-pub-9025216789293973~8026478685`
- [ ] iOS Ad Unit: `ca-app-pub-9025216789293973/2777089224`
- [ ] Test on device with production IDs (verify real ads appear)
- [ ] Review AdMob policies compliance

## Known Limitations

- New ad units may take up to 1 hour to start serving
- Account needs payment setup for production ads (banner on AdMob dashboard)
- ATT dialog (iOS 14+) not implemented yet — can add later via UMP SDK
