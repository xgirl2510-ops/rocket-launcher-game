using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Two-system rocket trail: a fast hot Flame (red/orange, additive, short life) layered
    /// with a slow Smoke plume (grey, alpha-blended, long life, drifts up). Call StartTrail()
    /// on launch and StopTrail() on land/hit. Built programmatically on Awake.
    /// </summary>
    public class RocketTrail : MonoBehaviour
    {
        [Header("Flame")]
        [SerializeField] private float _flameEmissionRate = 80f;
        [SerializeField] private float _flameLifetime = 0.35f;
        [SerializeField] private float _flameStartSize = 0.22f;

        [Header("Smoke")]
        [SerializeField] private float _smokeEmissionRate = 25f;
        [SerializeField] private float _smokeLifetime = 1.4f;
        [SerializeField] private float _smokeStartSize = 0.18f;

        private ParticleSystem _flame;
        private ParticleSystem _smoke;

        private void Awake()
        {
            // Look for existing children (in case scene has them wired); otherwise build at runtime
            var existing = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in existing)
            {
                if (ps.name == "FlameParticles") _flame = ps;
                else if (ps.name == "SmokeParticles") _smoke = ps;
            }

            if (_flame == null) _flame = CreateFlameSystem();
            if (_smoke == null) _smoke = CreateSmokeSystem();

            StopTrail();
        }

        /// <summary>Begin emitting both flame and smoke (call on rocket launch).</summary>
        public void StartTrail()
        {
            PlayClean(_flame);
            PlayClean(_smoke);
        }

        /// <summary>Stop emitting (existing particles fade out naturally).</summary>
        public void StopTrail()
        {
            StopEmit(_flame, ParticleSystemStopBehavior.StopEmitting);
            StopEmit(_smoke, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>Stop and clear all particles immediately (call on reset).</summary>
        public void ClearTrail()
        {
            StopEmit(_flame, ParticleSystemStopBehavior.StopEmittingAndClear);
            StopEmit(_smoke, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void PlayClean(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Clear();
            ps.Play();
        }

        private static void StopEmit(ParticleSystem ps, ParticleSystemStopBehavior behavior)
        {
            if (ps == null) return;
            ps.Stop(true, behavior);
        }

        // ---------------- Flame ----------------

        private ParticleSystem CreateFlameSystem()
        {
            var go = new GameObject("FlameParticles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.4f, 0f);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = _flameLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(_flameStartSize * 0.7f, _flameStartSize * 1.3f);
            // Per-particle random color: red ↔ orange ↔ yellow-orange
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.15f, 0f, 1f),
                new Color(1f, 0.7f, 0.1f, 1f))
            { mode = ParticleSystemGradientMode.TwoColors };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.playOnAwake = false;
            main.gravityModifier = -0.05f; // slight upward float

            var emission = ps.emission;
            emission.rateOverTime = _flameEmissionRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.04f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.05f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            // Each sub-system gets its own material instance so mainTexture/blend tweaks don't bleed across effects
            var flameMat = RuntimeSpriteFactory.CreateParticleMaterialInstance(additive: true);
            var soft = RuntimeSpriteFactory.GetSoftCircleSprite();
            if (soft != null && soft.texture != null)
                flameMat.mainTexture = soft.texture;
            renderer.material = flameMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "Projectile";
            renderer.sortingOrder = 0;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        // ---------------- Smoke ----------------

        private ParticleSystem CreateSmokeSystem()
        {
            var go = new GameObject("SmokeParticles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.45f, 0f);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(_smokeLifetime * 0.7f, _smokeLifetime * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(_smokeStartSize * 0.8f, _smokeStartSize * 1.4f);
            // Per-particle random color: dark grey ↔ light grey
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.18f, 0.16f, 0.15f, 0.8f),
                new Color(0.55f, 0.53f, 0.5f, 0.6f))
            { mode = ParticleSystemGradientMode.TwoColors };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            main.playOnAwake = false;
            main.gravityModifier = -0.15f; // smoke drifts up

            var emission = ps.emission;
            emission.rateOverTime = _smokeEmissionRate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.06f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            // Smoke grows as it disperses (1 → 2.5x)
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 2.5f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.15f),
                    new GradientAlphaKey(0.5f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = grad;

            var rotOverLifetime = ps.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-30f, 30f); // gentle spin

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            // Smoke uses standard alpha-blend (NOT additive — additive smoke looks like fire)
            var smokeMat = RuntimeSpriteFactory.CreateParticleMaterialInstance(additive: false);
            var soft = RuntimeSpriteFactory.GetSoftCircleSprite();
            if (soft != null && soft.texture != null)
                smokeMat.mainTexture = soft.texture;
            renderer.material = smokeMat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "Projectile";
            renderer.sortingOrder = -1; // render BEHIND flame

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }
    }
}
