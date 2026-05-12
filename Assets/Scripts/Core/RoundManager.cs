using System.Collections;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Manages round flow: miss/hit handling, reload, restart, target randomization, auto-play.
    /// Subscribes to Rocket events and delegates camera/audio/HUD updates.
    /// LaunchController handles only slingshot input; this class handles everything after launch.
    /// </summary>
    public partial class RoundManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rocket _rocket;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private CameraController _cameraController;
        [SerializeField] private Transform _targetTransform;
        [SerializeField] private Transform _launcherVehicleTransform;
        [SerializeField] private ObstacleSpawner _obstacleSpawner;
        [SerializeField] private LaunchController _launchController;

        [Header("Reload")]
        [SerializeField] private float _reloadDelay = 1.5f;

        [Header("Target Randomization")]
        // Target sits 25-47u ahead of the launcher (car X ≈ -1.65, so world X 23-45).
        // Verified by physics sim: at MaxLaunchForce=30, worst-case reach is ~48.9u when goal Y=10
        // (the highest random altitude). 45 gives ~4u buffer so every roll is reachable with the
        // right angle + full power, even when goal sits at maximum altitude.
        [SerializeField] private float _targetMinX = 23f;
        [SerializeField] private float _targetMaxX = 45f;
        [SerializeField] private float _targetMinY = -4f;
        [SerializeField] private float _targetMaxY = 10f;

        private int _missCount;
        private bool _isAutoPlaying;
        private readonly GameRoundTracker _roundTracker = new GameRoundTracker();

        /// <summary>Per-round and cross-round statistics tracker.</summary>
        public GameRoundTracker RoundTracker => _roundTracker;

        private const int MissesBeforeHints = 5;

        private void Awake()
        {
            _roundTracker.LoadBestScore();
            RandomizeTarget();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Skip validation during SceneSetupTool (AddComponent fires OnValidate before wiring)
            if (!gameObject.scene.isLoaded) return;

            if (_rocket == null) Debug.LogWarning("[RoundManager] _rocket not assigned.", this);
            if (_spawnPoint == null) Debug.LogWarning("[RoundManager] _spawnPoint not assigned.", this);
            if (_launchController == null) Debug.LogWarning("[RoundManager] _launchController not assigned.", this);
            if (_targetTransform == null) Debug.LogWarning("[RoundManager] _targetTransform not assigned.", this);
        }
#endif

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_launchController == null) Debug.LogError("[RoundManager] _launchController is null.", this);
            if (_spawnPoint == null) Debug.LogError("[RoundManager] _spawnPoint is null.", this);
