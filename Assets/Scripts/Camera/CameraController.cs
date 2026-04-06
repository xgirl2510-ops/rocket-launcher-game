using System;
using System.Collections;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Camera state machine: Intro pan (Target -> Vehicle), Follow rocket, Return to vehicle.
    /// Uses LateUpdate for smooth follow after physics. Camera Z always stays at -10.
    /// Screen shake delegated to CameraScreenShake component.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public enum CameraState { Intro, Idle, Following, Returning, LookingAtTarget }

        [Header("References")]
        [SerializeField] private Rocket _rocket;
        [SerializeField] private Transform _vehicleTransform;
        [SerializeField] private Transform _targetTransform;

        [Header("Intro Pan")]
        [SerializeField] private float _introPauseDuration = 1.0f;
        [SerializeField] private float _introPanDuration = 1.5f;

        [Header("Camera Home")]
        [SerializeField] private float _homeY = 2f;

        [Header("Follow")]
        [SerializeField] private float _followSmoothTime = 0.12f;
        [SerializeField] private float _followOffsetY = 2f;

        [Header("Dynamic Zoom")]
        [SerializeField] private float _maxOrthoSize = 25f;
        [SerializeField] private float _zoomOutSpeed = 5f;
        [SerializeField] private float _zoomMaxDistance = 40f;

        [Header("Return")]
        [SerializeField] private float _returnDuration = 1.0f;

        [Header("Look Target")]
        [SerializeField] private float _lookTargetPanDuration = 1.0f;
        [SerializeField] private float _lookTargetPauseDuration = 2f;

        public event Action OnIntroComplete;
        public event Action OnLookTargetComplete;

        private CameraState _currentState;
        private float _defaultZ;
        private Vector2 _smoothVelocity;
        private Camera _camera;
        private float _defaultOrthoSize;

        private CameraScreenShake _shake;

        // Prevent coroutine race — only one camera transition at a time
        private Coroutine _activeCoroutine;

        private void Awake()
        {
            _defaultZ = transform.position.z;
            _camera = GetComponent<Camera>();
            _defaultOrthoSize = _camera.orthographicSize;
            _shake = GetComponent<CameraScreenShake>();
            if (_shake == null) _shake = gameObject.AddComponent<CameraScreenShake>();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!gameObject.scene.isLoaded) return;

            if (_rocket == null) Debug.LogWarning("[CameraController] _rocket not assigned.", this);
            if (_vehicleTransform == null) Debug.LogWarning("[CameraController] _vehicleTransform not assigned.", this);
            if (_targetTransform == null) Debug.LogWarning("[CameraController] _targetTransform not assigned.", this);
        }
