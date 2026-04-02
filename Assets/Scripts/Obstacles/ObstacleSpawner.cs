using System.Collections.Generic;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Spawns random obstacles between vehicle and target, avoiding a guaranteed parabolic trajectory.
    /// Calculates a valid arc from spawn point to target, samples points along it as "safe zone",
    /// then places obstacles only outside that zone.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform _targetTransform;

        [Header("Obstacle Settings")]
        [SerializeField, Range(3, 15)] private int _obstacleCount = 6;
        [SerializeField] private float _obstacleMinSize = 0.8f;
        [SerializeField] private float _obstacleMaxSize = 2.0f;
        [SerializeField] private Color _obstacleColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        [Header("Safe Zone")]
        [SerializeField] private float _safeRadius = 1.5f;
        [SerializeField] private int _trajectorySteps = 30;

        [Header("Spawn Area")]
        [SerializeField] private float _spawnPaddingX = 3f;
        [SerializeField] private float _spawnMinY = -4f;
        [SerializeField] private float _spawnMaxY = 12f;

        private static Sprite _cachedSquareSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_cachedSquareSprite != null)
            {
                Object.Destroy(_cachedSquareSprite.texture);
                Object.Destroy(_cachedSquareSprite);
            }
            _cachedSquareSprite = null;
        }

        private readonly List<GameObject> _obstacles = new List<GameObject>();
        private Vector2[] _safeTrajectory;
        private Vector2 _lastLaunchDir;
        private float _lastLaunchForce;

        /// <summary>Get the launch direction for the safe trajectory.</summary>
        public Vector2 SafeLaunchDirection => _lastLaunchDir;
        /// <summary>Get the launch force for the safe trajectory.</summary>
        public float SafeLaunchForce => _lastLaunchForce;

        /// <summary>
        /// Clear old obstacles, calculate safe trajectory, spawn new obstacles.
        /// Call this after target position is set.
        /// </summary>
        public void RespawnObstacles()
        {
            ClearObstacles();

            if (_spawnPoint == null || _targetTransform == null) return;

            Vector2 start = _spawnPoint.position;
            Vector2 target = _targetTransform.position;

            _safeTrajectory = CalculateTrajectory(start, target);
            SpawnObstaclesAvoidingTrajectory(start, target);
        }

        /// <summary>
        /// Calculate a parabolic trajectory from start to target using a high arc.
        /// Uses the "high angle" solution to ensure the arc goes over obstacles.
        /// </summary>
        private Vector2[] CalculateTrajectory(Vector2 start, Vector2 target)
        {
            float g = Physics2D.gravity.magnitude;
            Vector2 diff = target - start;
            float dx = diff.x;
            float dy = diff.y;

            float vSquared = g * (dy + Mathf.Sqrt(dx * dx + dy * dy)) * 1.5f;
            float v = Mathf.Sqrt(Mathf.Max(vSquared, 100f));

            float v4 = v * v * v * v;
            float discriminant = v4 - g * (g * dx * dx + 2f * dy * v * v);

            float theta;
            if (discriminant < 0f)
            {
                theta = 60f * Mathf.Deg2Rad;
            }
            else
            {
                theta = Mathf.Atan2(v * v + Mathf.Sqrt(discriminant), g * dx);
            }

            float vx = v * Mathf.Cos(theta);
            float vy = v * Mathf.Sin(theta);

            _lastLaunchDir = new Vector2(vx, vy).normalized;
            _lastLaunchForce = Mathf.Clamp(v, GameConstants.MinLaunchForce, GameConstants.MaxLaunchForce);

            float totalTime = Mathf.Abs(dx) / Mathf.Max(vx, 0.1f);

            Vector2[] points = new Vector2[_trajectorySteps + 1];
            for (int i = 0; i <= _trajectorySteps; i++)
            {
                float t = (i / (float)_trajectorySteps) * totalTime;
                float x = start.x + vx * t;
                float y = start.y + vy * t - 0.5f * g * t * t;
                points[i] = new Vector2(x, y);
            }

            return points;
        }

        private void SpawnObstaclesAvoidingTrajectory(Vector2 start, Vector2 target)
        {
            float minX = start.x + _spawnPaddingX;
            float maxX = target.x - _spawnPaddingX;
            if (minX >= maxX) return;

            int spawned = 0;
            int maxAttempts = _obstacleCount * 20;
            int attempts = 0;

            while (spawned < _obstacleCount && attempts < maxAttempts)
            {
                attempts++;

                float x = Random.Range(minX, maxX);
                float y = Random.Range(_spawnMinY, _spawnMaxY);
                Vector2 candidate = new Vector2(x, y);

                if (IsInSafeZone(candidate)) continue;

                bool overlaps = false;
                float minDistSqr = _obstacleMaxSize * 1.2f;
                minDistSqr *= minDistSqr;
                foreach (var obs in _obstacles)
                {
                    if (((Vector2)obs.transform.position - candidate).sqrMagnitude < minDistSqr)
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps) continue;

                CreateObstacle(candidate);
                spawned++;
            }
        }

        private bool IsInSafeZone(Vector2 point)
        {
            if (_safeTrajectory == null) return false;

            float safeRadiusSqr = _safeRadius * _safeRadius;
            foreach (var tp in _safeTrajectory)
            {
                if ((point - tp).sqrMagnitude < safeRadiusSqr)
                    return true;
            }
            return false;
        }

        private void CreateObstacle(Vector2 position)
        {
            var go = new GameObject("Obstacle");
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.tag = GameConstants.TagGround;

            float size = Random.Range(_obstacleMinSize, _obstacleMaxSize);
            go.transform.localScale = new Vector3(size, size, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateSquareSprite();
            sr.color = _obstacleColor;
            sr.sortingLayerName = "Gameplay";

            go.AddComponent<BoxCollider2D>();

            _obstacles.Add(go);
        }

        private void ClearObstacles()
        {
            foreach (var obs in _obstacles)
            {
                if (obs != null) Destroy(obs);
            }
            _obstacles.Clear();
        }

        /// <summary>Get or create a simple white square sprite for obstacles (runtime-generated).</summary>
        private static Sprite GetOrCreateSquareSprite()
        {
            if (_cachedSquareSprite) return _cachedSquareSprite;

            Texture2D tex = new Texture2D(4, 4);
            Color[] pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            _cachedSquareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _cachedSquareSprite;
        }
    }
}
