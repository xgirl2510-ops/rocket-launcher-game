using System.Collections.Generic;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Spawns colored debris pieces that fly outward then fall to the ground.
    /// Pieces fade and self-destruct after landing.
    /// Uses manual movement + gravity (no Rigidbody) to avoid fall-through.
    /// Shares a single cached Sprite across all debris pieces to avoid GPU resource leaks.
    /// </summary>
    public class RocketDebris : MonoBehaviour
    {
        private static readonly Color[] RocketColors = {
            new Color(0.8f, 0f, 0f, 1f),
            new Color(1f, 0.2f, 0f, 1f),
            new Color(0.6f, 0f, 0f, 1f),
            new Color(0.3f, 0.3f, 0.3f, 1f),
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

        private const float Gravity = 12f;
        private Vector2 _velocity;
        private float _angularSpeed;
        private float _groundYOffset;
        private bool _grounded;

        /// <summary>Spawn rocket debris (small red/grey pieces).</summary>
        public static void Spawn(Vector2 position, int count = 16)
        {
            SpawnInternal(position, RocketColors, count, 0.08f, 0.22f, 1.5f, 4f);
        }

        /// <summary>Spawn dirt/rock chunks flying up from crater.</summary>
        public static void SpawnDirtDebris(Vector2 position, float scale)
        {
            int count = Mathf.RoundToInt(Mathf.Lerp(6, 20, Mathf.Clamp01(scale / 5f)));
            float maxSize = Mathf.Lerp(0.15f, 0.4f, Mathf.Clamp01(scale / 5f));
            SpawnInternal(position, DirtColors, count, 0.08f, maxSize, 1.5f, 5f);
        }

        /// <summary>Spawn target debris (bigger red pieces flying wider).</summary>
        public static void SpawnTargetDebris(Vector2 position)
        {
            SpawnInternal(position, TargetColors, 20, 0.15f, 0.5f, 2f, 6f);
        }

        private static void SpawnInternal(Vector2 position, Color[] colors, int count,
            float minSize, float maxSize, float minSpeed, float maxSpeed)
        {
            var sprite = RuntimeSpriteFactory.GetSolidSprite();

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Debris");
                go.transform.position = new Vector3(position.x, position.y, 0f);

                float size = Random.Range(minSize, maxSize);
                go.transform.localScale = new Vector3(size, size, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = colors[Random.Range(0, colors.Length)];
                sr.sortingLayerName = "Projectile";
                sr.sortingOrder = 5;
                sr.sprite = sprite;

                var debris = go.AddComponent<RocketDebris>();

                float angle = Random.Range(15f, 165f) * Mathf.Deg2Rad;
                float speed = Random.Range(minSpeed, maxSpeed);
                debris._velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                debris._angularSpeed = Random.Range(-360f, 360f);
                debris._groundYOffset = Random.Range(-0.15f, 0.1f);

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
            if (_grounded) return;

            _velocity.y -= Gravity * Time.fixedDeltaTime;
            transform.position += (Vector3)(_velocity * Time.fixedDeltaTime);
            transform.Rotate(0f, 0f, _angularSpeed * Time.fixedDeltaTime);

            float groundY = GroundScorch.GetGroundY(transform.position.x) + _groundYOffset;
            if (transform.position.y <= groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, 0f);
                _grounded = true;
            }
        }

        private void OnDestroy()
        {
            _allDebris.Remove(gameObject);
        }
    }
}
