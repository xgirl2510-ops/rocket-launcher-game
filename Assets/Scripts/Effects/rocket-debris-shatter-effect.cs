using System.Collections.Generic;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Spawns colored debris pieces that fly outward following impact momentum, fall to ground,
    /// bounce once, then fade out and self-destruct.
    /// Uses 5 randomized shapes (square, circle, triangle, trapezoid, sliver) for visual variety.
    /// Manual movement + gravity + drag (no Rigidbody) for predictable behavior and zero physics overhead.
    /// </summary>
    public class RocketDebris : MonoBehaviour
    {
        // Burnt rocket debris palette — mostly charred black/dark grey with a couple of glowing
        // ember pieces (still-hot metal). Realistic post-explosion remains.
        private static readonly Color[] RocketColors = {
            new Color(0.08f, 0.07f, 0.06f, 1f), // soot black
            new Color(0.15f, 0.12f, 0.1f, 1f),  // charred dark
            new Color(0.25f, 0.2f, 0.18f, 1f),  // burnt brown-grey
            new Color(0.4f, 0.35f, 0.3f, 1f),   // ash grey
            new Color(1f, 0.5f, 0.05f, 1f),     // glowing ember (rare)
            new Color(1f, 0.85f, 0.2f, 1f),     // hot metal (rare)
        };

        private static readonly Color[] DirtColors = {
            new Color(0.55f, 0.41f, 0.08f, 1f),
            new Color(0.45f, 0.33f, 0.07f, 1f),
            new Color(0.4f, 0.28f, 0.1f, 1f),
            new Color(0.35f, 0.25f, 0.05f, 1f),
        };

        private static readonly Color[] TargetColors = {
            new Color(1f, 0f, 0f, 1f),
            new Color(0.85f, 0f, 0f, 1f),
            new Color(1f, 0.15f, 0f, 1f),
            new Color(0.7f, 0f, 0f, 1f),
        };

        private static readonly List<GameObject> _allDebris = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _allDebris.Clear();
        }

        private const float Gravity = 18f;
        private const float LinearDrag = 1.2f;        // air resistance — slows pieces over time
        private const float BounceFactor = 0.35f;     // y-velocity multiplier on first ground hit
        private const float MinBounceSpeed = 1.5f;    // below this, stop bouncing and settle
        private const float FadeDuration = 0.4f;      // shrink + alpha fade before destroy
        private const float SettledLifetime = 1.5f;   // time on ground before fading

        private Vector2 _velocity;
        private float _angularSpeed;
        private float _groundYOffset;
        private bool _grounded;
        private bool _hasBounced;
        private SpriteRenderer _sr;
        private Vector3 _baseScale;
        private float _settledTime;

        /// <summary>Spawn rocket debris (few small charred pieces) using impact momentum.</summary>
        public static void Spawn(Vector2 position, int baseCount = 7)
        {
            Spawn(position, Vector2.zero, baseCount);
        }

        /// <summary>
        /// Spawn rocket debris with explicit impact velocity. Few pieces (6-10) since the rocket
        /// is mostly destroyed by the blast — mostly soot-black/charred bits, occasional glowing ember.
        /// </summary>
        public static void Spawn(Vector2 position, Vector2 impactVelocity, int baseCount = 7)
        {
            int count = Mathf.RoundToInt(baseCount * Random.Range(0.85f, 1.4f));
            SpawnInternal(position, RocketColors, count, 0.06f, 0.18f, 2f, 6f, impactVelocity);
        }

        /// <summary>Spawn dirt/rock chunks flying up from crater.</summary>
        public static void SpawnDirtDebris(Vector2 position, float scale)
        {
            int count = Mathf.RoundToInt(Mathf.Lerp(6, 22, Mathf.Clamp01(scale / 5f)) * Random.Range(0.8f, 1.2f));
            float maxSize = Mathf.Lerp(0.15f, 0.4f, Mathf.Clamp01(scale / 5f));
            SpawnInternal(position, DirtColors, count, 0.08f, maxSize, 1.5f, 5f, Vector2.zero);
        }

        /// <summary>Spawn target debris (red pieces — target is a separate object, not the rocket).</summary>
        public static void SpawnTargetDebris(Vector2 position, Vector2 impactVelocity = default)
        {
            // Target is a different physical object from the rocket — keep red color but reduce count.
            int count = Mathf.RoundToInt(10 * Random.Range(0.85f, 1.3f));
            SpawnInternal(position, TargetColors, count, 0.12f, 0.4f, 2.5f, 7f, impactVelocity);
        }

        private static void SpawnInternal(Vector2 position, Color[] colors, int count,
            float minSize, float maxSize, float minSpeed, float maxSpeed, Vector2 impactVelocity)
        {
            var shapes = RuntimeSpriteFactory.GetDebrisShapes();

            // Bias debris direction along impact normal (opposite of impact velocity)
            // — pieces fly back/away from impact, like real shrapnel
            Vector2 momentum = impactVelocity * 0.4f;
            Vector2 reflectBase = impactVelocity.sqrMagnitude > 0.01f
                ? -impactVelocity.normalized
                : Vector2.up;
            float baseAngle = Mathf.Atan2(reflectBase.y, reflectBase.x) * Mathf.Rad2Deg;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Debris");
                go.transform.position = new Vector3(position.x, position.y, 0f);

                // Random shape — wider variety than just squares
                var shape = shapes[Random.Range(0, shapes.Length)];

                float size = Random.Range(minSize, maxSize);
                // Slight non-uniform scale so pieces don't all look perfectly square
                float aspect = Random.Range(0.7f, 1.3f);
                go.transform.localScale = new Vector3(size * aspect, size / aspect, 1f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = colors[Random.Range(0, colors.Length)];
                sr.sortingLayerName = "Projectile";
                sr.sortingOrder = 5;
                sr.sprite = shape;

                var debris = go.AddComponent<RocketDebris>();
                debris._sr = sr;
                debris._baseScale = go.transform.localScale;

                // Spread ±90° around the reflect base direction so pieces fan out from the impact
                float angleDeg = baseAngle + Random.Range(-90f, 90f);
                float angle = angleDeg * Mathf.Deg2Rad;
                float speed = Random.Range(minSpeed, maxSpeed);
                debris._velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed + momentum;
                debris._angularSpeed = Random.Range(-720f, 720f);
                debris._groundYOffset = Random.Range(-0.15f, 0.05f);

                _allDebris.Add(go);
            }
        }

        /// <summary>Destroy all debris pieces (call on rocket reset).</summary>
        public static void ClearAll()
        {
            for (int i = _allDebris.Count - 1; i >= 0; i--)
            {
                if (_allDebris[i] != null)
                    Destroy(_allDebris[i]);
            }
            _allDebris.Clear();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            if (_grounded)
            {
                _settledTime += dt;
                if (_settledTime >= SettledLifetime)
                    StartFadeOut();
                return;
            }

            // Apply gravity + linear drag (exponential decay)
            _velocity.y -= Gravity * dt;
            _velocity *= Mathf.Max(0f, 1f - LinearDrag * dt);

            transform.position += (Vector3)(_velocity * dt);
            transform.Rotate(0f, 0f, _angularSpeed * dt);

            float groundY = GroundScorch.GetGroundY(transform.position.x) + _groundYOffset;
            if (transform.position.y <= groundY)
            {
                if (!_hasBounced && Mathf.Abs(_velocity.y) > MinBounceSpeed)
                {
                    // Bounce once: invert + dampen y, dampen x and angular too
                    _velocity = new Vector2(_velocity.x * 0.6f, -_velocity.y * BounceFactor);
                    _angularSpeed *= 0.5f;
                    _hasBounced = true;
                    transform.position = new Vector3(transform.position.x, groundY + 0.01f, 0f);
                }
                else
                {
                    transform.position = new Vector3(transform.position.x, groundY, 0f);
                    _grounded = true;
                    _settledTime = 0f;
                    _angularSpeed = 0f;
                }
            }
        }

        private float _fadeT;
        private bool _fading;

        private void StartFadeOut()
        {
            if (_fading) return;
            _fading = true;
            // Schedule destroy at end of fade
            Destroy(gameObject, FadeDuration);
        }

        private void Update()
        {
            if (!_fading || _sr == null) return;
            _fadeT += Time.deltaTime / FadeDuration;
            float f = Mathf.Clamp01(_fadeT);

            // Shrink + alpha fade
            transform.localScale = _baseScale * (1f - f * 0.5f);
            var c = _sr.color;
            c.a = 1f - f;
            _sr.color = c;
        }

        private void OnDestroy()
        {
            _allDebris.Remove(gameObject);
        }
    }
}
