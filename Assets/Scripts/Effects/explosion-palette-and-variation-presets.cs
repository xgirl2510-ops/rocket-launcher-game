using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Palette + variation presets for ExplosionEffect — picked once per explosion so each
    /// blast looks visually distinct. Provides:
    ///  - 5 fireball gradient presets (each is a 4-stop Gradient + an alpha curve)
    ///  - 3 smoke gradient presets
    ///  - per-explosion size/count/lifetime multipliers (random in [0.85, 1.2])
    /// </summary>
    public readonly struct ExplosionVariation
    {
        public readonly Gradient FireballGradient;
        public readonly Gradient SmokeGradient;
        public readonly float SizeMultiplier;
        public readonly float CountMultiplier;
        public readonly float LifetimeMultiplier;

        public ExplosionVariation(Gradient fireball, Gradient smoke,
            float sizeMul, float countMul, float lifeMul)
        {
            FireballGradient = fireball;
            SmokeGradient = smoke;
            SizeMultiplier = sizeMul;
            CountMultiplier = countMul;
            LifetimeMultiplier = lifeMul;
        }

        /// <summary>Roll a fresh variation: random palette pick + random ±20% multipliers.</summary>
        public static ExplosionVariation RollRandom(bool isHit)
        {
            var fireball = ExplosionPalettes.PickFireball(isHit);
            var smoke = ExplosionPalettes.PickSmoke();
            float size = Random.Range(0.85f, 1.2f);
            float count = Random.Range(0.8f, 1.25f);
            float life = Random.Range(0.85f, 1.2f);
            return new ExplosionVariation(fireball, smoke, size, count, life);
        }
    }

    /// <summary>Static palette library — 5 fireball + 3 smoke gradient presets.</summary>
    public static class ExplosionPalettes
    {
        // 5 fireball palettes — picked randomly per explosion. Each palette has 4 color stops
        // for richer variation than 2-color min/max.
        private static readonly Color[][] _fireballHitPalettes =
        {
            // Hit (target) — bright golden tones
            new[] { new Color(1f, 1f, 0.95f), new Color(1f, 0.9f, 0.3f), new Color(1f, 0.55f, 0.05f), new Color(0.6f, 0.15f, 0f) },
            new[] { new Color(1f, 0.95f, 0.7f), new Color(1f, 0.75f, 0.2f), new Color(1f, 0.4f, 0.05f), new Color(0.4f, 0.1f, 0f) },
            new[] { new Color(1f, 1f, 0.6f), new Color(1f, 0.85f, 0.15f), new Color(0.95f, 0.5f, 0f), new Color(0.5f, 0.18f, 0.05f) },
        };

        private static readonly Color[][] _fireballMissPalettes =
        {
            // Miss (ground) — orange/red toned
            new[] { new Color(1f, 0.9f, 0.5f), new Color(1f, 0.5f, 0.05f), new Color(0.85f, 0.15f, 0f), new Color(0.3f, 0.05f, 0f) },
            new[] { new Color(1f, 0.7f, 0.2f), new Color(1f, 0.35f, 0f), new Color(0.7f, 0.1f, 0f), new Color(0.25f, 0.05f, 0.05f) },
            new[] { new Color(1f, 0.85f, 0.35f), new Color(1f, 0.55f, 0.1f), new Color(0.9f, 0.2f, 0f), new Color(0.4f, 0.08f, 0f) },
            new[] { new Color(1f, 0.95f, 0.85f), new Color(1f, 0.6f, 0.2f), new Color(0.85f, 0.25f, 0.05f), new Color(0.35f, 0.05f, 0f) },
            // Slight purple-tinted variant — fuel additives, rare burns
            new[] { new Color(1f, 0.8f, 0.6f), new Color(1f, 0.4f, 0.2f), new Color(0.7f, 0.15f, 0.2f), new Color(0.25f, 0.05f, 0.1f) },
        };

        // 3 smoke palettes — picked randomly. Black-ish carbon tones since rocket fuel burns dirty.
        private static readonly Color[][] _smokePalettes =
        {
            // Pure black carbon
            new[] { new Color(0.08f, 0.08f, 0.08f), new Color(0.04f, 0.04f, 0.04f), new Color(0.18f, 0.16f, 0.15f), new Color(0.3f, 0.28f, 0.26f) },
            // Brown-black (oil-rich fuel)
            new[] { new Color(0.12f, 0.08f, 0.05f), new Color(0.05f, 0.04f, 0.03f), new Color(0.25f, 0.18f, 0.13f), new Color(0.35f, 0.28f, 0.22f) },
            // Dark grey (cleaner burn)
            new[] { new Color(0.18f, 0.18f, 0.18f), new Color(0.1f, 0.1f, 0.1f), new Color(0.32f, 0.32f, 0.32f), new Color(0.45f, 0.43f, 0.42f) },
        };

        public static Gradient PickFireball(bool isHit)
        {
            var pool = isHit ? _fireballHitPalettes : _fireballMissPalettes;
            return BuildGradient(pool[Random.Range(0, pool.Length)]);
        }

        public static Gradient PickSmoke()
        {
            return BuildGradient(_smokePalettes[Random.Range(0, _smokePalettes.Length)]);
        }

        // Build a Gradient with N color stops evenly spaced. Used by MinMaxGradient in RandomColor mode
        // so each particle samples a random point along the gradient.
        private static Gradient BuildGradient(Color[] colors)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                ck[i] = new GradientColorKey(colors[i], colors.Length == 1 ? 0f : (float)i / (colors.Length - 1));
            }
            // Alpha stays 1 — actual fade is handled by colorOverLifetime in the particle system
            var ak = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            g.SetKeys(ck, ak);
            return g;
        }
    }
}
