# Google Mobile Ads SDK for Unity — Technical Research

**Date:** 2026-04-08  
**Scope:** SDK v9.x+ for Unity 6 (2023+), latest API  
**Focus:** Installation, initialization, interstitial ads, iOS specifics

---

## 1. Installation

### Method: OpenUPM (Recommended)
```
Assets > Google Mobile Ads > Settings → Enter Android/iOS AdMob app IDs
```

**Package Manager approach:**
1. Add scoped registry (OpenUPM):
   - Name: `OpenUPM`
   - URL: `https://package.openupm.com`
   - Scope: `com.google`
2. Open Package Manager → My Registries → Install `Google Mobile Ads for Unity`

**Alternative:** Download `.unitypackage` from [GitHub releases](https://github.com/googleads/googleads-mobile-unity/releases)

**Latest version:** v9.3.0 (tested with GMA Android SDK 23.4.0, iOS SDK 11.10.0)

**Requirements:** Unity Editor 2019.4+ (tested on 2023+)

---

## 2. Initialization API

```csharp
using GoogleMobileAds.Api;

// Call ONCE at app launch, BEFORE loading any ads
MobileAds.Initialize();
```

**Critical:** Initialize after obtaining user consent if required. Ads may be preloaded upon initialization.

---

## 3. Interstitial Ad API (v9.x — Current)

### Load (Static Factory Method)
```csharp
var adRequest = new AdRequest();

InterstitialAd.Load(
    "ca-app-pub-3940256099942544/1033173712", // Test iOS interstitial
    adRequest,
    (InterstitialAd ad, LoadAdError error) =>
    {
        if (error != null)
        {
            Debug.Log($"Interstitial load failed: {error.GetMessage()}");
            return;
        }
        
        // Store reference for later
        _interstitialAd = ad;
        RegisterInterstitialEventHandlers(ad);
    }
);
```

### Show (Instance Method)
```csharp
if (_interstitialAd != null && _interstitialAd.CanShowAd())
{
    _interstitialAd.Show();
}
```

### Event Handlers (Optional but Recommended)
```csharp
void RegisterInterstitialEventHandlers(InterstitialAd ad)
{
    ad.OnAdFullScreenContentOpened += () =>
    {
        Debug.Log("Ad opened");
    };

    ad.OnAdFullScreenContentClosed += () =>
    {
        Debug.Log("Ad closed, load next one");
        // Preload next ad here
        LoadInterstitialAd();
    };

    ad.OnAdFullScreenContentFailed += (AdError error) =>
    {
        Debug.Log($"Ad failed: {error.GetMessage()}");
    };
}
```

### Memory Management
```csharp
_interstitialAd?.Destroy();
_interstitialAd = null;
```

**Key points:**
- Each loaded ad displays **once** → reload for next impression
- Call `Destroy()` to prevent memory leaks
- Best UX at natural transition points (game level completion)

---

## 4. iOS Test Ad Unit IDs

| Format | Test Ad Unit ID |
|--------|-----------------|
| **Interstitial** | `ca-app-pub-3940256099942544/4411468910` |
| iOS App ID | `ca-app-pub-3940256099942544~1458002511` |

**Important:** Replace with production IDs before App Store submission. Test IDs safe to click during development.

---

## 5. iOS ATT (App Tracking Transparency)

### Requirement
iOS 14+: Apps must request IDFA permission via ATT prompt if targeting ad personalization.

### Implementation (Recommended)
Use **User Messaging Platform (UMP) SDK** to trigger ATT context → then initialize GMA SDK:

```csharp
// After UMP consent/ATT dialog completes:
MobileAds.Initialize();
```

**In native iOS (if bridging):**
```objc
[GADMobileAds.sharedInstance startWithCompletionHandler:nil];
```

### Timing
Wait for ATT completion **before** loading ads so GMA SDK can use IDFA in requests.

**Official docs:** [iOS Privacy Strategies](https://developers.google.com/admob/ios/privacy/strategies)

---

## 6. Unity 6 Compatibility

### Status: Compatible with Known Issues
- v9.3.0 supports Unity 2019.4+ (includes 2023+ / Unity 6)
- **Issue reported:** [#3761](https://github.com/googleads/googleads-mobile-unity/issues/3761) — crashes on Unity 6.0 (Android)
- **Workaround:** Use [latest v9.x](https://github.com/googleads/googleads-mobile-unity/releases) and ensure Android gradle/build tools are up-to-date

### Version Sync Timing
When Google releases iOS SDK updates, plugin updates lag other platforms (1-2 months typical). Check [releases page](https://github.com/googleads/googleads-mobile-unity/releases) for latest.

---

## 7. Code Template (Complete)

```csharp
using GoogleMobileAds.Api;
using UnityEngine;

public class AdManager : MonoBehaviour
{
    private InterstitialAd _interstitialAd;
    private const string ANDROID_INTERSTITIAL = "ca-app-pub-3940256099942544/1033173712";
    private const string IOS_INTERSTITIAL = "ca-app-pub-3940256099942544/4411468910";

    void Start()
    {
        MobileAds.Initialize();
        LoadInterstitialAd();
    }

    void LoadInterstitialAd()
    {
        var adUnitId = Application.platform == RuntimePlatform.IPhonePlayer
            ? IOS_INTERSTITIAL
            : ANDROID_INTERSTITIAL;

        var request = new AdRequest();
        InterstitialAd.Load(adUnitId, request, OnInterstitialLoaded);
    }

    void OnInterstitialLoaded(InterstitialAd ad, LoadAdError error)
    {
        if (error != null)
        {
            Debug.LogError($"Load error: {error.GetMessage()}");
            return;
        }

        _interstitialAd = ad;
        RegisterEventHandlers();
    }

    void RegisterEventHandlers()
    {
        _interstitialAd.OnAdFullScreenContentClosed += () =>
        {
            _interstitialAd?.Destroy();
            _interstitialAd = null;
            LoadInterstitialAd(); // Preload next
        };

        _interstitialAd.OnAdFullScreenContentFailed += (error) =>
        {
            Debug.LogError($"Ad failed: {error.GetMessage()}");
        };
    }

    public void ShowInterstitialAd()
    {
        if (_interstitialAd != null && _interstitialAd.CanShowAd())
        {
            _interstitialAd.Show();
        }
    }
}
```

---

## Key Takeaways

1. **Installation:** OpenUPM (`com.google.ads.mobile`) — officially recommended
2. **API paradigm shift:** `InterstitialAd.Load()` (static) replaces deprecated `LoadAd()` instance method
3. **Callback pattern:** Load is async; callback receives both `ad` and `error` (null = success)
4. **iOS test ID:** `ca-app-pub-3940256099942544/4411468910`
5. **ATT:** Not enforced by GMA SDK, but recommended via UMP SDK for iOS 14+
6. **Unity 6:** Compatible; monitor GitHub for v9.x fixes if issues arise
7. **Lifecycle:** One ad per load → must reload after show/close events

---

## Sources

- [Official Quick Start](https://developers.google.com/admob/unity/quick-start)
- [Interstitial Ads Guide](https://developers.google.com/admob/unity/interstitial)
- [Test Ads Documentation](https://developers.google.com/admob/unity/test-ads)
- [GitHub Releases](https://github.com/googleads/googleads-mobile-unity/releases)
- [iOS Privacy Strategies](https://developers.google.com/admob/ios/privacy/strategies)

---

## Unresolved Questions

- GMA SDK compatibility window with future Unity 6.1+ — monitor releases page
- Whether UMP SDK is required or optional for iOS 14+ ATT (docs recommend but don't mandate)
