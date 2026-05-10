using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Centralized factory for runtime-generated sprites and materials.
    /// Eliminates duplicate 4x4 white texture creation across RocketDebris,
    /// ObstacleSpawner, GroundScorch, and duplicate Sprites/Default material
    /// creation across RocketTrail and ExplosionEffect.
    /// </summary>
    public static class RuntimeSpriteFactory
    {
        private static Sprite _solidSprite;
        private static Sprite _softCircleSprite;
        private static Sprite _hardCircleSprite;
        private static Sprite _triangleSprite;
        private static Sprite _trapezoidSprite;
        private static Sprite _sliverSprite;
        private static Material _particleMaterial;
        private static Material _additiveParticleMaterial;
        private static Sprite[] _debrisShapes;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_solidSprite != null)
            {
                Object.Destroy(_solidSprite.texture);
                Object.Destroy(_solidSprite);
            }
            _solidSprite = null;

            if (_softCircleSprite != null)
            {
                Object.Destroy(_softCircleSprite.texture);
                Object.Destroy(_softCircleSprite);
            }
            _softCircleSprite = null;

            DestroyShape(ref _hardCircleSprite);
            DestroyShape(ref _triangleSprite);
            DestroyShape(ref _trapezoidSprite);
            DestroyShape(ref _sliverSprite);
            _debrisShapes = null;

            if (_particleMaterial != null)
                Object.Destroy(_particleMaterial);
            _particleMaterial = null;

            if (_additiveParticleMaterial != null)
                Object.Destroy(_additiveParticleMaterial);
            _additiveParticleMaterial = null;
        }

        private static void DestroyShape(ref Sprite s)
        {
            if (s != null)
            {
                Object.Destroy(s.texture);
                Object.Destroy(s);
            }
            s = null;
        }

        /// <summary>4x4 white solid sprite, pivot center, PPU 4. Shared by debris, obstacles, scorch.</summary>
        public static Sprite GetSolidSprite()
        {
            if (_solidSprite != null) return _solidSprite;

            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            System.Array.Fill(pixels, Color.white);
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            _solidSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return _solidSprite;
        }

        /// <summary>Sprites/Default material for particle systems (always included in builds).</summary>
        public static Material GetParticleMaterial()
        {
            if (_particleMaterial != null) return _particleMaterial;

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[RuntimeSpriteFactory] Sprites/Default shader not found. " +
                    "Ensure it is in Project Settings > Graphics > Always Included Shaders.");
#endif
                shader = Shader.Find("UI/Default");
            }

            _particleMaterial = new Material(shader);
            return _particleMaterial;
        }

        /// <summary>
        /// Soft radial-gradient circle sprite (32x32, alpha fades 1→0 from center to edge).
        /// Used for flame/smoke particles to avoid hard square edges.
        /// </summary>
        public static Sprite GetSoftCircleSprite()
        {
            if (_softCircleSprite != null) return _softCircleSprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float maxDist = center;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float t = Mathf.Clamp01(1f - dist / maxDist);
                // Smoothstep curve for softer falloff
                float alpha = t * t * (3f - 2f * t);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            _softCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _softCircleSprite;
        }

        /// <summary>
        /// Create a fresh particle material instance using Sprites/Default shader.
        /// Pass <paramref name="additive"/> = true for glowing flame/spark blend, false for normal alpha smoke.
        /// Each caller gets its own instance so mainTexture assignments don't collide.
        /// </summary>
        public static Material CreateParticleMaterialInstance(bool additive)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[RuntimeSpriteFactory] Sprites/Default shader not found. " +
                    "Ensure it is in Project Settings > Graphics > Always Included Shaders.");
#endif
                shader = Shader.Find("UI/Default");
            }

            var mat = new Material(shader);
            // Sprites/Default uses fixed-function blend by default (SrcAlpha, OneMinusSrcAlpha) — that's normal alpha.
            // For additive glow we override the destination blend factor via material property block.
            if (additive)
            {
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
            }
            return mat;
        }

        /// <summary>
        /// Returns 5 cached debris shapes (square, hard-circle, triangle, trapezoid, long sliver).
        /// Used by RocketDebris to give each piece a different silhouette.
        /// </summary>
        public static Sprite[] GetDebrisShapes()
        {
            if (_debrisShapes != null) return _debrisShapes;

            _debrisShapes = new[]
            {
                GetSolidSprite(),       // square
                GetHardCircleSprite(),  // round
                GetTriangleSprite(),    // pointed
                GetTrapezoidSprite(),   // chunky
                GetSliverSprite()       // long thin
            };
            return _debrisShapes;
        }

        /// <summary>16x16 hard circle (no alpha gradient — solid silhouette for debris).</summary>
        public static Sprite GetHardCircleSprite()
        {
            if (_hardCircleSprite != null) return _hardCircleSprite;
            _hardCircleSprite = BuildShapeSprite(16, 16, (x, y, w, h) =>
            {
                float dx = x - (w - 1) * 0.5f;
                float dy = y - (h - 1) * 0.5f;
                float r = (w - 1) * 0.5f;
                return dx * dx + dy * dy <= r * r ? 1f : 0f;
            });
            return _hardCircleSprite;
        }

        /// <summary>16x16 upward-pointing triangle.</summary>
        public static Sprite GetTriangleSprite()
        {
            if (_triangleSprite != null) return _triangleSprite;
            _triangleSprite = BuildShapeSprite(16, 16, (x, y, w, h) =>
            {
                // Triangle: y >= 0 means inside if x is within the narrowing band
                float t = y / (float)(h - 1);            // 0 at bottom, 1 at top
                float halfWidth = (1f - t) * (w - 1) * 0.5f;
                float center = (w - 1) * 0.5f;
                return Mathf.Abs(x - center) <= halfWidth ? 1f : 0f;
            });
            return _triangleSprite;
        }

        /// <summary>16x10 trapezoid (chunky debris shape).</summary>
        public static Sprite GetTrapezoidSprite()
        {
            if (_trapezoidSprite != null) return _trapezoidSprite;
            _trapezoidSprite = BuildShapeSprite(16, 10, (x, y, w, h) =>
            {
                float t = y / (float)(h - 1);
                float halfWidth = Mathf.Lerp((w - 1) * 0.5f, (w - 1) * 0.25f, t);
                float center = (w - 1) * 0.5f;
                return Mathf.Abs(x - center) <= halfWidth ? 1f : 0f;
            });
            return _trapezoidSprite;
        }

        /// <summary>20x4 long thin sliver (looks like a metal strip).</summary>
        public static Sprite GetSliverSprite()
        {
            if (_sliverSprite != null) return _sliverSprite;
            _sliverSprite = BuildShapeSprite(20, 4, (x, y, w, h) => 1f);
            return _sliverSprite;
        }

        // Helper: build a small RGBA sprite using a per-pixel alpha function.
        private static Sprite BuildShapeSprite(int w, int h, System.Func<int, int, int, int, float> alphaFn)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Clamp01(alphaFn(x, y, w, h));
                pixels[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            float ppu = Mathf.Max(w, h);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        }
    }
}
