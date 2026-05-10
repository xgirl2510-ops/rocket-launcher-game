using System;
using UnityEngine;
using GoogleMobileAds.Api;

namespace RocketLauncher
{
    /// <summary>
    /// Singleton that manages Google AdMob interstitial ads.
    /// Handles SDK initialization, ad loading/showing, and round-based frequency logic.
    /// </summary>
    [DisallowMultipleComponent]
    public class AdManager : MonoBehaviour
    {
        /// <summary>Singleton instance, set in Awake. Null if no AdManager in scene.</summary>
        public static AdManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        /// <summary>Fired when an interstitial ad closes. RoundManager subscribes to continue game flow.</summary>
        public event Action OnAdClosed;

        private InterstitialAd _interstitialAd;
        private bool _isSdkInitialized;

        // Test ad unit IDs — replace with production IDs before release:
        // iOS production: ca-app-pub-9025216789293973/2777089224
        // Android production: (create on AdMob dashboard)
#if UNITY_IOS
        private const string AdUnitId = "ca-app-pub-3940256099942544/4411468910";
#elif UNITY_ANDROID
        private const string AdUnitId = "ca-app-pub-3940256099942544/1033173712";
#else
        private const string AdUnitId = "unused";
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            MobileAds.Initialize(status =>
            {
                _isSdkInitialized = true;
                LoadInterstitial();
            });
        }

        /// <summary>
        /// Returns true if an interstitial ad should show after the given completed round.
        /// Round 1 → always. After that → every 2 rounds (3, 5, 7...).
        /// </summary>
        public bool ShouldShowAd(int completedRound)
        {
            if (completedRound <= 0) return false;
            if (completedRound == 1) return true;
            return (completedRound - 1) % 2 == 0;
        }

        /// <summary>
        /// Shows the interstitial ad if one is loaded and ready.
        /// Returns true if ad was shown, false if not ready (caller should continue game flow).
        /// </summary>
        public bool ShowInterstitialIfReady()
        {
            if (_interstitialAd != null && _interstitialAd.CanShowAd())
            {
                _interstitialAd.Show();
                return true;
            }
            return false;
        }

        private void LoadInterstitial()
        {
            // Destroy previous ad to prevent memory leak
            DestroyCurrentAd();

            var request = new AdRequest();
            InterstitialAd.Load(AdUnitId, request, OnInterstitialLoaded);
        }

        private void OnInterstitialLoaded(InterstitialAd ad, LoadAdError error)
        {
            if (error != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[AdManager] Interstitial load failed: {error.GetMessage()}");
#endif
                return;
            }

            _interstitialAd = ad;
            RegisterEventHandlers();
        }

        private void RegisterEventHandlers()
        {
            _interstitialAd.OnAdFullScreenContentClosed += HandleAdClosed;
            _interstitialAd.OnAdFullScreenContentFailed += HandleAdFailed;
        }

        private void HandleAdClosed()
        {
            // Destroy shown ad, preload next one
            DestroyCurrentAd();
            LoadInterstitial();
            OnAdClosed?.Invoke();
        }

        private void HandleAdFailed(AdError error)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[AdManager] Ad display failed: {error.GetMessage()}");
#endif
            // Ad failed to display — treat as closed so game continues
            DestroyCurrentAd();
            LoadInterstitial();
            OnAdClosed?.Invoke();
        }

        private void DestroyCurrentAd()
        {
            if (_interstitialAd != null)
            {
                _interstitialAd.Destroy();
                _interstitialAd = null;
            }
        }

        private void OnDestroy()
        {
            DestroyCurrentAd();
        }
    }
}
