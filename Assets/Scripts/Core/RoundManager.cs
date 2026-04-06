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
        [SerializeField] private ObstacleSpawner _obstacleSpawner;
        [SerializeField] private LaunchController _launchController;

        [Header("Reload")]
        [SerializeField] private float _reloadDelay = 1.5f;

        [Header("Target Randomization")]
        [SerializeField] private float _targetMinX = 8f;
        [SerializeField] private float _targetMaxX = 35f;
        [SerializeField] private float _targetMinY = -4f;
        [SerializeField] private float _targetMaxY = 10f;

        private int _missCount;
        private bool _isAutoPlaying;
        private readonly GameRoundTracker _roundTracker = new GameRoundTracker();

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
            if (_rocket != null)
            {
                _rocket.OnRocketLanded += HandleRocketMiss;
                _rocket.OnTargetHit += HandleTargetHit;
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

            _rocket.ResetToPosition(_spawnPoint.position);

            if (_missCount >= MissesBeforeHints)
                RoundManagerHUD.Instance?.ShowHints();

            _launchController.EnableInput();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();

            if (_rocket != null)
            {
                _rocket.OnRocketLanded -= HandleRocketMiss;
                _rocket.OnTargetHit -= HandleTargetHit;
            }

            if (_cameraController != null)
            {
                _cameraController.OnIntroComplete -= OnIntroDone;
                // Defensive: OnLookTargetComplete is subscribed conditionally in HandleLookTarget and self-unsubscribes; safe to remove even if never subscribed.
                _cameraController.OnLookTargetComplete -= OnLookTargetDone;
            }
        }

        private IEnumerator DelayedAction(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action();
        }
    }
}
