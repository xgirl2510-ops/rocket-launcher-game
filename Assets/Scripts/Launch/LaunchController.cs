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
        [SerializeField, Range(0.3f, 1f)] private float _minDragDistance = 0.5f;
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
        }

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

            if (_aimArrow != null)
            {
                _aimArrow.Show();
                _aimArrow.UpdateArrow(launchDirection, normalizedForce);
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

            float launchForce = Mathf.Lerp(GameConstants.MinLaunchForce, GameConstants.MaxLaunchForce, normalizedForce);

            _roundManager.OnShotFired();
            RoundManagerHUD.Instance?.UpdateStatsUI(_roundManager.RoundTracker);

            _rocket.Launch(launchDirection, launchForce);
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
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            _rocket.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void EnableInput()
        {
            _inputEnabled = true;
        }

        public void DisableInput()
        {
            _inputEnabled = false;
            _isDragging = false;
            _aimArrow?.Hide();
        }
    }
}
