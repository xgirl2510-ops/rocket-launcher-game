using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Partial class: restart, auto-play, look-target actions, target randomization.
    /// Separated from RoundManager core (event handlers, reload, lifecycle) for file size.
    /// </summary>
    public partial class RoundManager
    {
        private const float MinDirectionSqr = 0.01f;
        /// <summary>Restart button clicked -- randomize target, intro pan, then enable input.</summary>
        public void HandleRestart()
        {
            _isAutoPlaying = false;
            // Stop only RoundManager's own coroutines (reload/autoplay delays).
            // CameraController manages its own coroutines via PlayIntro -> StopActiveCoroutine.
            StopAllCoroutines();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayClick();

            RoundManagerHUD.Instance?.HideWinUI();
            RoundManagerHUD.Instance?.HideHints();

            ResetGameState();
            PrepareNewRound();
        }

        private void ResetGameState()
        {
            RocketDebris.ClearAll();
            GroundScorch.ClearAll();
            _rocket.gameObject.SetActive(true);
            _rocket.ResetToPosition(_spawnPoint.position);
            if (_targetTransform != null) _targetTransform.gameObject.SetActive(true);
            _missCount = 0;
        }

        private void PrepareNewRound()
        {
            _roundTracker.NewRound();
            RoundManagerHUD.Instance?.UpdateStatsUI(_roundTracker);

            RandomizeTarget();

            if (_cameraController != null)
            {
                // Defensive: guard against restart during intro animation before OnIntroDone fires
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
            if (dir.sqrMagnitude < MinDirectionSqr) return;

            RoundManagerHUD.Instance?.HideAutoPlayButton();
            RoundManagerHUD.Instance?.HideHints();

            _isAutoPlaying = true;
            _rocket.gameObject.SetActive(true);
            _rocket.ResetToPosition(_spawnPoint.position);
            _launchController.RotateRocketToDirection(dir);
            _rocket.Launch(dir, force);
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLaunch();
                AudioManager.Instance.StartThrust();
            }
            _launchController.DisableInput();
        }

        /// <summary>Look Target button -- pan camera to target, wait, pan back.</summary>
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

            _roundTracker.NewRound();
            RoundManagerHUD.Instance?.UpdateStatsUI(_roundTracker);

            if (_cameraController != null)
                _cameraController.ReturnToVehicle();

            _rocket.gameObject.SetActive(true);
            _rocket.ResetToPosition(_spawnPoint.position);
            if (_targetTransform != null) _targetTransform.gameObject.SetActive(true);
            _missCount = 0;
            _launchController.EnableInput();
        }
    }
}