#endif

        private void Start()
        {
            if (_rocket == null || _vehicleTransform == null || _targetTransform == null)
            {
                Debug.LogError("[CameraController] Missing references — wire via editor tool (Tools > Rocket Launcher > Setup Scene).");
                enabled = false;
                return;
            }

            _rocket.OnRocketLaunched += HandleRocketLaunched;
            _rocket.OnRocketLanded += HandleRocketLanded;
            _rocket.OnTargetHit += HandleRocketHitTarget;

            PlayIntro();
        }

        /// <summary>Start the intro pan: snap to Target, pause, pan to Vehicle.</summary>
        public void PlayIntro()
        {
            StopActiveCoroutine();
            _currentState = CameraState.Intro;
            _activeCoroutine = StartCoroutine(IntroCoroutine());
        }

        private IEnumerator IntroCoroutine()
        {
            _camera.orthographicSize = _defaultOrthoSize;

            if (_targetTransform != null)
                SetCameraXY(_targetTransform.position.x, _homeY);

            yield return new WaitForSeconds(_introPauseDuration);

            yield return PanCoroutine(CurrentXY, VehicleHome, _introPanDuration);

            _currentState = CameraState.Idle;
            OnIntroComplete?.Invoke();
        }

        private void LateUpdate()
        {
            if (_currentState == CameraState.Following)
                FollowRocket();
        }

        private void FollowRocket()
        {
            if (_rocket == null) return;

            Vector2 target = new Vector2(
                _rocket.transform.position.x,
                _rocket.transform.position.y + _followOffsetY);

            Vector2 current = new Vector2(transform.position.x, transform.position.y);
            Vector2 smoothed = Vector2.SmoothDamp(current, target, ref _smoothVelocity, _followSmoothTime);
            SetCameraXY(smoothed.x, smoothed.y);

            float dist = Vector2.Distance(_rocket.transform.position, _vehicleTransform.position);
            float zoomT = Mathf.Clamp01(dist / _zoomMaxDistance);
            float targetOrtho = Mathf.Lerp(_defaultOrthoSize, _maxOrthoSize, zoomT);
            _camera.orthographicSize = Mathf.MoveTowards(
                _camera.orthographicSize, targetOrtho, _zoomOutSpeed * Time.deltaTime);
        }

        /// <summary>Called after rocket lands to smoothly pan back to vehicle.</summary>
        public void ReturnToVehicle()
        {
            if (_currentState == CameraState.Returning) return;
            StopActiveCoroutine();
            _activeCoroutine = StartCoroutine(ReturnToVehicleCoroutine());
        }

        private IEnumerator ReturnToVehicleCoroutine()
        {
            _currentState = CameraState.Returning;

            yield return PanCoroutine(CurrentXY, VehicleHome, _returnDuration,
                _camera.orthographicSize, _defaultOrthoSize);

            _currentState = CameraState.Idle;
        }

        /// <summary>Pan camera to target, pause, pan back to vehicle.</summary>
        public void PanToTarget()
        {
            if (_currentState == CameraState.Following) return;
            StopActiveCoroutine();
            _activeCoroutine = StartCoroutine(PanToTargetCoroutine());
        }

        private void StopActiveCoroutine()
        {
            if (_activeCoroutine != null)
            {
                StopCoroutine(_activeCoroutine);
                _activeCoroutine = null;
            }
        }

        private IEnumerator PanToTargetCoroutine()
        {
            _currentState = CameraState.LookingAtTarget;

            Vector2 targetPos = _targetTransform != null
                ? new Vector2(_targetTransform.position.x, _homeY)
                : CurrentXY;

            yield return PanCoroutine(CurrentXY, targetPos, _lookTargetPanDuration);
            yield return new WaitForSeconds(_lookTargetPauseDuration);
            yield return PanCoroutine(targetPos, VehicleHome, _lookTargetPanDuration);

            _currentState = CameraState.Idle;
            OnLookTargetComplete?.Invoke();
        }

        private void HandleRocketLaunched() => SetState(CameraState.Following);

        // Rocket landed/hit — camera stays at current position until ReturnToVehicle() is called
        private void HandleRocketLanded() => SetState(CameraState.Idle);
        private void HandleRocketHitTarget() => SetState(CameraState.Idle);

        private void OnDestroy()
        {
            if (_rocket != null)
            {
                _rocket.OnRocketLaunched -= HandleRocketLaunched;
                _rocket.OnRocketLanded -= HandleRocketLanded;
                _rocket.OnTargetHit -= HandleRocketHitTarget;
            }
        }

        private void SetState(CameraState newState)
        {
            StopActiveCoroutine();
            _currentState = newState;
            _smoothVelocity = Vector2.zero;
        }

        /// <summary>Trigger screen shake. Small shake on miss, bigger on hit.</summary>
        public void Shake(float duration, float magnitude)
        {
            if (_shake != null) _shake.Shake(duration, magnitude);
        }

        /// <summary>
        /// Smooth pan from <paramref name="from"/> to <paramref name="to"/> over <paramref name="duration"/> seconds.
        /// Optionally lerps orthographic size when both ortho params are provided.
        /// </summary>
        private IEnumerator PanCoroutine(Vector2 from, Vector2 to, float duration,
            float orthoFrom = -1f, float orthoTo = -1f)
        {
            bool lerpOrtho = orthoFrom >= 0f && orthoTo >= 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                SetCameraXY(Vector2.Lerp(from, to, t));
                if (lerpOrtho) _camera.orthographicSize = Mathf.Lerp(orthoFrom, orthoTo, t);
                yield return null;
            }
            SetCameraXY(to);
            if (lerpOrtho) _camera.orthographicSize = orthoTo;
        }

        private Vector2 CurrentXY => new Vector2(transform.position.x, transform.position.y);
        private Vector2 VehicleHome => _vehicleTransform != null
            ? new Vector2(_vehicleTransform.position.x, _homeY)
            : CurrentXY;

        private void SetCameraXY(Vector2 pos) => SetCameraXY(pos.x, pos.y);

        private void SetCameraXY(float x, float y)
        {
            Vector2 shakeOffset = _shake != null ? _shake.GetOffset() : Vector2.zero;
            transform.position = new Vector3(x + shakeOffset.x, y + shakeOffset.y, _defaultZ);
        }
    }
}
