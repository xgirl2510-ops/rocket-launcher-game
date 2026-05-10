using System.Collections.Generic;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Centralized first-hit predictor. Each frame while the player rocket is in flight,
    /// simulates the ballistic arc and finds the FIRST jet (chronologically along the path)
    /// that the rocket would collide with. Only that "victim" jet gets the interceptor
    /// privilege — other jets stay passive.
    ///
    /// Pattern: jets register themselves on enable, unregister on disable. The predictor
    /// is created lazily on first registration.
    /// </summary>
    public class RocketTrajectoryPredictor : MonoBehaviour
    {
        // Trajectory simulation constants. Higher step count = finer time resolution along arc.
        private const int ArcSteps = 120;
        private const float MaxPredictTime = 8f;

        private static RocketTrajectoryPredictor _instance;
        public static RocketTrajectoryPredictor Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[RocketTrajectoryPredictor]");
                    _instance = go.AddComponent<RocketTrajectoryPredictor>();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _instance = null;

        private readonly HashSet<JetInterceptorLauncher> _jets = new HashSet<JetInterceptorLauncher>();
        private Rocket _playerRocket;
        private JetInterceptorLauncher _victim;

        public void Register(JetInterceptorLauncher jet) => _jets.Add(jet);
        public void Unregister(JetInterceptorLauncher jet) => _jets.Remove(jet);

        /// <summary>True if the given jet is the predicted first-hit victim of the active rocket.</summary>
        public bool IsVictim(JetInterceptorLauncher jet) => _victim == jet;

        private void Update()
        {
            if (_playerRocket == null)
                _playerRocket = Object.FindFirstObjectByType<Rocket>();
            if (_playerRocket == null || !_playerRocket.IsFlying)
            {
                _victim = null;
                return;
            }

            _victim = FindFirstHitJet(_playerRocket.transform.position, _playerRocket.LinearVelocity);
        }

        /// <summary>
        /// Walk the predicted ballistic arc step by step. The FIRST jet whose actual collider
        /// the rocket would touch is the victim. We use OverlapPoint against the jet's
        /// PolygonCollider2D so only true physical collisions count — passing within
        /// DetectionRange but missing the silhouette does NOT trigger interception.
        /// </summary>
        private JetInterceptorLauncher FindFirstHitJet(Vector2 rocketPos, Vector2 rocketVel)
        {
            const float g = 9.81f;
            float dt = MaxPredictTime / ArcSteps;

            for (int i = 1; i <= ArcSteps; i++)
            {
                float t = dt * i;
                float x = rocketPos.x + rocketVel.x * t;
                float y = rocketPos.y + rocketVel.y * t - 0.5f * g * t * t;
                if (y < GameConstants.GroundTop) break;

                Vector2 sample = new Vector2(x, y);
                foreach (var jet in _jets)
                {
                    if (jet == null) continue;
                    var col = jet.GetComponent<Collider2D>();
                    if (col == null) continue;
                    if (col.OverlapPoint(sample)) return jet;
                }
            }
            return null;
        }
    }
}
