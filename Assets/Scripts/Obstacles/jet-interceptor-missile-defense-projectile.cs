using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Defensive interceptor missile fired by a jet obstacle when the player rocket gets close
    /// AND is on a collision trajectory. The interceptor flies toward the rocket's predicted
    /// position; on contact both detonate mid-air via the existing ExplosionEffect, and the
    /// rocket counts as a miss (Rocket.OnRocketLanded).
    ///
    /// Sprite: Assets/Sprites/Generated/rk.png (1591×410, nose pointing LEFT in raw PNG).
    /// Spawned + driven by JetInterceptorLauncher attached to each jet.
    /// </summary>
    public class InterceptorMissile : MonoBehaviour
    {
        // World width when scale = 1: 1591 / 100 PPU = 15.91. Target ≈ 0.6 unit wide → scale 0.038.
        public const float DefaultScale = 0.04f;
        // Speed in world units / sec. Faster than typical player rocket so intercept feels reactive.
        private const float Speed = 18f;
        // Hard cap so interceptor doesn't chase forever if rocket veers away.
        private const float MaxLifetime = 2.5f;

        private Transform _target;            // player rocket transform
        private float _spawnTime;
        private bool _hasHit;

        public void Initialize(Transform target)
        {
            _target = target;
            _spawnTime = Time.time;
        }

        private void Update()
        {
            if (_hasHit) return;

            // Self-destruct if target gone or lifetime exceeded
            if (_target == null || !_target.gameObject.activeInHierarchy
                || Time.time - _spawnTime > MaxLifetime)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 toTarget = _target.position - transform.position;
            float distance = toTarget.magnitude;

            // Close-enough threshold: detonate before sprite-overlap so explosion centers between
            // the two missiles. Critical: also force-land the rocket here, because relying solely
            // on OnTriggerEnter2D is unreliable at high speeds (18 m/s + Update-based motion can
            // tunnel past the rocket between physics steps).
            if (distance < 0.5f)
            {
                Vector2 midpoint = (transform.position + _target.position) * 0.5f;
                var rocket = _target.GetComponent<Rocket>();
                if (rocket != null) rocket.ForceLand();
                Detonate(midpoint);
                return;
            }

            Vector3 dir = toTarget / distance;
            transform.position += dir * Speed * Time.deltaTime;

            // Rotate to face direction of travel. Sprite's nose points LEFT so add 180°.
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 180f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasHit) return;
            // Interceptor only triggers on the player rocket (Player tag). Jets, ground, target
            // ignored — this missile exists for one job.
            if (!other.CompareTag("Player")) return;

            Vector2 impact = (transform.position + other.transform.position) * 0.5f;
            Detonate(impact);

            // Fire the rocket's existing landed event so RoundManager counts a miss + reload flow.
            var rocket = other.GetComponent<Rocket>();
            if (rocket != null)
                rocket.ForceLand();
        }

        private void Detonate(Vector2 position)
        {
            _hasHit = true;
            // Reuse the project's explosion effect for visual consistency. isHit=true → vibrant burst,
            // isVerticalImpact=false → mid-air round explosion (no mushroom stem).
            ExplosionEffect.Spawn(position, isHit: true, isVerticalImpact: false);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayHitTarget();

            Destroy(gameObject);
        }
    }
}
