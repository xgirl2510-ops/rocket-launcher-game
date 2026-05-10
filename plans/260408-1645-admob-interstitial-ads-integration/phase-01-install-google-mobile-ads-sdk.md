# Phase 1: Install Google Mobile Ads Unity Plugin

**Priority:** High | **Status:** ⬜ Not started

## Overview

Install Google Mobile Ads Unity Plugin v9.x via `.unitypackage` from GitHub releases. Configure App ID in project settings.

## Steps

1. Download latest `.unitypackage` from https://github.com/googleads/googleads-mobile-unity/releases
2. Import into Unity: Assets > Import Package > Custom Package
3. After import, go to Assets > Google Mobile Ads > Settings
4. Enter iOS App ID: `ca-app-pub-9025216789293973~8026478685`
5. Enable "Delay app measurement" = true (for ATT compliance later)
6. Verify no compile errors

## Important Notes

- Plugin adds External Dependency Manager (EDM4U) — resolves iOS CocoaPods automatically
- After import: run Assets > External Dependency Manager > iOS Resolver > Resolve
- `.unitypackage` is more reliable than OpenUPM for Unity 6 compatibility

## Files Created/Modified

- `Assets/GoogleMobileAds/` — SDK files (auto-generated)
- `Assets/Plugins/iOS/` — iOS native bridge (auto-generated)
- `ProjectSettings/GoogleMobileAdsSettings.asset` — App ID config

## Success Criteria

- [ ] Plugin imported without compile errors
- [ ] App ID configured in Google Mobile Ads Settings
- [ ] iOS Resolver runs successfully