#endif

            // Defensive: ensure Target's SpriteRenderer has a sprite at runtime.
            // Some scenes have a Target with SpriteRenderer enabled + red color but sprite=null
            // (broken square asset cache). Without a sprite, Target is invisible despite being active.
            if (_targetTransform != null)
            {
                var sr = _targetTransform.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite == null)
                    sr.sprite = RuntimeSpriteFactory.GetSolidSprite();
            }

            if (_rocket != null)
            {
                _rocket.OnRocketLanded += HandleRocketMiss;
                _rocket.OnTargetHit += HandleTargetHit;
                _rocket.OnLauncherVehicleHit += HandleLauncherVehicleHit;
            }

            if (_cameraController != null)
                _cameraController.OnIntroComplete += OnIntroDone;
            else
                _launchController.EnableInput();
        }

        /// <summary>Called by LaunchController after a shot is fired.</summary>
        public void OnShotFired()
        {
            _roundTracker.IncrementShots();
        }

        /// <summary>Rocket hit target — hide rocket, show win (or reset if auto-play demo).</summary>
        private void HandleTargetHit()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopThrust();
                AudioManager.Instance.PlayHitTarget();
                AudioManager.Instance.PlayWin();
            }

            if (_cameraController != null)
                _cameraController.Shake(0.3f, 0.2f);

            if (_isAutoPlaying)
            {
                StartCoroutine(DelayedAction(_reloadDelay, ReloadAfterAutoPlay));
                return;
            }

            _roundTracker.TryUpdateBest(_roundTracker.RoundShots);
            _rocket.gameObject.SetActive(false);
            RoundManagerHUD.Instance?.ShowWinUI();
            RoundManagerHUD.Instance?.UpdateStatsUI(_roundTracker);
        }

        /// <summary>
        /// Friendly fire — rocket fell on its own launcher vehicle.
        /// Treat as game-over: explosion already fired by Rocket.HandleLauncherVehicleTrigger
        /// (via OnImpact event), then we PAUSE physics + particles + audio per-object via
        /// WorldPauseController so the scene stops cold while UI stays responsive.
        /// </summary>
        private void HandleLauncherVehicleHit(Vector2 impactPosition)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopThrust();
                AudioManager.Instance.PlayHitTarget(); // reuse — same explosion sfx feels right
            }

            if (_isAutoPlaying)
            {
                // Auto-play demo: just reset like any other shot, no UI, no freeze.
                StartCoroutine(DelayedAction(_reloadDelay, ReloadAfterAutoPlay));
                return;
            }

            _rocket.gameObject.SetActive(false);
            RoundManagerHUD.Instance?.ShowGameOverUI();
            RoundManagerHUD.Instance?.UpdateStatsUI(_roundTracker);

            // Delay the freeze so the explosion can fully render and debris rigidbodies
            // get to apply their impulse for ~0.4s before physics is silenced. Freezing in
            // the same frame as OnImpact would lock debris in place at spawn position and
            // make the player see no motion at all.
            StartCoroutine(DelayedAction(0.4f, WorldPauseController.Freeze));
        }

        /// <summary>Rocket hit ground — count miss (or reset if auto-play demo).</summary>
        private void HandleRocketMiss()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StopThrust();
                AudioManager.Instance.PlayHitGround();
            }

            if (_cameraController != null)
                _cameraController.Shake(0.2f, 0.1f);

            if (_isAutoPlaying)
            {
                StartCoroutine(DelayedAction(_reloadDelay, ReloadAfterAutoPlay));
                return;
            }

            _missCount++;
            StartCoroutine(DelayedAction(_reloadDelay, ReloadRocket));
        }

        /// <summary>Return camera, reset rocket, show hints if enough misses.</summary>
        private void ReloadRocket()
        {
            if (_cameraController != null)
                _cameraController.ReturnToVehicle();

            // Clear leftover explosion effects from the previous shot — lingering fire/smoke/debris
            // would otherwise overlap visually with the next shot's impact.
            RocketDebris.ClearAll();
            ExplosionEffect.ClearAll();

            _rocket.ResetToPosition(_spawnPoint.position);

            if (_missCount >= MissesBeforeHints)
                RoundManagerHUD.Instance?.ShowHints();

            _launchController.EnableInput();
        }

        private void OnDestroy()
        {
            // Defensive: if scene tears down while paused, unfreeze so the next session
            // starts in a clean state (AudioListener.pause persists across scene loads).
            WorldPauseController.Unfreeze();

            StopAllCoroutines();

            if (_rocket != null)
            {
                _rocket.OnRocketLanded -= HandleRocketMiss;
                _rocket.OnTargetHit -= HandleTargetHit;
                _rocket.OnLauncherVehicleHit -= HandleLauncherVehicleHit;
            }

            if (_cameraController != null)
            {
                _cameraController.OnIntroComplete -= OnIntroDone;
                // Defensive: OnLookTargetComplete is subscribed conditionally in HandleLookTarget and self-unsubscribes; safe to remove even if never subscribed.
                _cameraController.OnLookTargetComplete -= OnLookTargetDone;
            }

            // Defensive: unsubscribe ad callback in case restart was interrupted
            if (AdManager.Instance != null)
                AdManager.Instance.OnAdClosed -= OnAdClosedRestart;
        }

        private IEnumerator DelayedAction(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action();
        }
    }
}
