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
    [SerializeField] private ObstacleSpawner _obstacleSpawner;

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
    [SerializeField] private Button _autoPlayButton;
    [SerializeField] private Button _lookTargetButton;
    [SerializeField] private TextMeshProUGUI _angleText;
    [SerializeField] private TextMeshProUGUI _forceText;
    [SerializeField] private TextMeshProUGUI _statsText;

    [Header("Screen Shake")]
    [SerializeField] private float _missShakeDuration = 0.2f;
    [SerializeField] private float _missShakeMagnitude = 0.1f;
    [SerializeField] private float _hitShakeDuration = 0.3f;
    [SerializeField] private float _hitShakeMagnitude = 0.2f;

    private Camera _camera;
    private bool _isDragging;
    private bool _inputEnabled = true;
    private bool _stretchPlayed;
    private int _missCount;
    private bool _isAutoPlaying;
    private readonly GameRoundTracker _roundTracker = new GameRoundTracker();
    private const int MISSES_BEFORE_AUTOPLAY = 5;

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
        if (_autoPlayButton != null)
        {
            _autoPlayButton.gameObject.SetActive(false);
            _autoPlayButton.onClick.AddListener(HandleAutoPlay);
        }
        if (_lookTargetButton != null)
        {
            _lookTargetButton.onClick.AddListener(HandleLookTarget);
        }
        if (_angleText != null) _angleText.gameObject.SetActive(false);
        if (_forceText != null) _forceText.gameObject.SetActive(false);

        UpdateStatsUI();

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
        _stretchPlayed = false;
    }

    private void HandleTouchMoved()
    {
        if (!TryComputeDrag(out Vector2 launchDirection, out float normalizedForce, out _))
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

        // Update hint texts if visible
        UpdateHintTexts(launchDirection, normalizedForce);
    }

    private void HandleTouchEnded()
    {
        _isDragging = false;
        if (_aimArrow != null) _aimArrow.Hide();

        if (!TryComputeDrag(out Vector2 launchDirection, out float normalizedForce, out _))
        {
            _rocket.transform.rotation = Quaternion.identity;
            return;
        }

        float launchForce = Mathf.Lerp(_minLaunchForce, _maxLaunchForce, normalizedForce);

        _roundTracker.IncrementShots();
        UpdateStatsUI();

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
    private bool TryComputeDrag(out Vector2 direction, out float normalizedForce, out float rawDistance)
    {
        Vector2 fingerWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 spawnPos = _spawnPoint.position;

        Vector2 dragVector = spawnPos - fingerWorldPos;
        rawDistance = dragVector.magnitude;

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

    /// <summary>Rocket hit target — hide rocket, show win (or reset if auto-play demo).</summary>
    private void HandleTargetHit()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopThrust();
            AudioManager.Instance.PlayHitTarget();
            AudioManager.Instance.PlayWin();
        }

        // Screen shake on hit (bigger)
        if (_cameraController != null)
            _cameraController.Shake(_hitShakeDuration, _hitShakeMagnitude);

        if (_isAutoPlaying)
        {
            Invoke(nameof(ReloadAfterAutoPlay), _reloadDelay);
            return;
        }

        // Update best score
        if (_roundTracker.TryUpdateBest(_roundTracker.RoundShots))
            UpdateStatsUI();

        _rocket.gameObject.SetActive(false);

        if (_winText != null)
            _winText.gameObject.SetActive(true);
        if (_restartButton != null)
            _restartButton.gameObject.SetActive(true);
    }

    /// <summary>Rocket hit ground — count miss (or reset if auto-play demo).</summary>
    private void HandleRocketMiss()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopThrust();
            AudioManager.Instance.PlayHitGround();
        }

        // Screen shake on miss (small)
        if (_cameraController != null)
            _cameraController.Shake(_missShakeDuration, _missShakeMagnitude);

        if (_isAutoPlaying)
        {
            Invoke(nameof(ReloadAfterAutoPlay), _reloadDelay);
            return;
        }

        _missCount++;
        Invoke(nameof(ReloadRocket), _reloadDelay);
    }

    /// <summary>Return camera, reset rocket, show autoplay if enough misses.</summary>
    private void ReloadRocket()
    {
        if (_cameraController != null)
            _cameraController.ReturnToVehicle();

        _rocket.ResetToPosition(_spawnPoint.position);

        // Show hints after N misses
        if (_missCount >= MISSES_BEFORE_AUTOPLAY)
        {
            if (_autoPlayButton != null) _autoPlayButton.gameObject.SetActive(true);
            if (_angleText != null) _angleText.gameObject.SetActive(true);
            if (_forceText != null) _forceText.gameObject.SetActive(true);
        }

        EnableInput();
    }

    /// <summary>Restart button clicked — randomize target, intro pan, then enable input.</summary>
    private void HandleRestart()
    {
        CancelInvoke();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();

        // Hide UI
        if (_winText != null)
            _winText.gameObject.SetActive(false);
        if (_restartButton != null)
            _restartButton.gameObject.SetActive(false);

        // Re-enable rocket, clear debris from previous round, reset counters
        RocketDebris.ClearAll();
        _rocket.gameObject.SetActive(true);
        _rocket.ResetToPosition(_spawnPoint.position);
        _missCount = 0;
        if (_autoPlayButton != null)
            _autoPlayButton.gameObject.SetActive(false);
        if (_angleText != null) _angleText.gameObject.SetActive(false);
        if (_forceText != null) _forceText.gameObject.SetActive(false);

        _roundTracker.NewRound();
        UpdateStatsUI();

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

        // Respawn obstacles with safe trajectory to new target
        if (_obstacleSpawner != null)
            _obstacleSpawner.RespawnObstacles();
    }

    /// <summary>Auto-play: launch along safe trajectory, then reset for player to retry same layout.</summary>
    private void HandleAutoPlay()
    {
        if (!_inputEnabled || _obstacleSpawner == null) return;

        CancelInvoke();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();

        Vector2 dir = _obstacleSpawner.SafeLaunchDirection;
        float force = _obstacleSpawner.SafeLaunchForce;
        if (dir.sqrMagnitude < 0.01f) return;

        // Hide autoplay button during demo
        if (_autoPlayButton != null)
            _autoPlayButton.gameObject.SetActive(false);

        _isAutoPlaying = true;
        RotateRocketToDirection(dir);
        _rocket.Launch(dir, force);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayLaunch();
            AudioManager.Instance.StartThrust();
        }
        DisableInput();
    }

    /// <summary>After auto-play demo, reset rocket for player to try same layout.</summary>
    private void ReloadAfterAutoPlay()
    {
        _isAutoPlaying = false;

        if (_cameraController != null)
            _cameraController.ReturnToVehicle();

        _rocket.gameObject.SetActive(true);
        _rocket.ResetToPosition(_spawnPoint.position);
        _missCount = 0; // Reset miss count
        EnableInput();
    }

    /// <summary>Look Target button — pan camera to target, wait 2s, pan back.</summary>
    private void HandleLookTarget()
    {
        if (!_inputEnabled) return;
        if (_rocket != null && _rocket.IsFlying) return;
        if (_cameraController == null) return;

        DisableInput();
        _cameraController.OnLookTargetComplete -= OnLookTargetDone;
        _cameraController.OnLookTargetComplete += OnLookTargetDone;
        _cameraController.PanToTarget();
    }

    private void OnLookTargetDone()
    {
        _cameraController.OnLookTargetComplete -= OnLookTargetDone;
        EnableInput();
    }

    private void RotateRocketToDirection(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        _rocket.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void UpdateHintTexts(Vector2 direction, float normalizedForce)
    {
        if (_angleText == null || _forceText == null) return;
        if (!_angleText.gameObject.activeSelf) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float force = Mathf.Lerp(_minLaunchForce, _maxLaunchForce, normalizedForce);

        _angleText.text = $"Angle: {angle:F1}°";
        _forceText.text = $"Force: {force:F1}";
    }

    private void UpdateStatsUI()
    {
        if (_statsText == null) return;
        _statsText.text = _roundTracker.GetStatsText();
    }

    private void OnDestroy()
    {
        CancelInvoke();

        if (_rocket != null)
        {
            _rocket.OnRocketLanded -= HandleRocketMiss;
            _rocket.OnTargetHit -= HandleTargetHit;
        }

        if (_cameraController != null)
        {
            _cameraController.OnIntroComplete -= OnIntroDone;
            _cameraController.OnLookTargetComplete -= OnLookTargetDone;
        }

        if (_restartButton != null)
            _restartButton.onClick.RemoveListener(HandleRestart);
        if (_autoPlayButton != null)
            _autoPlayButton.onClick.RemoveListener(HandleAutoPlay);
        if (_lookTargetButton != null)
            _lookTargetButton.onClick.RemoveListener(HandleLookTarget);
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
