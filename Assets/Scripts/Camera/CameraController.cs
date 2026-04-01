using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Camera state machine: Intro pan (Target → Vehicle), Follow rocket, Landed, Return to vehicle.
/// Uses LateUpdate for smooth follow after physics. Camera Z always stays at -10.
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

    private CameraState _currentState;
    private float _defaultZ;
    private Vector2 _smoothVelocity;

    private void Awake()
    {
        _defaultZ = transform.position.z;
    }

    private void Start()
    {
        // Auto-find references if not assigned
        if (_rocket == null)
            _rocket = FindFirstObjectByType<Rocket>();

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

    private void SetState(CameraState newState)
    {
        _currentState = newState;
        _smoothVelocity = Vector2.zero;
    }

    private void SetCameraXY(float x, float y)
    {
        transform.position = new Vector3(x, y, _defaultZ);
    }
}
