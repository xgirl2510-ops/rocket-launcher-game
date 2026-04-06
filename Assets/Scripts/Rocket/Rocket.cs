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
        private const float MinVelocitySqr = 0.01f;

        /// <summary>Fired when the rocket is launched via impulse.</summary>
        public event Action OnRocketLaunched;
        /// <summary>Fired when the rocket hits ground (miss).</summary>
        public event Action OnRocketLanded;
        /// <summary>Fired when the rocket hits the target (win).</summary>
        public event Action OnTargetHit;
        /// <summary>Fired on any impact with position, hit flag, and max height reached.</summary>
        public event Action<Vector2, bool, float> OnImpact;

        private Rigidbody2D _rb;
        private bool _isFlying;
        private float _maxHeight;
        private RocketTrail _trail;
        private SpriteRenderer[] _spriteRenderers;

        /// <summary>Whether the rocket is currently in flight.</summary>
        public bool IsFlying => _isFlying;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _trail = GetComponent<RocketTrail>();
            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// Switch to Dynamic, apply impulse force, fire OnRocketLaunched.
        /// </summary>
        public void Launch(Vector2 direction, float force)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + GameConstants.SpriteAngleOffset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.AddForce(direction * force, ForceMode2D.Impulse);
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
            if (!collision.gameObject.CompareTag(GroundTag)) return;

            _isFlying = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            if (_trail != null) _trail.StopTrail();
            OnImpact?.Invoke(transform.position, false, _maxHeight);
            SetSpritesVisible(false);

            OnRocketLanded?.Invoke();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isFlying) return;
            if (!other.CompareTag(TargetTag)) return;

            _isFlying = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;

            if (_trail != null) _trail.StopTrail();
            OnImpact?.Invoke(transform.position, true, _maxHeight);
            other.gameObject.SetActive(false);

            SetSpritesVisible(false);

            OnTargetHit?.Invoke();
        }
    }
}
