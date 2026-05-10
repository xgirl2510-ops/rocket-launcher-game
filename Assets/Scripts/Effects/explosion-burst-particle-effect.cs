using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Multi-layer explosion effect: flash (instant white pop), fireball (red/orange burst),
    /// shockwave ring (expanding circle), smoke plume (lingering grey), sparks (small bright streaks).
    /// Auto-destroys after the longest particle finishes. Spawn via static helper.
    /// </summary>
    public class ExplosionEffect : MonoBehaviour
    {
        // Tuning per layer — kept as constants since explosions are one-shot and fixed-feel.
        private const float SmokeLifetime = 2.5f;     // longest layer drives destroy time — black smoke lingers
        private const float DestroyPadding = 0.3f;

        // Tracks all live explosion GameObjects so ClearAll() can remove leftover effects on round reset.
        private static readonly System.Collections.Generic.List<GameObject> _allExplosions =
            new System.Collections.Generic.List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => _allExplosions.Clear();

        /// <summary>Spawn an explosion at the given position. isHit=true → gold burst, false → red.</summary>
        public static void Spawn(Vector2 position, bool isHit)
        {
            Spawn(position, isHit, isVerticalImpact: !isHit); // default: ground hits get mushroom
        }

        /// <summary>
        /// Spawn an explosion. <paramref name="isVerticalImpact"/> = true triggers mushroom-cloud smoke
        /// (stem + grand cap + dust ring) — physically only correct when the rocket struck a surface
        /// from above. Side hits should pass false to use rounded omnidirectional smoke instead.
        /// </summary>
        public static void Spawn(Vector2 position, bool isHit, bool isVerticalImpact)
        {
            var go = new GameObject("Explosion");
            go.transform.position = new Vector3(position.x, position.y, 0f);

            var fx = go.AddComponent<ExplosionEffect>();
            fx.PlayLayered(isHit, isVerticalImpact);
            _allExplosions.Add(go);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Debug: confirms the impact point used to anchor effects
            Debug.Log($"[Explosion] Spawn at y={position.y:F2} (isHit={isHit}, isVertical={isVerticalImpact}) GroundTop={GameConstants.GroundTop}");
#endif
        }

        /// <summary>Destroy all live explosion GameObjects (call on round reload to remove leftover smoke/fire).</summary>
        public static void ClearAll()
        {
            for (int i = _allExplosions.Count - 1; i >= 0; i--)
            {
                if (_allExplosions[i] != null)
                    Destroy(_allExplosions[i]);
            }
            _allExplosions.Clear();
        }

        private void OnDestroy() => _allExplosions.Remove(gameObject);

        private void PlayLayered(bool isHit, bool isVerticalImpact)
        {
            // Roll palette + size/count/lifetime variation ONCE per explosion so all layers stay consistent
            // — this is what makes each blast look distinct from the previous one.
            var v = ExplosionVariation.RollRandom(isHit);

            CreateFlash(isHit, v);
            CreateFireball(isHit, v);

            if (isVerticalImpact)
            {
                // Ground impact: smoke column from ground + lingering fire on soil + dust
                CreateSmokeStem(v);
                CreateLingeringFire(v);
                CreateDustRing(v);
            }
            else
            {
                // Mid-air target hit: smoke + dust burst AT the impact point,
                // burning embers shower DOWN from the impact point (gravity).
                CreateSmoke(v);
                CreateImpactDustPuff(v);
                CreateFallingEmbers(v);
            }

            CreateSparks(isHit, v);

            // Smoke is the longest layer; pad destroy time by lifetime multiplier
            Destroy(gameObject, SmokeLifetime * v.LifetimeMultiplier + DestroyPadding);
        }

        // ---------------- Flash (instant bright pop) ----------------

        private void CreateFlash(bool isHit, ExplosionVariation v)
        {
            var ps = NewSubsystem("Flash");
            var main = ps.main;
            main.startLifetime = 0.12f * v.LifetimeMultiplier;
            main.startSpeed = 0f;
            main.startSize = (isHit ? 4f : 3.2f) * v.SizeMultiplier;
            main.startColor = isHit ? new Color(1f, 1f, 0.8f, 1f) : new Color(1f, 1f, 1f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 4;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            ApplySoftAdditive(ps, sortingOrder: 12);
            ps.Play();
        }

        // ---------------- Fireball (red/orange burst) ----------------

        private void CreateFireball(bool isHit, ExplosionVariation v)
        {
            var ps = NewSubsystem("Fireball");
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f * v.LifetimeMultiplier, 1.0f * v.LifetimeMultiplier);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * v.SizeMultiplier, 0.85f * v.SizeMultiplier);
            // RandomColor mode: each particle samples a random point along the 4-stop palette gradient,
            // so a single explosion has many distinct hues simultaneously (not just two endpoints).
            main.startColor = new ParticleSystem.MinMaxGradient(v.FireballGradient)
            { mode = ParticleSystemGradientMode.RandomColor };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.playOnAwake = false;
            main.gravityModifier = -0.15f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            int fireballBurst = Mathf.RoundToInt(120 * v.CountMultiplier);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, fireballBurst) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;
            shape.randomDirectionAmount = 1f; // full random outward

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            ApplySoftAdditive(ps, sortingOrder: 11);
            ps.Play();
        }

        // ---------------- Smoke (lingering black smoke from burnt fuel) ----------------

        private void CreateSmoke(ExplosionVariation v)
        {
            var ps = NewSubsystem("Smoke");
            var main = ps.main;
            // Shorter lifetime so smoke doesn't drift far from impact before fading
            float life = SmokeLifetime * 0.7f * v.LifetimeMultiplier;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            // Slow expansion — smoke billows around impact, not blasting outward
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * v.SizeMultiplier, 1.1f * v.SizeMultiplier);
            // Random sample along smoke gradient — distinct hue per smoke billow within the same explosion
            main.startColor = new ParticleSystem.MinMaxGradient(v.SmokeGradient)
            { mode = ParticleSystemGradientMode.RandomColor };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.playOnAwake = false;
            // Tiny upward drift — smoke stays near the impact point, doesn't shoot to the sky.
            // Previous value -0.35 caused acceleration of +3.4 m/s² → particles flew 10+ units up.
            main.gravityModifier = -0.05f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            int smokeBurst = Mathf.RoundToInt(60 * v.CountMultiplier);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, smokeBurst) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            shape.randomDirectionAmount = 1f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 2.5f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f),
                    new GradientAlphaKey(0.5f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-40f, 40f);

            ApplyAlphaBlend(ps, sortingOrder: 9);
            ps.Play();
        }

        // ---------------- Mushroom Cloud: Stem (rising column) ----------------

        // Stem column rise height above impact. Cap sits exactly at this altitude.
        // Lower value = cap closer to impact = mushroom feels snappier and less floaty.
        private const float StemRiseHeight = 3.5f;

        private void CreateSmokeStem(ExplosionVariation v)
        {
            var ps = NewSubsystem("SmokeStem");
            // Anchor at the CRATER LIP (same anchor logic as LingeringFire) so the column
            // rises from the visible mouth of the hole instead of being buried at the crater
            // floor where it gets occluded by the dirt walls and looks detached from the fire.
            float craterFloorY = GroundScorch.GetGroundY(transform.position.x);
            float craterDepth = GameConstants.GroundTop - craterFloorY; // positive
            float anchorY = craterFloorY + craterDepth * 0.3f;
            float groundLocalY = anchorY - transform.position.y;
            ps.transform.localPosition = new Vector3(0f, groundLocalY, 0f);

            var main = ps.main;
            // Fast stem: speed × lifetime ≈ StemRiseHeight (3.5).
            //   speed 5.5 × life 0.65 = 3.575 → particle reaches just under cap altitude ✓
            //   emit window 0.4s → column fills from impact to ~mid-rise rapidly
            float particleLife = 0.65f * v.LifetimeMultiplier;
            main.startLifetime = particleLife;
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * v.SizeMultiplier, 0.85f * v.SizeMultiplier);
            main.startColor = new ParticleSystem.MinMaxGradient(v.SmokeGradient)
            { mode = ParticleSystemGradientMode.RandomColor };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;
            main.playOnAwake = false;
            main.gravityModifier = 0f;
            main.duration = 0.4f; // tight emit window — stem shoots up fast and stops

            var emission = ps.emission;
            // High rate compensates for short window → ~36 particles total over 0.4s
            emission.rateOverTime = 90f * v.CountMultiplier;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            // SingleSidedEdge along X axis — particles spawn along a horizontal line and
            // fire upward (+Y) by default. Unambiguous in 2D ortho setups (no Z-axis confusion).
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(0f, 0f, 0f);

            // velocityOverLifetime omitted entirely — any usage in this 2D setup caused freezes.
            // Stem deceleration is achieved by gravityModifier alone (negative for buoyancy,
            // positive damping via Linear Drag if needed in main module).

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            // Stem stays roughly column-shaped — only slight growth so it doesn't flare into a triangle
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 1.3f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f),
                    new GradientAlphaKey(0.5f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-60f, 60f);

            // Noise gives the stem a billowing roll instead of straight-line drift
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.8f);
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.5f;
            noise.damping = true;

            ApplyAlphaBlend(ps, sortingOrder: 9);
            ps.Play();
        }

        // ---------------- Lingering Fire (residual flames burning at impact) ----------------

        // How long flames keep burning at the impact point after the initial explosion subsides.
        private const float LingeringFireDuration = 2.5f;

        private void CreateLingeringFire(ExplosionVariation v)
        {
            var ps = NewSubsystem("LingeringFire");
            // Anchor a bit ABOVE the crater floor so flames are clearly visible inside the bowl
            // — not buried under the dirt ridge of the crater wall. Lift = 30% of crater depth.
            float craterFloorY = GroundScorch.GetGroundY(transform.position.x);
            float craterDepth = GameConstants.GroundTop - craterFloorY; // positive number
            float anchorY = craterFloorY + craterDepth * 0.3f;
            float groundLocalY = anchorY - transform.position.y;
            ps.transform.localPosition = new Vector3(0f, groundLocalY, 0f);

            var main = ps.main;
            // Slightly longer flickers for a more substantial flame
            float particleLife = 0.7f * v.LifetimeMultiplier;
            main.startLifetime = new ParticleSystem.MinMaxCurve(particleLife * 0.7f, particleLife);
            // Faster rise so flames lick higher
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
            // Bigger flame particles — bolder burn
            main.startSize = new ParticleSystem.MinMaxCurve(0.45f * v.SizeMultiplier, 0.9f * v.SizeMultiplier);
            main.startColor = new ParticleSystem.MinMaxGradient(v.FireballGradient)
            { mode = ParticleSystemGradientMode.RandomColor };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;
            main.playOnAwake = false;
            main.gravityModifier = -0.2f; // stronger upward lick
            main.duration = LingeringFireDuration;

            // Higher rate → denser, more visibly intense fire
            var emission = ps.emission;
            emission.rateOverTime = 60f * v.CountMultiplier;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            // Wider base → flames cover a bigger patch of ground
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.4f * v.SizeMultiplier, 0.05f, 0.01f);
            shape.randomDirectionAmount = 0.3f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            // Flames shrink as they rise and burn out
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            ApplySoftAdditive(ps, sortingOrder: 11);
            ps.Play();
        }

        // ---------------- Mid-air target hit: Impact Dust + Falling Embers ----------------

        // How long embers fall before fading (free-fall sqrt(2h/g) for ~10 units ≈ 1.4s).
        private const float EmbersFallDuration = 1.4f;

        private void CreateFallingEmbers(ExplosionVariation v)
        {
            var ps = NewSubsystem("FallingEmbers");
            // Anchored at impact point — embers shower DOWN from where the target was hit.

            var main = ps.main;
            // Long enough for embers to reach ground (~1.4s free-fall from typical target height)
            main.startLifetime = new ParticleSystem.MinMaxCurve(EmbersFallDuration * 0.8f, EmbersFallDuration);
            // Initial outward burst before gravity takes over
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f * v.SizeMultiplier, 0.35f * v.SizeMultiplier);
            // Bright fireball palette — embers glow as they fall
            main.startColor = new ParticleSystem.MinMaxGradient(v.FireballGradient)
            { mode = ParticleSystemGradientMode.RandomColor };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;
            main.playOnAwake = false;
            // Strong positive gravity → embers fall realistically, like sparks from a flare
            main.gravityModifier = 1.0f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            int burst = Mathf.RoundToInt(50 * v.CountMultiplier);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });

            // Hemisphere-down: spawn in a downward-biased burst
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.arc = 360f;
            shape.randomDirectionAmount = 1f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            // Embers shrink as they cool/fall
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f),
                    new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            ApplySoftAdditive(ps, sortingOrder: 12);
            ps.Play();
        }

        private void CreateImpactDustPuff(ExplosionVariation v)
        {
            var ps = NewSubsystem("ImpactDustPuff");
            // Anchored at impact point — dust puff radiates outward from where the rocket struck the target.

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * v.SizeMultiplier, 0.7f * v.SizeMultiplier);
            // Dirt-tone dust — pulverized debris from the target object
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.45f, 0.4f, 0.35f, 0.7f),
                new Color(0.7f, 0.62f, 0.55f, 0.85f))
            { mode = ParticleSystemGradientMode.TwoColors };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.playOnAwake = false;
            main.gravityModifier = 0f; // suspended in mid-air around impact

            var emission = ps.emission;
            emission.rateOverTime = 0;
            int burst = Mathf.RoundToInt(35 * v.CountMultiplier);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });

            // Omni-directional puff — dust expands outward in all directions from impact
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            shape.arc = 360f;
            shape.randomDirectionAmount = 1f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 1.6f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f),
                    new GradientAlphaKey(0.4f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            ApplyAlphaBlend(ps, sortingOrder: 8);
            ps.Play();
        }

        // ---------------- Mushroom Cloud: Dust Ring (ground-level outward burst) ----------------

        private void CreateDustRing(ExplosionVariation v)
        {
            var ps = NewSubsystem("DustRing");
            // Anchor at ground surface so dust kicks up FROM the soil, not from rocket-center altitude.
            float groundLocalY = GroundScorch.GetGroundY(transform.position.x) - transform.position.y;
            ps.transform.localPosition = new Vector3(0f, groundLocalY, 0f);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f * v.SizeMultiplier, 0.7f * v.SizeMultiplier);
            // Lighter dirt-tinted dust — different from smoke, suggests pulverized ground
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.45f, 0.4f, 0.35f, 0.7f),
                new Color(0.7f, 0.62f, 0.55f, 0.85f))
            { mode = ParticleSystemGradientMode.TwoColors };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.playOnAwake = false;
            main.gravityModifier = 0.1f; // dust falls slightly back down

            var emission = ps.emission;
            emission.rateOverTime = 0;
            int burst = Mathf.RoundToInt(30 * v.CountMultiplier);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });

            // Circle 2D — particles spawn around the edge and shoot outward (radial).
            // randomDirectionAmount = 1 ensures each particle gets a randomized outward vector.
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.arc = 360f;
            shape.randomDirectionAmount = 1f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 1.5f));

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.1f),
                    new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-30f, 30f);

            ApplyAlphaBlend(ps, sortingOrder: 8);
            ps.Play();
        }

        // ---------------- Sparks (small bright streaks) ----------------

        private void CreateSparks(bool isHit, ExplosionVariation v)
        {
            var ps = NewSubsystem("Sparks");
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f * v.LifetimeMultiplier, 1.0f * v.LifetimeMultiplier);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f * v.SizeMultiplier, 0.12f * v.SizeMultiplier);
            // Sparks share the same fireball palette — they're hot metal bits from the same explosion
            main.startColor = new ParticleSystem.MinMaxGradient(v.FireballGradient)
            { mode = ParticleSystemGradientMode.RandomColor };
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;
            main.playOnAwake = false;
            main.gravityModifier = 0.6f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            int sparkBurst = Mathf.RoundToInt(35 * v.CountMultiplier);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, sparkBurst) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;
            shape.randomDirectionAmount = 1f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ApplySoftAdditive(ps, sortingOrder: 13);
            ps.Play();
        }

        // ---------------- Helpers ----------------

        private ParticleSystem NewSubsystem(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private static void ApplySoftAdditive(ParticleSystem ps, int sortingOrder)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var mat = RuntimeSpriteFactory.CreateParticleMaterialInstance(additive: true);
            var soft = RuntimeSpriteFactory.GetSoftCircleSprite();
            if (soft != null && soft.texture != null) mat.mainTexture = soft.texture;
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "Projectile";
            renderer.sortingOrder = sortingOrder;
        }

        private static void ApplyAlphaBlend(ParticleSystem ps, int sortingOrder)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var mat = RuntimeSpriteFactory.CreateParticleMaterialInstance(additive: false);
            var soft = RuntimeSpriteFactory.GetSoftCircleSprite();
            if (soft != null && soft.texture != null) mat.mainTexture = soft.texture;
            renderer.material = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = "Projectile";
            renderer.sortingOrder = sortingOrder;
        }
    }
}
