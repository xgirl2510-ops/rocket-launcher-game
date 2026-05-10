using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Defensive launcher attached to each jet obstacle. Fires interceptor missile rk.png
    /// ONLY if THIS jet is the predicted first-hit victim of the player rocket AND the
    /// rocket is within DetectionRange. Other jets do nothing — the centralized
    /// RocketTrajectoryPredictor singleton ensures only one jet (the victim) acts.
    ///
    /// Trigger criteria (must satisfy all):
    ///   1. Player rocket flying
    ///   2. RocketTrajectoryPredictor flagged THIS jet as the first-hit victim
    ///   3. Distance jet→rocket ≤ DetectionRange
    ///   4. Cooldown elapsed since last fire
    /// </summary>
    public class JetInterceptorLauncher : MonoBehaviour
    {
        /// <summary>
        /// Range in world units within which the victim jet fires its interceptor at
        /// the incoming rocket. Public so external systems can read this geometry.
        /// </summary>
        public const float DetectionRange = 5f;

        // Cooldown so a victim jet doesn't spam interceptors during a single approach.
        private const float CooldownSeconds = 0.6f;

        // Sprite reference for interceptor — assigned by ObstacleSpawner from rk.png.
        private static Sprite _cachedInterceptorSprite;

        private Rocket _playerRocket;
        private float _nextFireTime;
        private bool _wasFlying;

        public static void SetInterceptorSprite(Sprite sprite) => _cachedInterceptorSprite = sprite;

        private void OnEnable()
        {
            RocketTrajectoryPredictor.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (RocketTrajectoryPredictor.Instance != null)
                RocketTrajectoryPredictor.Instance.Unregister(this);
        }

        private void Start()
        {
            _playerRocket = Object.FindFirstObjectByType<Rocket>();
        }

        private void Update()
        {
            if (_playerRocket == null || _cachedInterceptorSprite == null) return;

            // Reset cooldown on rocket relaunch so the victim jet can intercept the new shot.
            bool flyingNow = _playerRocket.IsFlying;
            if (flyingNow && !_wasFlying) _nextFireTime = 0f;
            _wasFlying = flyingNow;

            if (!flyingNow) return;
            if (Time.time < _nextFireTime) return;

            // Centralized predictor decides which single jet is the victim. If we're not
            // the victim, do nothing — even if the rocket is in our DetectionRange.
            if (!RocketTrajectoryPredictor.Instance.IsVictim(this)) return;

            float dist = Vector2.Distance(_playerRocket.transform.position, transform.position);
            if (dist > DetectionRange) return;

            FireInterceptor();
            _nextFireTime = Time.time + CooldownSeconds;
        }

        private void FireInterceptor()
        {
            var go = new GameObject("Interceptor");
            go.transform.position = transform.position;
            go.transform.localScale = Vector3.one * InterceptorMissile.DefaultScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _cachedInterceptorSprite;
            sr.sortingLayerName = GameConstants.SortingLayerGameplay;
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.4f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var trail = go.AddComponent<RocketTrail>();

            var missile = go.AddComponent<InterceptorMissile>();
            missile.Initialize(_playerRocket.transform);

            trail.StartTrail();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayInterceptorLaunch();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Interceptor] Victim jet at {transform.position} fires at rocket {_playerRocket.transform.position}");
#endif
        }
    }
}
