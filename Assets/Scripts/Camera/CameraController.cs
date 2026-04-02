using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Camera state machine: Intro pan (Target → Vehicle), Follow rocket, Landed, Return to vehicle.
/// Uses LateUpdate for smooth follow after physics. Camera Z always stays at -10.
/// Includes screen shake on rocket impact.
/// </summary>
public class CameraController : MonoBehaviour
{
    public enum CameraState { Intro, Idle, Following, Landed, Returning }

    [Header("References")]
    [SerializeField] private Rocket _rocket;
    [SerializeField] private Transform _vehicleTransform;
    [SerializeField] private Transform _targetTransform;

    [Header("Intro Pan")]
    [SerializeField] private float _introPauseDuration = 1.0f;
    [SerializeField] private float _introPanDuration = 1.5f;

    [Header("Camera Home")]
    [SerializeField] private float _homeY = 2f;  // Must match CamY in scene setup

    [Header("Follow")]
    [SerializeField] private float _followSmoothTime = 0.05f;
    [SerializeField] private float _followOffsetY = 2f;

    [Header("Return")]
    [SerializeField] private float _returnSmoothTime = 0.5f;
    [SerializeField] private float _returnThreshold = 0.1f;

    public event Action OnIntroComplete;
    public event Action OnLookTargetComplete;

    private CameraState _currentState;
    private float _defaultZ;
    private Vector2 _smoothVelocity;

    // Screen shake state
    private float _shakeDuration;
    private float _shakeMagnitude;
    private float _shakeElapsed;

    private void Awake()
    {
        _defaultZ = transform.position.z;
    }

    private void Start()
    {
        // Auto-find references if not assigned
        if (_rocket == null)
            _rocket = FindAnyObjectByType<Rocket>();

        if (_vehicleTransform == null)
        {
            var vehicle = GameObject.Find("LauncherVehicle");
            if (vehicle != null) _vehicleTransform = vehicle.transform;
        }

        if (_targetTransform == null)
        {
            var target = GameObject.Find("Target");
            if (target != null) _targetTransform = target.transform;
        }

        // Subscribe to rocket events
        if (_rocket != null)
        {
            _rocket.OnRocketLaunched += () => SetState(CameraState.Following);
            _rocket.OnRocketLanded += () => SetState(CameraState.Landed);
            _rocket.OnTargetHit += () => SetState(CameraState.Landed);
        }

        // Start intro pan
        PlayIntro();
    }

    /// <summary>Start the intro pan: snap to Target, pause, pan to Vehicle.</summary>
    public void PlayIntro()
    {
        _currentState = CameraState.Intro;
        StartCoroutine(IntroCoroutine());
    }

    private IEnumerator IntroCoroutine()
    {
        // Snap camera to Target — use target X but fixed camera Y
        if (_targetTransform != null)
        {
            SetCameraXY(_targetTransform.position.x, _homeY);
        }

        // Pause at Target
        yield return new WaitForSeconds(_introPauseDuration);

        // Pan from Target to Vehicle — camera Y stays at _homeY
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

        // Ensure exact final position
        SetCameraXY(endPos.x, endPos.y);

        _currentState = CameraState.Idle;
        OnIntroComplete?.Invoke();
        Debug.Log("[CameraController] Intro complete — camera at vehicle.");
    }

    private void LateUpdate()
    {
        switch (_currentState)
        {
            case CameraState.Intro:
                // Handled by coroutine
                break;

            case CameraState.Idle:
                // Camera stays at vehicle
                break;

            case CameraState.Following:
                FollowRocket();
                break;

            case CameraState.Landed:
                // Camera stays where rocket landed
                break;

            case CameraState.Returning:
                ReturnToVehicle();
                break;
        }
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
    }

    /// <summary>Called by GameManager after rocket lands to pan back to vehicle.</summary>
    public void ReturnToVehicle()
    {
        if (_currentState != CameraState.Returning)
        {
            SetState(CameraState.Returning);
            return;
        }

        if (_vehicleTransform == null) return;

        Vector2 target = new Vector2(_vehicleTransform.position.x, _homeY);
        Vector2 current = new Vector2(transform.position.x, transform.position.y);
        Vector2 smoothed = Vector2.SmoothDamp(current, target, ref _smoothVelocity, _returnSmoothTime);
        SetCameraXY(smoothed.x, smoothed.y);

        if (Vector2.Distance(smoothed, target) < _returnThreshold)
        {
            SetCameraXY(target.x, target.y);
            SetState(CameraState.Idle);
        }
    }

    /// <summary>Pan camera to target, wait 2s, pan back to vehicle.</summary>
    public void PanToTarget()
    {
        if (_currentState == CameraState.Following) return;
        StartCoroutine(PanToTargetCoroutine());
    }

    private IEnumerator PanToTargetCoroutine()
    {
        _currentState = CameraState.Intro; // block other movement

        float panDuration = 1.0f;

        // Pan to target
        Vector2 startPos = new Vector2(transform.position.x, transform.position.y);
        Vector2 targetPos = _targetTransform != null
            ? new Vector2(_targetTransform.position.x, _homeY)
            : startPos;

        float elapsed = 0f;
        while (elapsed < panDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / panDuration);
            Vector2 pos = Vector2.Lerp(startPos, targetPos, t);
            SetCameraXY(pos.x, pos.y);
            yield return null;
        }
        SetCameraXY(targetPos.x, targetPos.y);

        // Wait at target
        yield return new WaitForSeconds(2f);

        // Pan back to vehicle
        Vector2 vehiclePos = _vehicleTransform != null
            ? new Vector2(_vehicleTransform.position.x, _homeY)
            : targetPos;

        elapsed = 0f;
        while (elapsed < panDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / panDuration);
            Vector2 pos = Vector2.Lerp(targetPos, vehiclePos, t);
            SetCameraXY(pos.x, pos.y);
            yield return null;
        }
        SetCameraXY(vehiclePos.x, vehiclePos.y);

        _currentState = CameraState.Idle;
        OnLookTargetComplete?.Invoke();
    }

    private void SetState(CameraState newState)
    {
        _currentState = newState;
        _smoothVelocity = Vector2.zero;
    }

    /// <summary>
    /// Trigger screen shake. Small shake on miss, bigger on hit.
    /// </summary>
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
