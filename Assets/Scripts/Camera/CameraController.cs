using System;
using System.Collections;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Camera state machine: Intro pan (Target -> Vehicle), Follow rocket, Return to vehicle.
    /// Uses LateUpdate for smooth follow after physics. Camera Z always stays at -10.
    /// Includes screen shake on rocket impact.
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

        // Screen shake state
        private float _shakeDuration;
        private float _shakeMagnitude;
        private float _shakeElapsed;

        // Prevent coroutine race — only one camera transition at a time
        private Coroutine _activeCoroutine;

        private void Awake()
        {
            _defaultZ = transform.position.z;
            _camera = GetComponent<Camera>();
            _defaultOrthoSize = _camera.orthographicSize;
        }

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

            Vector2 startPos = new Vector2(transform.position.x, transform.position.y);
            Vector2 endPos = _vehicleTransform != null
                ? new Vector2(_vehicleTransform.position.x, _homeY)
                : startPos;

            float elapsed = 0f;
            while (elapsed < _introPanDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _introPanDuration);
                Vector2 pos = Vector2.Lerp(startPos, endPos, t);
                SetCameraXY(pos.x, pos.y);
                yield return null;
            }

            SetCameraXY(endPos.x, endPos.y);
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

            Vector2 startPos = new Vector2(transform.position.x, transform.position.y);
            Vector2 endPos = _vehicleTransform != null
                ? new Vector2(_vehicleTransform.position.x, _homeY)
                : startPos;
            float startOrtho = _camera.orthographicSize;

            float elapsed = 0f;
            while (elapsed < _returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _returnDuration);
                Vector2 pos = Vector2.Lerp(startPos, endPos, t);
                SetCameraXY(pos.x, pos.y);
                _camera.orthographicSize = Mathf.Lerp(startOrtho, _defaultOrthoSize, t);
                yield return null;
            }

            SetCameraXY(endPos.x, endPos.y);
            _camera.orthographicSize = _defaultOrthoSize;
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

            Vector2 startPos = new Vector2(transform.position.x, transform.position.y);
            Vector2 targetPos = _targetTransform != null
                ? new Vector2(_targetTransform.position.x, _homeY)
                : startPos;

            float elapsed = 0f;
            while (elapsed < _lookTargetPanDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _lookTargetPanDuration);
                Vector2 pos = Vector2.Lerp(startPos, targetPos, t);
                SetCameraXY(pos.x, pos.y);
                yield return null;
            }
            SetCameraXY(targetPos.x, targetPos.y);

            yield return new WaitForSeconds(_lookTargetPauseDuration);

            Vector2 vehiclePos = _vehicleTransform != null
                ? new Vector2(_vehicleTransform.position.x, _homeY)
                : targetPos;

            elapsed = 0f;
            while (elapsed < _lookTargetPanDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / _lookTargetPanDuration);
                Vector2 pos = Vector2.Lerp(targetPos, vehiclePos, t);
                SetCameraXY(pos.x, pos.y);
                yield return null;
            }
            SetCameraXY(vehiclePos.x, vehiclePos.y);

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
            _currentState = newState;
            _smoothVelocity = Vector2.zero;
        }

        /// <summary>Trigger screen shake. Small shake on miss, bigger on hit.</summary>
        public void Shake(float duration, float magnitude)
        {
            _shakeDuration = duration;
            _shakeMagnitude = magnitude;
            _shakeElapsed = 0f;
        }

        private void SetCameraXY(float x, float y)
        {
            Vector2 shakeOffset = Vector2.zero;

            if (_shakeElapsed < _shakeDuration)
            {
                _shakeElapsed += Time.deltaTime;
                float decay = 1f - (_shakeElapsed / _shakeDuration);
                shakeOffset = UnityEngine.Random.insideUnitCircle * _shakeMagnitude * decay;
            }

            transform.position = new Vector3(x + shakeOffset.x, y + shakeOffset.y, _defaultZ);
        }
    }
}
