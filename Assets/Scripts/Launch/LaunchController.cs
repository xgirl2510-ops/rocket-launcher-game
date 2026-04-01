using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Slingshot input: touch/click on vehicle → drag to aim → release to launch rocket.
/// Direction = opposite of drag. Force = mapped from drag distance.
/// Auto-reloads rocket after miss. Shows win UI + restart on hit.
/// Randomizes target position each round.
/// </summary>
public class LaunchController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rocket _rocket;
    [SerializeField] private AimArrow _aimArrow;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private CameraController _cameraController;
    [SerializeField] private Transform _targetTransform;

    [Header("Drag Settings")]
    [SerializeField, Range(0.3f, 1f)] private float _minDragDistance = 0.5f;
    [SerializeField, Range(2f, 5f)] private float _maxDragDistance = 3.0f;

    [Header("Launch Force")]
    [SerializeField, Range(3f, 15f)] private float _minLaunchForce = 5f;
    [SerializeField, Range(10f, 40f)] private float _maxLaunchForce = 30f;

    [Header("Vehicle Detection")]
    [SerializeField] private Collider2D _vehicleCollider;

    [Header("Reload")]
    [SerializeField] private float _reloadDelay = 1.5f;

    [Header("Target Randomization")]
    [SerializeField] private float _targetMinX = 8f;
    [SerializeField] private float _targetMaxX = 35f;
    [SerializeField] private float _targetMinY = -4f;
    [SerializeField] private float _targetMaxY = 10f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI _winText;
    [SerializeField] private Button _restartButton;

    private Camera _camera;
    private bool _isDragging;
    private bool _inputEnabled = true;

    private void Awake()
    {
        _camera = Camera.main;
        // Randomize target in Awake so CameraController.Start() sees correct position
        RandomizeTarget();
    }

    private void Start()
    {
        if (_rocket != null)
        {
            _rocket.OnRocketLanded += HandleRocketMiss;
            _rocket.OnTargetHit += HandleTargetHit;
        }

        // Hide UI at start
        if (_winText != null)
            _winText.gameObject.SetActive(false);
        if (_restartButton != null)
        {
            _restartButton.gameObject.SetActive(false);
            _restartButton.onClick.AddListener(HandleRestart);
        }

        // Wait for intro to finish before enabling input
        DisableInput();

        if (_cameraController != null)
        {
            _cameraController.OnIntroComplete += OnIntroDone;
        }
        else
        {
            EnableInput();
        }
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
    }

    private void HandleTouchMoved()
    {
        Vector2 fingerWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 spawnPos = _spawnPoint.position;

        Vector2 dragVector = spawnPos - fingerWorldPos;
        float dragDistance = dragVector.magnitude;

        if (dragDistance < _minDragDistance)
        {
            _aimArrow.Hide();
            return;
        }

        float clampedDistance = Mathf.Min(dragDistance, _maxDragDistance);
        float normalizedForce = (clampedDistance - _minDragDistance) / (_maxDragDistance - _minDragDistance);
        Vector2 launchDirection = dragVector.normalized;

        _aimArrow.Show();
        _aimArrow.UpdateArrow(launchDirection, normalizedForce);
        RotateRocketToDirection(launchDirection);
    }

    private void HandleTouchEnded()
    {
        _isDragging = false;

        Vector2 fingerWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 spawnPos = _spawnPoint.position;

        Vector2 dragVector = spawnPos - fingerWorldPos;
        float dragDistance = dragVector.magnitude;

        _aimArrow.Hide();

        if (dragDistance < _minDragDistance)
        {
            _rocket.transform.rotation = Quaternion.identity;
            return;
        }

        float clampedDistance = Mathf.Min(dragDistance, _maxDragDistance);
        float normalizedForce = (clampedDistance - _minDragDistance) / (_maxDragDistance - _minDragDistance);
        float launchForce = Mathf.Lerp(_minLaunchForce, _maxLaunchForce, normalizedForce);
        Vector2 launchDirection = dragVector.normalized;

        _rocket.Launch(launchDirection, launchForce);
        DisableInput();
    }

    /// <summary>Rocket hit target — hide rocket, show win + restart button.</summary>
    private void HandleTargetHit()
    {
        _rocket.gameObject.SetActive(false);

        if (_winText != null)
            _winText.gameObject.SetActive(true);
        if (_restartButton != null)
            _restartButton.gameObject.SetActive(true);
    }

    /// <summary>Rocket hit ground — wait then auto-reload.</summary>
    private void HandleRocketMiss()
    {
        Invoke(nameof(ReloadRocket), _reloadDelay);
    }

    /// <summary>Return camera, reset rocket, randomize target, re-enable input.</summary>
    private void ReloadRocket()
    {
        if (_cameraController != null)
            _cameraController.ReturnToVehicle();

        _rocket.ResetToPosition(_spawnPoint.position);
        EnableInput();
    }

    /// <summary>Restart button clicked — randomize target, intro pan, then enable input.</summary>
    private void HandleRestart()
    {
        // Hide UI
        if (_winText != null)
            _winText.gameObject.SetActive(false);
        if (_restartButton != null)
            _restartButton.gameObject.SetActive(false);

        // Re-enable rocket
        _rocket.gameObject.SetActive(true);
        _rocket.ResetToPosition(_spawnPoint.position);

        // Randomize target BEFORE intro so camera shows new position
        RandomizeTarget();

        // Intro pan: camera shows target → pans back to vehicle → enable input
        if (_cameraController != null)
        {
            _cameraController.OnIntroComplete -= OnIntroDone;
            _cameraController.OnIntroComplete += OnIntroDone;
            _cameraController.PlayIntro();
        }
        else
        {
            EnableInput();
        }
    }

    /// <summary>Called when intro pan finishes — re-enable input.</summary>
    private void OnIntroDone()
    {
        _cameraController.OnIntroComplete -= OnIntroDone;
        EnableInput();
    }

    /// <summary>Move target to random X and Y position.</summary>
    private void RandomizeTarget()
    {
        if (_targetTransform == null) return;

        float x = Random.Range(_targetMinX, _targetMaxX);
        float y = Random.Range(_targetMinY, _targetMaxY);
        _targetTransform.position = new Vector3(x, y, _targetTransform.position.z);
    }

    private void RotateRocketToDirection(Vector2 direction)
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
        _aimArrow.Hide();
    }
}
