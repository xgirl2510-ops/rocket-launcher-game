using System.Collections;
using UnityEngine;

/// <summary>
/// Slingshot input: touch/click on vehicle → drag to aim → release to launch rocket.
/// Direction = opposite of drag. Force = mapped from drag distance.
/// Auto-reloads rocket after miss. Shows win UI + restart on hit.
/// Randomizes target position each round.
/// UI management split into partial class: launch-controller-hud-management.cs
/// </summary>
public partial class LaunchController : MonoBehaviour
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

    private Camera _camera;
    private bool _isDragging;
    private bool _inputEnabled = true;
    private bool _stretchPlayed;
    private int _missCount;
    private bool _isAutoPlaying;
    private readonly GameRoundTracker _roundTracker = new GameRoundTracker();
    private const int MissesBeforeHints = 5;

    private void Awake()
    {
        _camera = Camera.main;
        RandomizeTarget();
    }

    private void Start()
    {
        if (_rocket != null)
        {
            _rocket.OnRocketLanded += HandleRocketMiss;
            _rocket.OnTargetHit += HandleTargetHit;
        }

        InitHUD();
        DisableInput();

        if (_cameraController != null)
            _cameraController.OnIntroComplete += OnIntroDone;
        else
            EnableInput();
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

        if (_cameraController != null)
            _cameraController.Shake(_hitShakeDuration, _hitShakeMagnitude);

        if (_isAutoPlaying)
        {
            StartCoroutine(DelayedAction(_reloadDelay, ReloadAfterAutoPlay));
            return;
        }

        if (_roundTracker.TryUpdateBest(_roundTracker.RoundShots))
            UpdateStatsUI();

        _rocket.gameObject.SetActive(false);
        ShowWinUI();
    }

    /// <summary>Rocket hit ground — count miss (or reset if auto-play demo).</summary>
    private void HandleRocketMiss()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopThrust();
            AudioManager.Instance.PlayHitGround();
        }

        if (_cameraController != null)
            _cameraController.Shake(_missShakeDuration, _missShakeMagnitude);

        if (_isAutoPlaying)
        {
            StartCoroutine(DelayedAction(_reloadDelay, ReloadAfterAutoPlay));
            return;
        }

        _missCount++;
        StartCoroutine(DelayedAction(_reloadDelay, ReloadRocket));
    }

    /// <summary>Return camera, reset rocket, show hints if enough misses.</summary>
    private void ReloadRocket()
    {
        if (_cameraController != null)
            _cameraController.ReturnToVehicle();

        _rocket.ResetToPosition(_spawnPoint.position);

        if (_missCount >= MissesBeforeHints)
            ShowHints();

        EnableInput();
    }

    /// <summary>Restart button clicked — randomize target, intro pan, then enable input.</summary>
    private void HandleRestart()
    {
        StopAllCoroutines();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();

        HideWinUI();
        HideHints();

        RocketDebris.ClearAll();
        GroundScorch.ClearAll();
        _rocket.gameObject.SetActive(true);
        _rocket.ResetToPosition(_spawnPoint.position);
        if (_targetTransform != null) _targetTransform.gameObject.SetActive(true);
        _missCount = 0;

        _roundTracker.NewRound();
        UpdateStatsUI();

        RandomizeTarget();

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

    private void OnIntroDone()
    {
        _cameraController.OnIntroComplete -= OnIntroDone;
        EnableInput();
    }

    private void RandomizeTarget()
    {
        if (_targetTransform == null) return;

        float x = Random.Range(_targetMinX, _targetMaxX);
        float y = Random.Range(_targetMinY, _targetMaxY);
        _targetTransform.position = new Vector3(x, y, _targetTransform.position.z);

        if (_obstacleSpawner != null)
            _obstacleSpawner.RespawnObstacles();
    }

    /// <summary>Auto-play: launch along safe trajectory, then reset for player to retry.</summary>
    private void HandleAutoPlay()
    {
        if (!_inputEnabled || _obstacleSpawner == null) return;

        StopAllCoroutines();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayClick();

        Vector2 dir = _obstacleSpawner.SafeLaunchDirection;
        float force = _obstacleSpawner.SafeLaunchForce;
        if (dir.sqrMagnitude < 0.01f) return;

        HideAutoPlayButton();

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

    private void ReloadAfterAutoPlay()
    {
        _isAutoPlaying = false;

        if (_cameraController != null)
            _cameraController.ReturnToVehicle();

        _rocket.gameObject.SetActive(true);
        _rocket.ResetToPosition(_spawnPoint.position);
        if (_targetTransform != null) _targetTransform.gameObject.SetActive(true);
        _missCount = 0;
        EnableInput();
    }

    /// <summary>Look Target button — pan camera to target, wait, pan back.</summary>
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

    private void OnDestroy()
    {
        StopAllCoroutines();

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

        CleanupHUD();
    }

    /// <summary>Generic delayed action coroutine — replaces Invoke(nameof(...), delay).</summary>
    private IEnumerator DelayedAction(float delay, System.Action action)
    {
        yield return new WaitForSeconds(delay);
        action();
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
