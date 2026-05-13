using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Subscribes to Rocket.OnImpact and spawns visual effects (explosion, debris, craters).
    /// Decouples Rocket physics from visual effect systems.
    /// </summary>
    public class ImpactEffectsHandler : MonoBehaviour
    {
        [SerializeField] private Rocket _rocket;
        [SerializeField] private Transform _ground;
        [SerializeField] private CameraController _cameraController;

        [Header("Screen Shake")]
        [SerializeField] private float _missShakeDuration = 0.35f;
        [SerializeField] private float _missShakeMagnitude = 0.25f;
        [SerializeField] private float _hitShakeDuration = 0.55f;
        [SerializeField] private float _hitShakeMagnitude = 0.45f;

        private void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_rocket == null)
                Debug.LogError("[ImpactEffectsHandler] _rocket not assigned — impact effects disabled.", this);
            if (_cameraController == null)
                _cameraController = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
#endif
        }

        private void OnEnable()
        {
            if (_rocket != null)
                _rocket.OnImpact += HandleImpact;
        }

        private void OnDisable()
        {
            if (_rocket != null)
                _rocket.OnImpact -= HandleImpact;
        }

        private void HandleImpact(Vector2 position, bool isHit, float maxHeight, Vector2 impactVelocity)
        {
            // Ground-anchored effects (stem from ground + lingering fire on soil + dust ring)
            // only when the impact actually happened ON the ground surface — not when the rocket
            // hit an obstacle in the sky (obstacles are tagged "Ground" so the rocket bounces off,
            // but they are physically NOT the ground and shouldn't trigger ground-only visuals).
            // Threshold = a bit above GroundTop, accounting for rocket center offset.
            const float groundContactBand = 1.5f;
            bool impactOnGroundSurface =
                position.y < GameConstants.GroundTop + groundContactBand;
            bool isVerticalImpact = !isHit && impactOnGroundSurface;

            // Crater MUST be spawned before the explosion so stem/lingering-fire/dust-ring
            // (anchored at the actual hole bottom via GroundScorch.GetGroundY) read the correct
            // depth. Otherwise they sit on the flat ground line and look "lưng chừng".
            //
            // Y is clamped to the GROUND SPRITE's actual top edge (not the GameConstants
            // GroundTop constant), so the SpriteMask sits INSIDE the visible sprite bounds
            // regardless of where the sprite was positioned in the scene.
            if (!isHit && position.y < GameConstants.GroundTop + GameConstants.CraterSpawnHeightThreshold)
            {
                float craterY = GameConstants.GroundTop;
                if (_ground != null)
                {
                    var groundSr = _ground.GetComponent<SpriteRenderer>();
                    if (groundSr != null) craterY = groundSr.bounds.max.y;
                }
                Vector2 craterPos = new Vector2(position.x, craterY);
                GroundScorch.Spawn(craterPos, maxHeight, _ground);
            }

            ExplosionEffect.Spawn(position, isHit, isVerticalImpact);
            RocketDebris.Spawn(position, impactVelocity);

            if (isHit)
                RocketDebris.SpawnTargetDebris(position, impactVelocity);

            // Stronger screen shake — scaled by impact speed for extra punch on fast collisions
            if (_cameraController != null)
            {
                float speedFactor = Mathf.Clamp01(impactVelocity.magnitude / 20f); // 0..1 by 20 m/s
                float duration = isHit ? _hitShakeDuration : _missShakeDuration;
                float magnitude = (isHit ? _hitShakeMagnitude : _missShakeMagnitude)
                    * (1f + 0.5f * speedFactor);
                _cameraController.Shake(duration, magnitude);
            }
        }
    }
}
