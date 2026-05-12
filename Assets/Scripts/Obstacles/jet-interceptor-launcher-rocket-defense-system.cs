using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Defensive launcher attached to each jet obstacle. Stays passive until the centralized
    /// RocketTrajectoryPredictor flags THIS jet as the predicted first-hit victim — at which
    /// point it fires a homing interceptor missile.
    ///
    /// Trigger is event-driven (not polled): predictor calls OnFlaggedAsVictim(rendezvous, time)
    /// once per rocket launch, on exactly one jet (or none if the arc misses every jet).
    /// </summary>
    public class JetInterceptorLauncher : MonoBehaviour
    {
        /// <summary>
        /// Range in world units within which the interceptor must rendezvous with the rocket.
        /// Public so the predictor and missile can read this geometry.
        /// </summary>
        public const float DetectionRange = 5f;

        // Sprite reference for interceptor — assigned by ObstacleSpawner from rk.png.
        private static Sprite _cachedInterceptorSprite;

        private Rocket _playerRocket;

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

        /// <summary>
        /// Called by RocketTrajectoryPredictor immediately after the rocket is launched, on
        /// the single jet whose DetectionRange the predicted arc enters first. Spawns one
        /// homing interceptor with the rendezvous point — the missile uses real-time pursuit
        /// of the rocket, not a baked path, so it tracks even if the rocket veers from prediction.
        /// </summary>
        public void OnFlaggedAsVictim(Vector2 rendezvous, float timeToRendezvous, bool rocketAscending)
        {
            if (_playerRocket == null) _playerRocket = Object.FindFirstObjectByType<Rocket>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Interceptor] Victim launcher at {transform.position} received OnFlaggedAsVictim. " +
                      $"playerRocket={(_playerRocket != null ? "OK" : "NULL")}, sprite={(_cachedInterceptorSprite != null ? "OK" : "NULL")}, ascending={rocketAscending}");
#endif
            if (_playerRocket == null || _cachedInterceptorSprite == null) return;

            FireInterceptor(rendezvous, timeToRendezvous, rocketAscending);
        }

        private void FireInterceptor(Vector2 rendezvous, float timeToRendezvous, bool rocketAscending)
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
            // Jet sprite (protector.png) has its nose pointing LEFT in the raw PNG, and the jet
            // GameObject has no flip applied — so the jet's "forward" in world space = -transform.right.
            Vector2 jetForward = -(Vector2)transform.right;
            missile.Initialize(_playerRocket.transform, rendezvous, timeToRendezvous, transform.position, DetectionRange, rocketAscending, jetForward);

            trail.StartTrail();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayInterceptorLaunch();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Interceptor] Jet at {transform.position} fires homing missile — rendezvous {rendezvous} in {timeToRendezvous:F2}s");
#endif
        }
    }
}
