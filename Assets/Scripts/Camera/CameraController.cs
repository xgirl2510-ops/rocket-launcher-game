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
        /// <summary>Possible camera behavior states.</summary>
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
        [Tooltip("Top padding (world units) between rocket and screen top when rocket reaches expected apex. Smaller = rocket sits closer to top edge at apex.")]
        [SerializeField] private float _apexTopPadding = 4f;
        [Tooltip("Height (world units) at which the camera reaches full apex-lag. Smaller = rocket pushes to top edge sooner.")]
        [SerializeField] private float _expectedApexHeight = 40f;
        [Tooltip("Right padding (world units) between rocket and screen right edge at expected max range.")]
        [SerializeField] private float _rangeRightPadding = 4f;
        [Tooltip("Horizontal distance (from launcher) at which the camera reaches full range-lag. Smaller = rocket pushes to right edge sooner.")]
        [SerializeField] private float _expectedRangeDistance = 80f;

        [Header("Dynamic Zoom")]
        // Sized so apex (~55u up) and max range (~105u right) both leave ~10u padding from screen edges.
        // X is the binding constraint: aspect 9/19.5 means halfWidth = ortho * 0.46 → ortho ≥ ~22 to give 10u right-padding.
        // Y at this size shows top-edge ~22u above rocket, which is comfortably above the 10u request.
        [SerializeField] private float _maxOrthoSize = 22f;
        [SerializeField] private float _zoomOutSpeed = 5f;
        // Reach max zoom by the time rocket is roughly mid-trajectory so framing is wide BEFORE apex.
        [SerializeField] private float _zoomMaxDistance = 50f;

        [Header("Follow Bounds")]
        [Tooltip("If true, camera X never goes left of vehicle X — keeps view focused on the playfield.")]
        [SerializeField] private bool _clampLeftToVehicle = true;

        [Header("Background Bounds")]
        // World-space rect of the painted background (bg.png centred at 24.225, 18; size 80×50).
        // Camera centre is clamped so the camera frustum (ortho × aspect) never leaves these bounds —
        // stops the sky/ground BG from "running out of the frame" at high zoom-out.
        [Tooltip("Left edge of background (world X).")]
        [SerializeField] private float _bgMinX = -15.775f;
        [Tooltip("Right edge of background (world X).")]
        [SerializeField] private float _bgMaxX = 64.225f;
        [Tooltip("Bottom edge of background (world Y).")]
        [SerializeField] private float _bgMinY = -7f;
        [Tooltip("Top edge of background (world Y).")]
        [SerializeField] private float _bgMaxY = 43f;
        [Tooltip("If true, camera centre is clamped inside the BG rect so empty space never shows.")]
        [SerializeField] private bool _clampToBackgroundBounds = true;

        [Header("Return")]
        [SerializeField] private float _returnDuration = 1.0f;

        [Header("Look Target")]
        [SerializeField] private float _lookTargetPanDuration = 1.0f;
        [SerializeField] private float _lookTargetPauseDuration = 2f;

        /// <summary>Fired when the intro pan finishes and camera is idle.</summary>
        public event Action OnIntroComplete;
        /// <summary>Fired when the look-at-target sequence completes and camera returns to idle.</summary>
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[CameraController] Missing references — wire via editor tool (Tools > Rocket Launcher > Setup Scene).");
#endif
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

            float orthoNow = _camera.orthographicSize;
            float halfWidth = orthoNow * _camera.aspect;

            // ----- X-axis lag follow: rocket drifts toward right edge as it flies forward -----
            // At launcher (rangeT = 0): camera locked on rocket.X (rocket centered horizontally).
            // At expected range (rangeT = 1): camera at rocket.X - (halfWidth - _rangeRightPadding)
            //                                  so right-edge sits exactly _rangeRightPadding ahead of rocket.
            float rocketX = _rocket.transform.position.x;
            float baseX = _vehicleTransform != null ? _vehicleTransform.position.x : 0f;
            float distAhead = Mathf.Max(0f, rocketX - baseX);
            float rangeT = Mathf.Clamp01(distAhead / Mathf.Max(_expectedRangeDistance, 0.01f));

            float lockedOffsetX = 0f;                                   // rocket centered horizontally
            float rangeOffsetX = -(halfWidth - _rangeRightPadding);     // rocket drifted toward right edge
            float dynamicOffsetX = Mathf.Lerp(lockedOffsetX, rangeOffsetX, rangeT);
            float targetX = rocketX + dynamicOffsetX;

            // Asymmetric follow: rocket should never push the camera left of the vehicle —
            // forward-only gameplay means there's nothing of interest behind the launcher.
            if (_clampLeftToVehicle && _vehicleTransform != null)
                targetX = Mathf.Max(targetX, _vehicleTransform.position.x);

            // ----- Y-axis lag follow with dead-zone -----
            // Camera HOLDS at _homeY while the rocket is still below it — that's the "intro pan
            // settled" position. Only once the rocket climbs ABOVE the camera centre does the
            // Y-follow engage. Without this, launching from a low vehicle yanks the camera DOWN
            // to centre the rocket, making the vehicle appear to leap upward on every shot.
            float rocketY = _rocket.transform.position.y;
            float baseY = _vehicleTransform != null ? _vehicleTransform.position.y : _homeY;
            float heightAboveBase = Mathf.Max(0f, rocketY - baseY);
            float heightT = Mathf.Clamp01(heightAboveBase / Mathf.Max(_expectedApexHeight, 0.01f));

            float lockedOffsetY = _followOffsetY;
            float apexOffsetY = -(orthoNow - _apexTopPadding);
            float dynamicOffsetY = Mathf.Lerp(lockedOffsetY, apexOffsetY, heightT);
            float wantTargetY = rocketY + dynamicOffsetY;
            // Dead-zone: never pull the camera BELOW its home Y just because rocket is low.
            float targetY = Mathf.Max(wantTargetY, _homeY);

            // Compute the next ortho size BEFORE clamping so the BG-bounds clamp uses the
            // actual frustum the camera will have this frame, not the previous frame's.
            float dist = Vector2.Distance(_rocket.transform.position, _vehicleTransform.position);
            float zoomT = Mathf.Clamp01(dist / _zoomMaxDistance);
            float targetOrtho = Mathf.Lerp(_defaultOrthoSize, _maxOrthoSize, zoomT);
            float nextOrtho = Mathf.MoveTowards(
                _camera.orthographicSize, targetOrtho, _zoomOutSpeed * Time.deltaTime);
            _camera.orthographicSize = nextOrtho;

            // Clamp camera centre so the visible frustum never leaves the painted BG rect.
            // If the BG is smaller than the frustum in some axis (rare at max zoom), we centre it.
            if (_clampToBackgroundBounds)
            {
                float halfH = nextOrtho;
                float halfW = nextOrtho * _camera.aspect;
                float bgCentreX = (_bgMinX + _bgMaxX) * 0.5f;
                float bgCentreY = (_bgMinY + _bgMaxY) * 0.5f;
                float minX = _bgMinX + halfW;
                float maxX = _bgMaxX - halfW;
                float minY = _bgMinY + halfH;
                float maxY = _bgMaxY - halfH;
                targetX = minX <= maxX ? Mathf.Clamp(targetX, minX, maxX) : bgCentreX;
                targetY = minY <= maxY ? Mathf.Clamp(targetY, minY, maxY) : bgCentreY;
            }

            Vector2 target = new Vector2(targetX, targetY);
            Vector2 current = new Vector2(transform.position.x, transform.position.y);
            Vector2 smoothed = Vector2.SmoothDamp(current, target, ref _smoothVelocity, _followSmoothTime);
            SetCameraXY(smoothed.x, smoothed.y);
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
