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
        // Per-round random in [_obstacleCountMin, _obstacleCountMax] for replayability.
        [SerializeField, Range(3, 15)] private int _obstacleCountMin = 6;
        [SerializeField, Range(3, 15)] private int _obstacleCountMax = 12;
        // Uniform jet scale matching launcher truck width (~3.5 world units).
        // 1791 / 100 PPU = 17.91 unit at scale 1; 3.5 / 17.91 ≈ 0.196.
        // Min == Max so all jets render identical size (Random.Range collapses to one value).
        [SerializeField] private float _obstacleMinSize = 0.196f;
        [SerializeField] private float _obstacleMaxSize = 0.196f;
        [SerializeField] private Color _obstacleColor = Color.white;
        // Jet fighter sprite (protector.png) — 880x182 PNG. At scale 0.4 with PPU 100,
        // world width ≈ 3.5 units which matches the launcher truck's visual width.
        [SerializeField] private Sprite _obstacleSprite;
        // Defensive interceptor missile sprite (rk.png) — fired by jets at incoming player rocket.
        [SerializeField] private Sprite _interceptorSprite;
        // Aspect-ratio of protector.png (1791/361) so collider matches the visual rectangle.
        private const float ObstacleSpriteAspect = 1791f / 361f;
        // World dimensions of the sprite at scale 1 (pixels / PPU = 100). Used for AABB overlap
        // so we use REAL world units, not aspect ratio. WidthAtScale1 = 17.91, HeightAtScale1 = 3.61.
        private const float SpriteWidthAtScale1 = 1791f / 100f;
        private const float SpriteHeightAtScale1 = 361f / 100f;

        [Header("Safe Zone")]
        [SerializeField] private float _safeRadius = 1.5f;
        [SerializeField] private int _trajectorySteps = 30;

        [Header("Spawn Area")]
        [SerializeField] private float _spawnPaddingX = 3f;
        // Vertical range tuned for jets to cluster around mid-air, not scatter to extremes.
        [SerializeField] private float _spawnMinY = -3f;
        [SerializeField] private float _spawnMaxY = 13f;

        // Higher multiplier because corridor radius (10 units = DetectionRange) eats most of the
        // spawn rectangle — we need lots of attempts to find positions outside the safe corridor.
        private const int MaxSpawnAttemptsMultiplier = 60;
        private const float VelocityEstimateScale = 1.5f;
        private const float MinVelocitySquared = 100f;
        private const float FallbackAngleDeg = 60f;
        private const float TimeOfFlightScale = 1.2f;
        // Tight packing — 1.05 means jets can sit nearly touching (5% buffer between AABBs).
        // Visual cluster reads as a "formation" rather than scattered scouts.
        private const float OverlapSeparationScale = 1.05f;
        private const float MinHorizontalSpeed = 0.1f;

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

            SolveOptimalAngle(diff, g, out float vClamped, out float theta);

            float vx = vClamped * Mathf.Cos(theta);
            float vy = vClamped * Mathf.Sin(theta);

            _lastLaunchDir = new Vector2(vx, vy).normalized;
            _lastLaunchForce = vClamped;

            return SampleTrajectoryPoints(start, vx, vy, g, diff.x);
        }

        /// <summary>Solve launch angle and clamped velocity for a high-arc trajectory.</summary>
        private void SolveOptimalAngle(Vector2 diff, float g, out float vClamped, out float theta)
        {
            float dx = diff.x;
            float dy = diff.y;

            // Compute initial speed estimate, clamp to launch force range
            // BEFORE solving for angle so theta is correct for the actual speed.
            float vSquared = g * (dy + Mathf.Sqrt(dx * dx + dy * dy)) * VelocityEstimateScale;
            float v = Mathf.Sqrt(Mathf.Max(vSquared, MinVelocitySquared));
            vClamped = Mathf.Clamp(v, GameConstants.MinLaunchForce, GameConstants.MaxLaunchForce);

            float vc2 = vClamped * vClamped;
            float vc4 = vc2 * vc2;
            float discriminant = vc4 - g * (g * dx * dx + 2f * dy * vc2);

            if (discriminant < 0f)
            {
                theta = FallbackAngleDeg * Mathf.Deg2Rad;
            }
            else
            {
                // High-arc solution: atan2(v^2 + sqrt(disc), g*dx)
                theta = Mathf.Atan2(vc2 + Mathf.Sqrt(discriminant), g * dx);
            }
        }

        /// <summary>Sample points along a ballistic arc for safe-zone checking.</summary>
        private Vector2[] SampleTrajectoryPoints(Vector2 start, float vx, float vy, float g, float dx)
        {
            float timeOfFlight = (vy + Mathf.Sqrt(vy * vy + 2f * g * Mathf.Max(0f, start.y - GameConstants.GroundTop))) / g;
            float totalTime = Mathf.Min(Mathf.Abs(dx) / Mathf.Max(Mathf.Abs(vx), MinHorizontalSpeed), timeOfFlight * TimeOfFlightScale);

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

            // Roll target count per round so each replay has a slightly different jet count.
            int targetCount = Random.Range(_obstacleCountMin, _obstacleCountMax + 1);
            int spawned = 0;
            int maxAttempts = targetCount * MaxSpawnAttemptsMultiplier;
            int attempts = 0;

            while (spawned < targetCount && attempts < maxAttempts)
            {
                attempts++;
                Vector2 candidate = GenerateRandomPosition(minX, maxX);
                // Roll size FIRST so anti-overlap can use the same size we'll spawn with.
                float candidateScale = Random.Range(_obstacleMinSize, _obstacleMaxSize);
                if (!IsPositionValid(candidate, candidateScale)) continue;
                CreateObstacle(candidate, candidateScale);
                spawned++;
            }
        }

        private Vector2 GenerateRandomPosition(float minX, float maxX)
        {
            float x = Random.Range(minX, maxX);
            float y = Random.Range(_spawnMinY, _spawnMaxY);
            return new Vector2(x, y);
        }

        private bool IsPositionValid(Vector2 candidate, float candidateScale)
        {
            if (IsInSafeZone(candidate)) return false;
            if (IsTooCloseToTarget(candidate)) return false;

            // AABB overlap check using the jet's true rectangular footprint.
            // Sprite world-size at scale s = (aspect * s) wide × s tall.
            // Padding via OverlapSeparationScale keeps a small visible gap between planes.
            Rect candidateRect = MakeJetRect(candidate, candidateScale, OverlapSeparationScale);
            foreach (var obs in _obstacles)
            {
                if (obs == null) continue;
                float existingScale = obs.transform.localScale.x;
                Rect existingRect = MakeJetRect(obs.transform.position, existingScale, OverlapSeparationScale);
                if (candidateRect.Overlaps(existingRect)) return false;
            }
            return true;
        }

        /// <summary>
        /// True if a jet placed at this candidate position would overlap or sit too close to
        /// the target aircraft. Buffer = target half-width + jet half-width + 1 unit gap, so
        /// no jet appears to be "huddled" against the target visually.
        /// </summary>
        private bool IsTooCloseToTarget(Vector2 candidate)
        {
            if (_targetTransform == null) return false;
            // Target world half-width ≈ 21.03 * 0.27 / 2 ≈ 2.84.
            // Jet half-width ≈ 17.91 * obstacleScale / 2 ≈ 1.75.
            // Buffer = 2.84 + 1.75 + 1.0 (gap) ≈ 5.6.
            const float minDistanceToTarget = 5.6f;
            const float minDistSqr = minDistanceToTarget * minDistanceToTarget;
            return ((Vector2)_targetTransform.position - candidate).sqrMagnitude < minDistSqr;
        }

        /// <summary>Build the world-space AABB of a jet at given center+scale, padded by separation factor.</summary>
        private static Rect MakeJetRect(Vector2 center, float scale, float padding)
        {
            float w = SpriteWidthAtScale1 * scale * padding;
            float h = SpriteHeightAtScale1 * scale * padding;
            return new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);
        }

        /// <summary>
        /// A point is "in the safe zone" if a jet placed there would block the safe trajectory.
        /// Corridor must be ≥ jet half-WIDTH (the longest sprite axis) so a jet centered just
        /// outside the corridor still cannot extend its silhouette into the trajectory line.
        /// Auto-play follows this same trajectory exactly, so a clipped jet would intercept it.
        /// </summary>
        private bool IsInSafeZone(Vector2 point)
        {
            if (_safeTrajectory == null) return false;

            // Jet world half-width ≈ SpriteWidthAtScale1 * obstacleScale * 0.5 = 17.91 * 0.196 / 2 ≈ 1.75
            // Add a small buffer (0.5) to account for rocket collider radius and trajectory step error.
            float maxScale = Mathf.Max(_obstacleMinSize, _obstacleMaxSize);
            float corridor = SpriteWidthAtScale1 * maxScale * 0.5f + 0.5f;
            float corridorSqr = corridor * corridor;
            foreach (var tp in _safeTrajectory)
            {
                if ((point - tp).sqrMagnitude < corridorSqr)
                    return true;
            }
            return false;
        }

        private void CreateObstacle(Vector2 position, float size)
        {
            var go = new GameObject("Obstacle");
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.tag = GameConstants.TagGround;
            // Obstacles use Default layer (0). Physics2D matrix must allow Default↔Rocket (layer 8) collision.
            go.layer = GameConstants.DefaultLayer;

            // Use uniform scale so the jet keeps its real silhouette aspect — non-uniform
            // scaling would squash/stretch the plane and look fake.
            go.transform.localScale = new Vector3(size, size, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            // Fall back to solid sprite if no jet sprite assigned (defensive — pre-Setup-Scene state)
            sr.sprite = _obstacleSprite != null ? _obstacleSprite : RuntimeSpriteFactory.GetSolidSprite();
            sr.color = _obstacleColor;
            sr.sortingLayerName = GameConstants.SortingLayerGameplay;

            // PolygonCollider2D auto-builds from the sprite's physics shape (alpha outline) so
            // the rocket only collides where the jet actually exists — no invisible "wing area"
            // around an empty rectangle. Falls back to BoxCollider2D for the solid square sprite.
            if (_obstacleSprite != null)
            {
                go.AddComponent<PolygonCollider2D>();
                // Afterburner exhaust — only meaningful on the jet sprite, not the fallback square.
                go.AddComponent<JetExhaustTrail>();
                // Gentle vertical bobbing so the jet reads as actively flying, not frozen.
                go.AddComponent<JetHoverAnimation>();
                // Defensive interceptor — fires rk.png missile when player rocket is incoming.
                if (_interceptorSprite != null)
                {
                    JetInterceptorLauncher.SetInterceptorSprite(_interceptorSprite);
                    go.AddComponent<JetInterceptorLauncher>();
                }
            }
            else
            {
                go.AddComponent<BoxCollider2D>();
            }

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

        private void OnDestroy()
        {
            ClearObstacles();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!gameObject.scene.isLoaded) return;
            if (_spawnPoint == null) Debug.LogWarning("[ObstacleSpawner] _spawnPoint not assigned.", this);
            if (_targetTransform == null) Debug.LogWarning("[ObstacleSpawner] _targetTransform not assigned.", this);
        }
#endif
    }
}
