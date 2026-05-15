using System.Collections.Generic;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Dive-puzzle obstacle layout. Each round:
    ///   1. Solver brute-forces a "dive" trajectory (apex above target, descent angle ≥60°) using
    ///      the live rocket physics (drag + thrust + impulse).
    ///   2. Two "cap" jets are pinned above the target leaving a narrow slot — only the dive
    ///      trajectory's vertical drop slips through it.
    ///   3. One "shield" jet sits in front of the target and one "tail" jet behind so flat shots
    ///      from any angle are blocked.
    ///   4. The remaining 8-16 jets fill the playfield without intruding on the dive corridor.
    ///
    /// Auto-play replays the solver's exact (dir, force) using player physics so it always hits.
    ///
    /// Public surface (SafeLaunchDirection / SafeLaunchForce / RespawnObstacles) preserved so
    /// RoundManager.HandleAutoPlay continues to work unchanged.
    /// </summary>
    public partial class ObstacleSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform _targetTransform;
        [Tooltip("Player rocket — flight params (drag/thrust) are read live so the solver matches actual physics.")]
        [SerializeField] private Rocket _rocket;

        [Header("Jet Count (random per round)")]
        [SerializeField, Range(4, 30)] private int _jetCountMin = 12;
        [SerializeField, Range(4, 30)] private int _jetCountMax = 20;

        [Header("Cap Slot (two jets above target)")]
        [Tooltip("Vertical offset of the cap pair above the target (world units).")]
        [SerializeField, Range(1.5f, 6f)] private float _capHeightAboveTarget = 3f;
        [Tooltip("Half-width of the dive slot between the two cap jets, in units of rocket diameter.")]
        [SerializeField, Range(0.5f, 3f)] private float _capSlotHalfRocketWidths = 1.0f;

        [Header("Shield + Tail jets")]
        [Tooltip("Horizontal distance the shield jet sits AHEAD of the target (between launcher and target).")]
        [SerializeField, Range(3f, 10f)] private float _shieldDistanceAhead = 6f;
        [Tooltip("Horizontal distance the tail jet sits BEHIND the target (further from launcher).")]
        [SerializeField, Range(3f, 10f)] private float _tailDistanceBehind = 5f;

        [Header("Jet Visual")]
        [SerializeField] private float _obstacleMinSize = 0.196f;
        [SerializeField] private float _obstacleMaxSize = 0.196f;
        [SerializeField] private Color _obstacleColor = Color.white;
        [SerializeField] private Sprite _obstacleSprite;
        [SerializeField] private Sprite _interceptorSprite;
        // Aspect-ratio of protector.png (1791/361) so collider matches the visual rectangle.
        private const float ObstacleSpriteAspect = 1791f / 361f;
        private const float SpriteWidthAtScale1 = 1791f / 100f;
        private const float SpriteHeightAtScale1 = 361f / 100f;
        // Approximate rocket sprite half-width used to size the cap slot in rocket-widths.
        private const float RocketDiameterEstimate = 0.6f;

        [Header("Spawn Area (for filler jets)")]
        // Min horizontal gap from launcher / target so jets don't crowd them.
        [SerializeField] private float _spawnPaddingX = 8f;
        [SerializeField] private float _spawnMinY = -3f;
        [SerializeField] private float _spawnMaxY = 13f;

        // Tight packing — jets can sit nearly touching (5% buffer between AABBs).
        private const float OverlapSeparationScale = 1.05f;
        // Corridor radius around the dive trajectory; filler jets must stay this far away.
        private const float DiveCorridorRadius = 2.0f;
        // Filler placement budget — tens of jets × this multiplier = total attempts.
        private const int FillerAttemptsPerJet = 80;

        private readonly List<GameObject> _obstacles = new List<GameObject>();
        private DiveSolution _lastDive;

        /// <summary>Get the launch direction for the dive trajectory (auto-play replay).</summary>
        public Vector2 SafeLaunchDirection => _lastDive.Found ? _lastDive.LaunchDir : Vector2.zero;
        /// <summary>Get the launch force for the dive trajectory (auto-play replay).</summary>
        public float SafeLaunchForce => _lastDive.Found ? _lastDive.LaunchForce : 0f;
        /// <summary>Whether the last RespawnObstacles call found a valid dive solution.</summary>
        public bool HasValidDive => _lastDive.Found;

        /// <summary>
        /// Solve a dive for the current target and lay out the puzzle. Returns true on success.
        /// If the solver can't find a dive (target geometrically unreachable), returns false WITHOUT
        /// spawning obstacles — RoundManager should re-roll the target and call again.
        /// </summary>
        public bool RespawnObstacles()
        {
            ClearObstacles();
            _lastDive = default;

            if (_spawnPoint == null || _targetTransform == null) return false;

            Vector2 start = _spawnPoint.position;
            Vector2 target = _targetTransform.position;

            RocketFlightParams flightParams = _rocket != null
                ? _rocket.GetFlightParams()
                : new RocketFlightParams(Physics2D.gravity.magnitude, 0.4f, 12f, 0.6f,
                                         GameConstants.MinLaunchForce, GameConstants.MaxLaunchForce);

            if (!RocketDiveSolver.TrySolve(start, target, flightParams, out _lastDive))
                return false;

            float scale = Random.Range(_obstacleMinSize, _obstacleMaxSize);
            int jetCount = Random.Range(_jetCountMin, _jetCountMax + 1);

            PlacePuzzleJets(start, target, scale, jetCount);
            return true;
        }

        private void ClearObstacles()
        {
            foreach (var obs in _obstacles)
                if (obs != null) Destroy(obs);
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
            if (_rocket == null) Debug.LogWarning("[ObstacleSpawner] _rocket not assigned — solver will use default flight params.", this);
        }
#endif
    }
}
