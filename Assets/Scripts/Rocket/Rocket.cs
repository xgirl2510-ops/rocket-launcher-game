using System;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Rocket physics: launch with impulse, rotate to face velocity, detect ground/target collision.
    /// Starts Kinematic, becomes Dynamic on Launch(). Events notify RoundManager and CameraController.
    /// Integrates with RocketTrail (trail particles) and ExplosionEffect (impact burst).
    /// </summary>
    public class Rocket : MonoBehaviour
    {
        private const string GroundTag = GameConstants.TagGround;
        private const string TargetTag = GameConstants.TagTarget;
        private const string LauncherVehicleTag = GameConstants.TagLauncherVehicle;
        private const float MinVelocitySqr = 0.01f;

        [Header("Realistic Flight")]
        [Tooltip("How long the engine keeps burning after launch (seconds). After this, the rocket coasts.")]
        [SerializeField, Range(0.2f, 1.5f)] private float _thrustDuration = 0.6f;
        [Tooltip("Continuous force multiplier applied during thrust phase (× initial impulse direction).")]
        [SerializeField, Range(0f, 30f)] private float _thrustForce = 12f;
        [Tooltip("Air resistance applied during flight. Higher = rocket loses speed faster.")]
        [SerializeField, Range(0f, 2f)] private float _airDrag = 0.4f;

        /// <summary>Fired when the rocket is launched via impulse.</summary>
        public event Action OnRocketLaunched;
        /// <summary>Fired when the rocket hits ground (miss).</summary>
        public event Action OnRocketLanded;
        /// <summary>Fired when the rocket hits the target (win).</summary>
        public event Action OnTargetHit;
        /// <summary>Fired when the rocket hits its OWN launcher vehicle (friendly fire = game over).</summary>
        public event Action<Vector2> OnLauncherVehicleHit;
        /// <summary>Fired on any impact with position, hit flag, max height reached, and impact velocity.</summary>
        public event Action<Vector2, bool, float, Vector2> OnImpact;

        private Rigidbody2D _rb;
        private bool _isFlying;
        private float _maxHeight;
        private RocketTrail _trail;
        private SpriteRenderer[] _spriteRenderers;

        // Realistic-flight state
        private Vector2 _thrustDirection;
        private float _thrustTimeRemaining;
        private float _originalDrag;

        /// <summary>Whether the rocket is currently in flight.</summary>
        public bool IsFlying => _isFlying;

        /// <summary>Current linear velocity (read-only) — used by interceptor trajectory prediction.</summary>
        public Vector2 LinearVelocity => _rb != null ? _rb.linearVelocity : Vector2.zero;

        /// <summary>
        /// Snapshot of the player rocket's flight physics used by RocketDiveSolver to simulate
        /// candidate trajectories with the EXACT same numbers the live rocket flies with.
        /// </summary>
        public RocketFlightParams GetFlightParams()
        {
            return new RocketFlightParams(
                gravity: Physics2D.gravity.magnitude,
                drag: _airDrag,
                thrustForce: _thrustForce,
                thrustDuration: _thrustDuration,
                minForce: GameConstants.MinLaunchForce,
                maxForce: GameConstants.MaxLaunchForce);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _trail = GetComponent<RocketTrail>();
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            // Remember rigidbody's authored drag so we can restore it after flight
            _originalDrag = _rb.linearDamping;
        }

        /// <summary>
        /// Switch to Dynamic, apply impulse force, fire OnRocketLaunched.
        /// Standard player launch — drag + thrust burn for realistic flight feel.
        /// </summary>
        public void Launch(Vector2 direction, float force)
        {
            LaunchInternal(direction, force, applyThrustAndDrag: true);
        }

        /// <summary>
        /// Pure ballistic launch (no drag, no thrust) — used by auto-play so the rocket
        /// follows the gravity-only trajectory ObstacleSpawner solves analytically.
        /// </summary>
        public void LaunchBallistic(Vector2 direction, float force)
        {
            LaunchInternal(direction, force, applyThrustAndDrag: false);
        }

        private void LaunchInternal(Vector2 direction, float force, bool applyThrustAndDrag)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + GameConstants.SpriteAngleOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.linearDamping = applyThrustAndDrag ? _airDrag : 0f;
            _rb.AddForce(direction * force, ForceMode2D.Impulse);

            // Thrust only when player launches; auto-play uses pure parabola so its trajectory
            // matches ObstacleSpawner's ballistic solver exactly and reliably hits the target.
            _thrustDirection = direction.normalized;
            _thrustTimeRemaining = applyThrustAndDrag ? _thrustDuration : 0f;

            _isFlying = true;
            _maxHeight = transform.position.y;

            if (_trail != null) _trail.StartTrail();

            OnRocketLaunched?.Invoke();
        }

        /// <summary>
        /// Reset rocket to spawn position, zero velocity, set Kinematic.
        /// </summary>
        public void ResetToPosition(Vector2 position)
        {
            _isFlying = false;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.linearDamping = _originalDrag;            // restore authored drag (kinematic ignores it anyway)
            _thrustTimeRemaining = 0f;                    // cancel any in-progress thrust burn

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            transform.rotation = Quaternion.identity;

            if (_trail != null) _trail.ClearTrail();
            SetSpritesVisible(true);
        }

        private void SetSpritesVisible(bool visible)
        {
            foreach (var sr in _spriteRenderers)
                sr.enabled = visible;
        }

        private void FixedUpdate()
        {
            if (!_isFlying) return;
            if (transform.position.y > _maxHeight)
                _maxHeight = transform.position.y;

            // Continuous engine thrust during the burn window — rocket accelerates instead of
            // immediately starting a parabolic free-fall arc like a thrown stone would.
            if (_thrustTimeRemaining > 0f)
            {
                _rb.AddForce(_thrustDirection * _thrustForce, ForceMode2D.Force);
                _thrustTimeRemaining -= Time.fixedDeltaTime;
            }

            RotateToVelocity();
        }

        private void RotateToVelocity()
        {
            Vector2 vel = _rb.linearVelocity;
            if (vel.sqrMagnitude < MinVelocitySqr) return;

            float angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg + GameConstants.SpriteAngleOffset;
            _rb.MoveRotation(angle);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_isFlying) return;

            // Jet hit: rocket physically rams a defender jet BEFORE the interceptor reached it.
            // Both rocket and jet are destroyed mid-air. Counts as a miss so the round restarts.
            var hitJet = collision.gameObject.GetComponent<JetInterceptorLauncher>();
            if (hitJet != null)
            {
                Vector2 jetImpactVelocity = _rb.linearVelocity;
                Vector2 jetImpactPos = collision.GetContact(0).point;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Rocket] ACTUAL JET COLLISION at jet {hitJet.transform.position}, impact point {jetImpactPos}, rocket pos {transform.position}, velocity {jetImpactVelocity}");
