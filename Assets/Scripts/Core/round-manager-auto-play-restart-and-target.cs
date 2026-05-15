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
        /// <summary>Restart button clicked -- show ad if needed, then randomize target, intro pan, enable input.</summary>
        public void HandleRestart()
        {
            // Unfreeze in case we're restarting from a friendly-fire game-over.
            // Idempotent — normal restarts (not frozen) are no-ops.
            WorldPauseController.Unfreeze();

            _isAutoPlaying = false;
            // Stop only RoundManager's own coroutines (reload/autoplay delays).
            // CameraController manages its own coroutines via PlayIntro -> StopActiveCoroutine.
            StopAllCoroutines();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayClick();

            RoundManagerHUD.Instance?.HideWinUI();
            RoundManagerHUD.Instance?.HideGameOverUI();
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
            if (AdManager.Instance != null)
                AdManager.Instance.OnAdClosed -= OnAdClosedRestart;
            StartNewRound();
        }

        private void StartNewRound()
        {
            ResetGameState();
            PrepareNewRound();
        }

        private void ResetGameState()
        {
            RocketDebris.ClearAll();
            // Clear craters when starting a new round — the battlefield resets between rounds.
            // (Within a round, missed shots accumulate craters via GroundScorch.Spawn, and
            // RemoveOverlappingCraters dedupes shots that land on the same spot.)
            GroundScorch.ClearAll();
            ExplosionEffect.ClearAll();
            _rocket.gameObject.SetActive(true);
            _rocket.ResetToPosition(_spawnPoint.position);
            if (_targetTransform != null) _targetTransform.gameObject.SetActive(true);
            // Launcher vehicle gets disabled on friendly-fire game-over — re-enable on restart.
            if (_launcherVehicleTransform != null) _launcherVehicleTransform.gameObject.SetActive(true);
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
            // Auto-play uses the SAME physics (drag + thrust) the player flies under because the
            // dive solver simulated those exact params to find this (dir, force) pair. Switching
            // to pure ballistic would diverge from the solved trajectory and miss the dive slot.
            _rocket.Launch(dir, force);
            RocketTrajectoryPredictor.Instance.OnRocketLaunched(_spawnPoint.position, dir, force);
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

        // Max re-rolls before we accept whatever position we last tried — protects against
        // pathological config combos where no point in [min..max] is solvable. With the current
        // range (X[8..36] Y[-4..8]) every roll has been observed to solve in ≤2 attempts.
        private const int MaxTargetRerolls = 10;

        private void RandomizeTarget()
        {
            if (_targetTransform == null) return;

            // Re-roll the target position until ObstacleSpawner.RespawnObstacles reports a valid
            // dive solution (apex above target, descent ≥60°, within rocket reach). If we exhaust
            // retries we keep the last roll so the round still starts — the puzzle just won't
            // have its full dive structure that one round.
            for (int attempt = 0; attempt < MaxTargetRerolls; attempt++)
            {
                float x = Random.Range(_targetMinX, _targetMaxX);
                float y = Random.Range(_targetMinY, _targetMaxY);
                _targetTransform.position = new Vector3(x, y, _targetTransform.position.z);

                if (_obstacleSpawner == null) return;
                if (_obstacleSpawner.RespawnObstacles()) return;
            }
            Debug.LogWarning("[RoundManager] No dive solution after " + MaxTargetRerolls + " rerolls — keeping last target.");
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
