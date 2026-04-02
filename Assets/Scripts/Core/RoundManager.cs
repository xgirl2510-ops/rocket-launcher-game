using System.Collections;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Manages round flow: miss/hit handling, reload, restart, target randomization, auto-play.
    /// Subscribes to Rocket events and delegates camera/audio/HUD updates.
    /// LaunchController handles only slingshot input; this class handles everything after launch.
    /// </summary>
    public class RoundManager : MonoBehaviour
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
        public ObstacleSpawner ObstacleSpawner => _obstacleSpawner;
        public bool IsAutoPlaying => _isAutoPlaying;

        private const int MissesBeforeHints = 5;

        private void Awake()
        {
            RandomizeTarget();
        }

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

        /// <summary>Restart button clicked — randomize target, intro pan, then enable input.</summary>
        public void HandleRestart()
        {
            StopAllCoroutines();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayClick();

            RoundManagerHUD.Instance?.HideWinUI();
            RoundManagerHUD.Instance?.HideHints();

            RocketDebris.ClearAll();
            GroundScorch.ClearAll();
            _rocket.gameObject.SetActive(true);
            _rocket.ResetToPosition(_spawnPoint.position);
            if (_targetTransform != null) _targetTransform.gameObject.SetActive(true);
            _missCount = 0;

            _roundTracker.NewRound();
            RoundManagerHUD.Instance?.UpdateStatsUI(_roundTracker);

            RandomizeTarget();

            if (_cameraController != null)
            {
                _cameraController.OnIntroComplete -= OnIntroDone;
                _cameraController.OnIntroComplete += OnIntroDone;
                _cameraController.PlayIntro();
            }
            else
            {
                _launchController.EnableInput();
            }
        }

        /// <summary>Auto-play: launch along safe trajectory, then reset for player.</summary>
        public void HandleAutoPlay()
        {
            if (_obstacleSpawner == null) return;

            StopAllCoroutines();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayClick();

            Vector2 dir = _obstacleSpawner.SafeLaunchDirection;
            float force = _obstacleSpawner.SafeLaunchForce;
            if (dir.sqrMagnitude < 0.01f) return;

            RoundManagerHUD.Instance?.HideAutoPlayButton();

            _isAutoPlaying = true;
            _launchController.RotateRocketToDirection(dir);
            _rocket.Launch(dir, force);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLaunch();
                AudioManager.Instance.StartThrust();
            }
            _launchController.DisableInput();
        }

        /// <summary>Look Target button — pan camera to target, wait, pan back.</summary>
        public void HandleLookTarget()
        {
            if (_rocket != null && _rocket.IsFlying) return;
            if (_cameraController == null) return;

            _launchController.DisableInput();
            _cameraController.OnLookTargetComplete -= OnLookTargetDone;
            _cameraController.OnLookTargetComplete += OnLookTargetDone;
            _cameraController.PanToTarget();
        }

        private void OnIntroDone()
        {
            _cameraController.OnIntroComplete -= OnIntroDone;
            _launchController.EnableInput();
        }

        private void OnLookTargetDone()
        {
            _cameraController.OnLookTargetComplete -= OnLookTargetDone;
            _launchController.EnableInput();
        }

        private void RandomizeTarget()
        {
            if (_targetTransform == null) return;

            float x = Random.Range(_targetMinX, _targetMaxX);
            float y = Random.Range(_targetMinY, _targetMaxY);
            _targetTransform.position = new Vector3(x, y, _targetTransform.position.z);

            if (_obstacleSpawner != null)
                _obstacleSpawner.RespawnObstacles();
        }

        private void ReloadAfterAutoPlay()
        {
            _isAutoPlaying = false;

            if (_cameraController != null)
                _cameraController.ReturnToVehicle();

            _rocket.gameObject.SetActive(true);
            _rocket.ResetToPosition(_spawnPoint.position);
            if (_targetTransform != null) _targetTransform.gameObject.SetActive(true);
            _missCount = 0;
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
