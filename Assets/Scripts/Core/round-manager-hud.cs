using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RocketLauncher
{
    /// <summary>
    /// HUD management for RoundManager: win text, buttons, hint labels, stats display.
    /// Singleton so RoundManager can access it without circular SerializeField refs.
    /// </summary>
    public class RoundManagerHUD : MonoBehaviour
    {
        public static RoundManagerHUD Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        [Header("UI")]
        [SerializeField] private TextMeshProUGUI _winText;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _autoPlayButton;
        [SerializeField] private Button _lookTargetButton;
        [SerializeField] private TextMeshProUGUI _angleText;
        [SerializeField] private TextMeshProUGUI _forceText;
        [SerializeField] private TextMeshProUGUI _statsText;

        [Header("Round Manager")]
        [SerializeField] private RoundManager _roundManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (_winText != null) _winText.gameObject.SetActive(false);
            if (_restartButton != null)
            {
                _restartButton.gameObject.SetActive(false);
                _restartButton.onClick.AddListener(OnRestartClicked);
            }
            if (_autoPlayButton != null)
            {
                _autoPlayButton.gameObject.SetActive(false);
                _autoPlayButton.onClick.AddListener(OnAutoPlayClicked);
            }
            if (_lookTargetButton != null)
                _lookTargetButton.onClick.AddListener(OnLookTargetClicked);
            if (_angleText != null) _angleText.gameObject.SetActive(false);
            if (_forceText != null) _forceText.gameObject.SetActive(false);

            if (_roundManager != null)
                UpdateStatsUI(_roundManager.RoundTracker);
        }

        private void OnRestartClicked() => _roundManager?.HandleRestart();
        private void OnAutoPlayClicked() => _roundManager?.HandleAutoPlay();
        private void OnLookTargetClicked() => _roundManager?.HandleLookTarget();

        public void ShowWinUI()
        {
            if (_winText != null) _winText.gameObject.SetActive(true);
            if (_restartButton != null) _restartButton.gameObject.SetActive(true);
        }

        public void HideWinUI()
        {
            if (_winText != null) _winText.gameObject.SetActive(false);
            if (_restartButton != null) _restartButton.gameObject.SetActive(false);
        }

        public void ShowHints()
        {
            if (_autoPlayButton != null) _autoPlayButton.gameObject.SetActive(true);
            if (_angleText != null) _angleText.gameObject.SetActive(true);
            if (_forceText != null) _forceText.gameObject.SetActive(true);
        }

        public void HideHints()
        {
            if (_autoPlayButton != null) _autoPlayButton.gameObject.SetActive(false);
            if (_angleText != null) _angleText.gameObject.SetActive(false);
            if (_forceText != null) _forceText.gameObject.SetActive(false);
        }

        public void HideAutoPlayButton()
        {
            if (_autoPlayButton != null) _autoPlayButton.gameObject.SetActive(false);
        }

        public void UpdateHintTexts(Vector2 direction, float normalizedForce)
        {
            if (_angleText == null || _forceText == null) return;
            if (!_angleText.gameObject.activeSelf) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float force = Mathf.Lerp(GameConstants.MinLaunchForce, GameConstants.MaxLaunchForce, normalizedForce);

            _angleText.text = $"Angle: {angle:F1}\u00b0";
            _forceText.text = $"Force: {force:F1}";
        }

        public void UpdateStatsUI(GameRoundTracker tracker)
        {
            if (_statsText == null) return;
            _statsText.text = tracker.GetStatsText();
        }

        private void OnDestroy()
        {
            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(OnRestartClicked);
            if (_autoPlayButton != null)
                _autoPlayButton.onClick.RemoveListener(OnAutoPlayClicked);
            if (_lookTargetButton != null)
                _lookTargetButton.onClick.RemoveListener(OnLookTargetClicked);
        }
    }
}
