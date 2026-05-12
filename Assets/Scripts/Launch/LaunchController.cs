using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Slingshot input only: touch/click on vehicle, drag to aim, release to launch rocket.
    /// Direction = opposite of drag. Force = mapped from drag distance.
    /// Round flow (miss/hit/reload/restart) handled by RoundManager.
    /// </summary>
    public class LaunchController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rocket _rocket;
        [SerializeField] private AimArrow _aimArrow;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private RoundManager _roundManager;

        [Header("Drag Settings")]
        // Tiny threshold so the aim arrow appears the instant the player starts dragging,
        // instead of waiting until the drag exceeds half a world unit. Anything > 0 still
        // prevents division-by-zero on stationary clicks.
        [SerializeField, Range(0.01f, 1f)] private float _minDragDistance = 0.05f;
        [SerializeField, Range(2f, 5f)] private float _maxDragDistance = 3.0f;

        [Header("Vehicle Detection")]
        [SerializeField] private Collider2D _vehicleCollider;

        private Camera _camera;
        private bool _isDragging;
        private bool _inputEnabled;
        private bool _stretchPlayed;

        private void Awake()
        {
            _camera = Camera.main;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_camera == null) Debug.LogError("[LaunchController] No main camera found.", this);
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!gameObject.scene.isLoaded) return;

            if (_rocket == null) Debug.LogWarning("[LaunchController] _rocket not assigned.", this);
            if (_spawnPoint == null) Debug.LogWarning("[LaunchController] _spawnPoint not assigned.", this);
            if (_roundManager == null) Debug.LogWarning("[LaunchController] _roundManager not assigned.", this);
        }
#endif

        private void Update()
        {
            if (!_inputEnabled) return;

            if (Input.GetMouseButtonDown(0))
                HandleTouchBegan();
            else if (Input.GetMouseButton(0) && _isDragging)
                HandleTouchMoved();
            else if (Input.GetMouseButtonUp(0) && _isDragging)
                HandleTouchEnded();
        }

        private void HandleTouchBegan()
        {
            Vector2 worldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
            if (_vehicleCollider == null || !_vehicleCollider.OverlapPoint(worldPos)) return;
            _isDragging = true;
            _stretchPlayed = false;
        }

        private void HandleTouchMoved()
        {
            if (!TryComputeDrag(out Vector2 launchDirection, out float normalizedForce))
            {
                if (_aimArrow != null) _aimArrow.Hide();
                return;
            }

            // Clamp launch direction to valid angle range; report whether clamped or near limit
            launchDirection = ClampLaunchAngle(launchDirection, out AimAngleStatus status);

            if (_aimArrow != null)
            {
                _aimArrow.Show();
                _aimArrow.UpdateArrow(launchDirection, normalizedForce, status);
            }
            RotateRocketToDirection(launchDirection);

            if (!_stretchPlayed && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayStretch();
                _stretchPlayed = true;
            }

            RoundManagerHUD.Instance?.UpdateHintTexts(launchDirection, normalizedForce);
        }

        private void HandleTouchEnded()
        {
            _isDragging = false;
            if (_aimArrow != null) _aimArrow.Hide();

            if (!TryComputeDrag(out Vector2 launchDirection, out float normalizedForce))
            {
                _rocket.transform.rotation = Quaternion.identity;
                return;
            }

            // Apply same angle clamp on release so launch matches what the player saw on the arrow
            launchDirection = ClampLaunchAngle(launchDirection, out _);

            float launchForce = Mathf.Lerp(GameConstants.MinLaunchForce, GameConstants.MaxLaunchForce, normalizedForce);

            if (_roundManager != null)
            {
                _roundManager.OnShotFired();
                RoundManagerHUD.Instance?.UpdateStatsUI(_roundManager.RoundTracker);
            }

            _rocket.Launch(launchDirection, launchForce);

            // Notify predictor with the EXACT launch parameters so it can find the first-hit
            // jet immediately — without depending on physics having stepped to populate
            // rb.linearVelocity. The predictor will tell that single jet to fire its interceptor.
            RocketTrajectoryPredictor.Instance.OnRocketLaunched(
                _rocket.transform.position, launchDirection, launchForce);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayLaunch();
                AudioManager.Instance.StartThrust();
            }
            DisableInput();
        }

        /// <summary>
        /// Computes drag vector from current mouse position to spawn point.
        /// Returns false if drag distance is below minimum threshold.
        /// </summary>
        private bool TryComputeDrag(out Vector2 direction, out float normalizedForce)
        {
            Vector2 fingerWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 spawnPos = _spawnPoint.position;

            Vector2 dragVector = spawnPos - fingerWorldPos;
            float rawDistance = dragVector.magnitude;

            if (rawDistance < _minDragDistance)
            {
                direction = Vector2.zero;
                normalizedForce = 0f;
                return false;
            }

            float clampedDistance = Mathf.Min(rawDistance, _maxDragDistance);
            normalizedForce = (clampedDistance - _minDragDistance) / (_maxDragDistance - _minDragDistance);
            direction = dragVector.normalized;
            return true;
        }

        /// <summary>Rotate rocket sprite to face the given direction.</summary>
        public void RotateRocketToDirection(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + GameConstants.SpriteAngleOffset;
            _rocket.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// Clamp launch direction to allowed angle range [MinLaunchAngleDeg, MaxLaunchAngleDeg].
        /// Returns the (possibly clamped) direction and reports whether it was clamped or near the limit.
        /// </summary>
        private static Vector2 ClampLaunchAngle(Vector2 direction, out AimAngleStatus status)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float min = GameConstants.MinLaunchAngleDeg;
            float max = GameConstants.MaxLaunchAngleDeg;
            float warn = GameConstants.LaunchAngleWarnMarginDeg;

            float clamped = Mathf.Clamp(angle, min, max);
            bool wasClamped = !Mathf.Approximately(clamped, angle);

            if (wasClamped)
            {
                status = AimAngleStatus.Clamped;
            }
            else if (clamped <= min + warn || clamped >= max - warn)
            {
                status = AimAngleStatus.NearLimit;
            }
            else
            {
                status = AimAngleStatus.Valid;
            }

            float rad = clamped * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        /// <summary>Allow slingshot input (called after intro pan or reload).</summary>
        public void EnableInput()
        {
            _inputEnabled = true;
        }

        /// <summary>Block slingshot input (called during flight or camera pan).</summary>
        public void DisableInput()
        {
            _inputEnabled = false;
            _isDragging = false;
            _aimArrow?.Hide();
        }
    }
}