#endif

                _isFlying = false;
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                if (_trail != null) _trail.StopTrail();

                // Mid-air explosion at the contact point (vibrant burst, no mushroom stem since
                // it's not a vertical ground impact).
                ExplosionEffect.Spawn(jetImpactPos, isHit: true, isVerticalImpact: false);

                Destroy(hitJet.gameObject);
                SetSpritesVisible(false);

                OnImpact?.Invoke(transform.position, false, _maxHeight, jetImpactVelocity);
                OnRocketLanded?.Invoke();
                return;
            }

            if (!collision.gameObject.CompareTag(GroundTag)) return;

            // Capture velocity BEFORE zeroing — debris/explosion need impact momentum
            Vector2 impactVelocity = _rb.linearVelocity;

            _isFlying = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            if (_trail != null) _trail.StopTrail();
            OnImpact?.Invoke(transform.position, false, _maxHeight, impactVelocity);
            SetSpritesVisible(false);

            OnRocketLanded?.Invoke();
        }

        /// <summary>
        /// External-trigger landing (e.g. interceptor missile detonation). Treated identically
        /// to a ground miss: stop physics, hide sprite, fire OnRocketLanded so RoundManager
        /// counts a miss and runs the reload flow.
        /// </summary>
        public void ForceLand()
        {
            if (!_isFlying) return;
            _isFlying = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            if (_trail != null) _trail.StopTrail();
            SetSpritesVisible(false);

            OnRocketLanded?.Invoke();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isFlying) return;

            // Safe-compare: CompareTag throws "Tag is not defined" if the tag string isn't
            // registered in TagManager. Wrap in helper that returns false on undefined,
            // so a stale tag database doesn't blow up the trigger pipeline.
            if (SafeCompareTag(other, TargetTag))
            {
                HandleTargetTrigger(other);
                return;
            }
            if (SafeCompareTag(other, LauncherVehicleTag))
            {
                // Ignore the vehicle collider while the rocket is moving UPWARD — at launch
                // the rocket spawns INSIDE the vehicle's trigger area (sit-on-top spawn point),
                // so without this guard every shot would game-over the player on liftoff.
                // Friendly fire only counts when the rocket is falling back down.
                if (_rb.linearVelocity.y >= 0f) return;

                HandleLauncherVehicleTrigger(other);
                return;
            }
        }

        /// <summary>CompareTag wrapper that swallows Unity's "Tag not defined" exception.</summary>
        private static bool SafeCompareTag(Collider2D col, string tag)
        {
            try { return col.CompareTag(tag); }
            catch (UnityException) { return false; }
        }

        /// <summary>Rocket hit the win target — fire WIN flow and hide target.</summary>
        private void HandleTargetTrigger(Collider2D other)
        {
            Vector2 impactVelocity = _rb.linearVelocity;

            _isFlying = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            // Use the TARGET's center as impact position (not rocket center).
            // Rocket flies into the target collider so its transform.position is offset
            // toward the entry side — visual effects must center on the target itself.
            Vector2 impactPosition = other.bounds.center;

            if (_trail != null) _trail.StopTrail();
            OnImpact?.Invoke(impactPosition, true, _maxHeight, impactVelocity);
            other.gameObject.SetActive(false);

            SetSpritesVisible(false);

            OnTargetHit?.Invoke();
        }

        /// <summary>Rocket fell onto its OWN launcher vehicle — fire GAME OVER flow.</summary>
        private void HandleLauncherVehicleTrigger(Collider2D other)
        {
            Vector2 impactVelocity = _rb.linearVelocity;

            _isFlying = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            Vector2 impactPosition = other.bounds.center;

            if (_trail != null) _trail.StopTrail();
            // isHit=true so explosion uses the same vibrant fireball as a target kill —
            // the vehicle blowing up should feel as dramatic as winning, just with a
            // different narrative outcome.
            OnImpact?.Invoke(impactPosition, true, _maxHeight, impactVelocity);
            other.gameObject.SetActive(false);

            SetSpritesVisible(false);

            OnLauncherVehicleHit?.Invoke(impactPosition);
        }
    }
}
