using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Partial: HUD and UI management for LaunchController.
/// Handles win text, buttons, hint labels, stats display, and screen shake parameters.
/// </summary>
public partial class LaunchController
{
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

    /// <summary>Hide all UI elements at start, wire button listeners.</summary>
    private void InitHUD()
    {
        if (_winText != null) _winText.gameObject.SetActive(false);
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
            _lookTargetButton.onClick.AddListener(HandleLookTarget);
        if (_angleText != null) _angleText.gameObject.SetActive(false);
        if (_forceText != null) _forceText.gameObject.SetActive(false);

        UpdateStatsUI();
    }

    /// <summary>Unwire button listeners on destroy.</summary>
    private void CleanupHUD()
    {
        if (_restartButton != null)
            _restartButton.onClick.RemoveListener(HandleRestart);
        if (_autoPlayButton != null)
            _autoPlayButton.onClick.RemoveListener(HandleAutoPlay);
        if (_lookTargetButton != null)
            _lookTargetButton.onClick.RemoveListener(HandleLookTarget);
    }

    private void ShowWinUI()
    {
        if (_winText != null) _winText.gameObject.SetActive(true);
        if (_restartButton != null) _restartButton.gameObject.SetActive(true);
    }

    private void HideWinUI()
    {
        if (_winText != null) _winText.gameObject.SetActive(false);
        if (_restartButton != null) _restartButton.gameObject.SetActive(false);
    }

    private void ShowHints()
    {
        if (_autoPlayButton != null) _autoPlayButton.gameObject.SetActive(true);
        if (_angleText != null) _angleText.gameObject.SetActive(true);
        if (_forceText != null) _forceText.gameObject.SetActive(true);
    }

    private void HideHints()
    {
        if (_autoPlayButton != null) _autoPlayButton.gameObject.SetActive(false);
        if (_angleText != null) _angleText.gameObject.SetActive(false);
        if (_forceText != null) _forceText.gameObject.SetActive(false);
    }

    private void HideAutoPlayButton()
    {
        if (_autoPlayButton != null) _autoPlayButton.gameObject.SetActive(false);
    }

    private void UpdateHintTexts(Vector2 direction, float normalizedForce)
    {
        if (_angleText == null || _forceText == null) return;
        if (!_angleText.gameObject.activeSelf) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float force = Mathf.Lerp(_minLaunchForce, _maxLaunchForce, normalizedForce);

        _angleText.text = $"Angle: {angle:F1}\u00b0";
        _forceText.text = $"Force: {force:F1}";
    }

    private void UpdateStatsUI()
    {
        if (_statsText == null) return;
        _statsText.text = _roundTracker.GetStatsText();
    }
}
