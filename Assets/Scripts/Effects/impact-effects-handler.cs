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

        private void Start()
        {
            if (_rocket == null)
                Debug.LogError("[ImpactEffectsHandler] _rocket not assigned — impact effects disabled.", this);
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

        private void HandleImpact(Vector2 position, bool isHit, float maxHeight)
        {
            ExplosionEffect.Spawn(position, isHit);
            RocketDebris.Spawn(position);

            if (isHit)
                RocketDebris.SpawnTargetDebris(position);

            if (!isHit && position.y < GameConstants.GroundTop + GameConstants.CraterSpawnHeightThreshold)
                GroundScorch.Spawn(position, maxHeight);
        }
    }
}
