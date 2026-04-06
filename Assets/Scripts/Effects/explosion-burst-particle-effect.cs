using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// One-shot particle burst at rocket impact position.
    /// Gold/yellow for target hit, grey/white for miss.
    /// Auto-destroys after particles finish playing.
    /// Instantiate via ExplosionEffect.Spawn() static helper.
    /// Uses Sprites/Default material (always included in builds) — same pattern as RocketTrail.
    /// </summary>
    public class ExplosionEffect : MonoBehaviour
    {
        private const int BurstCount = 30;
        private const float ParticleLifetime = 0.6f;
        private const float StartSpeed = 8f;
        private const float StartSize = 0.3f;

        private ParticleSystem _ps;

        /// <summary>
        /// Spawn an explosion at the given position.
        /// isHit=true -> gold/yellow burst; isHit=false -> grey/white burst.
        /// </summary>
        public static void Spawn(Vector2 position, bool isHit)
        {
            var go = new GameObject("Explosion");
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var fx = go.AddComponent<ExplosionEffect>();
            fx.InitAndPlay(isHit);
        }

        private void InitAndPlay(bool isHit)
        {
            _ps = gameObject.AddComponent<ParticleSystem>();

            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            Color burstColor = isHit
                ? new Color(1f, 0.84f, 0f, 1f)
                : new Color(0.7f, 0.7f, 0.7f, 1f);

            Color fadeColor = isHit
                ? new Color(1f, 1f, 0f, 0f)
                : new Color(1f, 1f, 1f, 0f);

            ConfigureParticleSystem(burstColor, fadeColor);

            _ps.Play();

            Destroy(gameObject, ParticleLifetime + 0.2f);
        }

        private void ConfigureParticleSystem(Color burstColor, Color fadeColor)
        {
            var main = _ps.main;
            main.startLifetime = ParticleLifetime;
            main.startSpeed = StartSpeed;
            main.startSize = StartSize;
            main.startColor = burstColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 50;
            main.playOnAwake = false;
            main.loop = false;

            var emission = _ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, BurstCount)
            });

            var shape = _ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var sizeOverLifetime = _ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var colorOverLifetime = _ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(burstColor, 0f),
                    new GradientColorKey(fadeColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var renderer = _ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = RuntimeSpriteFactory.GetParticleMaterial();
            renderer.sortingLayerName = "Projectile";
            renderer.sortingOrder = 10;
        }
    }
}
