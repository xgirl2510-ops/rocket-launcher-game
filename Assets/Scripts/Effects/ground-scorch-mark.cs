using System.Collections.Generic;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Static utility: creates real crater holes in the ground at rocket impact points.
    /// One SpriteMask per crater cuts the ground sprite (VisibleOutsideMask) — the hole
    /// reveals whatever sits behind the ground (sky background by default). No fill layer.
    /// Also spawns dirt debris via RocketDebris.SpawnDirtDebris() and tracks crater geometry
    /// so GetGroundY can return correct floor heights for falling debris.
    /// Persists until ClearAll() is called (round restart).
    /// </summary>
    public static class GroundScorch
    {
        /// <summary>Per-crater positional data for ground-Y lookup.</summary>
        private struct CraterData
        {
            /// <summary>World X position of crater center.</summary>
            public float X;
            /// <summary>Horizontal width of the crater hole.</summary>
            public float Width;
            /// <summary>Vertical depth of the crater hole.</summary>
            public float Depth;
        }

        private static readonly List<GameObject> _allCraters = new List<GameObject>();
        private static readonly List<CraterData> _craters = new List<CraterData>();
        private static bool _groundPrepared;

        private const int MaskVariantCount = 8;

        private const float SmallCraterHeightThreshold = 15f;
        private const float MediumCraterHeightThreshold = 30f;
        private const float SmallCraterMinScale = 0.8f;
        private const float SmallCraterMaxScale = 1.2f;
        private const float MediumCraterMinScale = 1.2f;
        private const float MediumCraterMaxScale = 1.8f;
        private const float LargeCraterMinScale = 1.8f;
        private const float LargeCraterMaxScale = 2.5f;
        private static Sprite[] _maskVariants;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _allCraters.Clear();
            _craters.Clear();
            DestroyMaskVariants();
            _groundPrepared = false;
        }

        /// <summary>Destroy cached mask variant textures/sprites to prevent GPU memory leak.</summary>
        private static void DestroyMaskVariants()
        {
            DestroySpriteArray(ref _maskVariants);
        }

        private static void DestroySpriteArray(ref Sprite[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null)
                {
                    Object.Destroy(arr[i].texture);
                    Object.Destroy(arr[i]);
                }
            }
            arr = null;
        }

        /// <summary>Enable mask interaction on ground so SpriteMasks cut it.</summary>
        public static void PrepareGround(Transform ground)
        {
            if (_groundPrepared) return;
            if (ground == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[GroundScorch] Ground transform not provided — craters disabled. Wire via Tools > Rocket Launcher > Setup Scene.");
#endif
                return;
            }
            var sr = ground.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            _groundPrepared = true;
        }

        /// <summary>Spawn a real crater hole in the ground. Size scales with max flight height.</summary>
        public static void Spawn(Vector2 impactPosition, float maxHeight = 10f, Transform ground = null)
        {
            PrepareGround(ground);
            EnsureMaskVariants();

            float groundY = GameConstants.GroundTop;
            float scale = CalculateCraterScale(maxHeight, groundY);
            float craterW = Random.Range(1.2f, 1.6f) * scale;
            float craterH = Random.Range(0.8f, 1.2f) * scale;

            // Remove any pre-existing crater whose footprint overlaps this new impact.
            // Without this, repeated hits at the same spot stack masks + floor sprites,
            // doubling alpha and producing the visible "two craters mashed together" look.
            RemoveOverlappingCraters(impactPosition.x, craterW);

            var parent = CreateCraterGameObject(impactPosition, groundY, craterW, craterH);

            RocketDebris.SpawnDirtDebris(impactPosition, scale);

            _craters.Add(new CraterData { X = impactPosition.x, Width = craterW, Depth = craterH });
            _allCraters.Add(parent);
        }

        /// <summary>
        /// Destroy and de-register craters whose center lies within the new impact's footprint.
        /// Prevents stacking: the new (potentially larger) crater fully replaces the old one.
        /// </summary>
        private static void RemoveOverlappingCraters(float impactX, float newCraterWidth)
        {
            // Use the same horizontal extent metric as GetGroundY (halfW = Width * 0.4).
            // If an old crater's center sits inside the new crater's effective half-width
            // (or vice versa), the visible holes overlap and the old one must go.
            float newHalfW = newCraterWidth * 0.4f;
            for (int i = _craters.Count - 1; i >= 0; i--)
            {
                float oldHalfW = _craters[i].Width * 0.4f;
                float dx = Mathf.Abs(impactX - _craters[i].X);
                if (dx < newHalfW + oldHalfW * 0.5f)
                {
                    if (_allCraters[i] != null)
                        Object.Destroy(_allCraters[i]);
                    _allCraters.RemoveAt(i);
                    _craters.RemoveAt(i);
                }
            }
        }

        private static float CalculateCraterScale(float maxHeight, float groundY)
        {
            float heightAboveGround = Mathf.Max(0f, maxHeight - groundY);
            if (heightAboveGround <= SmallCraterHeightThreshold)
                return Random.Range(SmallCraterMinScale, SmallCraterMaxScale);
            if (heightAboveGround <= MediumCraterHeightThreshold)
                return Random.Range(MediumCraterMinScale, MediumCraterMaxScale);
            return Random.Range(LargeCraterMinScale, LargeCraterMaxScale);
        }

        private static GameObject CreateCraterGameObject(Vector2 impactPosition, float groundY,
            float craterW, float craterH)
        {
            var parent = new GameObject("Crater");
            parent.transform.position = new Vector3(impactPosition.x, groundY, 0f);

            // SpriteMask — cuts a smooth jagged hole through the ground sprite.
            var maskGo = new GameObject("CraterMask");
            maskGo.transform.SetParent(parent.transform, false);
            float maskW = craterW * 1.15f;
            float maskH = craterH * 1.1f;
            maskGo.transform.localScale = new Vector3(maskW, maskH, 1f);
            var mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = _maskVariants[Random.Range(0, MaskVariantCount)];
            mask.alphaCutoff = 0.5f;

            // The SpriteMask above cuts a hole in the ground sprite. No fill layer — the
            // hole simply reveals whatever sits behind the ground (sky background by default).
            return parent;
        }

        /// <summary>
        /// Returns ground Y at given X, accounting for craters.
        /// Debris uses this to fall into crater holes instead of stopping at surface.
        /// </summary>
        public static float GetGroundY(float x)
        {
            float baseY = GameConstants.GroundTop;
            for (int i = 0; i < _craters.Count; i++)
            {
                float halfW = _craters[i].Width * 0.4f;
                float dx = Mathf.Abs(x - _craters[i].X);
                if (dx >= halfW) continue;

                float t = 1f - (dx / halfW);
                float depth = _craters[i].Depth * t * 0.7f;
                float craterY = baseY - depth;
                if (craterY < baseY)
                    baseY = craterY;
            }
            return baseY;
        }

        /// <summary>Destroy all craters (call on round restart).</summary>
        public static void ClearAll()
        {
            for (int i = _allCraters.Count - 1; i >= 0; i--)
            {
                if (_allCraters[i] != null)
                    Object.Destroy(_allCraters[i]);
            }
            _allCraters.Clear();
            _craters.Clear();
            _groundPrepared = false;
        }

        private static void EnsureMaskVariants()
        {
            if (_maskVariants == null)
            {
                _maskVariants = new Sprite[MaskVariantCount];
                for (int i = 0; i < MaskVariantCount; i++)
                    _maskVariants[i] = BuildMaskSprite();
            }
        }

        /// <summary>
        /// Jagged semicircle mask for the SpriteMask cut.
        /// Improvements over previous version:
        ///   - 256×256 (4× resolution) for smooth scaling
        ///   - Multi-octave Perlin noise (3 layers) for organic, non-repetitive edges
        ///   - Smoothstep alpha falloff at edge (no hard cutoff = no pixelated rim)
        ///   - Bilinear filter (smooth interpolation when scaled)
        ///   - Pivot at top-center so the half-circle hangs DOWN from ground line.
        /// </summary>
        private static Sprite BuildMaskSprite()
        {
            const int size = 256;
            const float edgeFalloffPx = 4f; // pixels of alpha gradient at the rim
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float cx = size / 2f;
            float baseRadius = size / 2f - edgeFalloffPx;

            float noiseOffset1 = Random.Range(0f, 100f);
            float noiseOffset2 = Random.Range(0f, 100f);
            float noiseOffset3 = Random.Range(0f, 100f);

            // Top band must be FULLY OPAQUE so the mask cuts the ground sprite cleanly along
            // the ground line. Previous implementation only cleared the very top row, but the
            // smoothstep falloff left semi-transparent pixels just below — those got rejected by
            // the SpriteMask alphaCutoff and produced a thin visible "line" of ground at the rim.
            const int topOpaqueBand = 6; // pixels at the top kept fully white = mask cuts cleanly

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - size;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    // 3-octave Perlin: large rolling shape + medium variation + fine detail
                    float n1 = Mathf.PerlinNoise(angle * 1.5f + noiseOffset1, 0.5f);
                    float n2 = Mathf.PerlinNoise(angle * 4f + noiseOffset2, 1.5f) * 0.5f;
                    float n3 = Mathf.PerlinNoise(angle * 9f + noiseOffset3, 2.5f) * 0.25f;
                    float noise = (n1 + n2 + n3) / 1.75f;

                    float jaggedRadius = baseRadius * (0.82f + noise * 0.36f);

                    // Smoothstep alpha — fade from inside (1) to outside (0) over edgeFalloffPx
                    float t = Mathf.Clamp01((jaggedRadius - dist) / edgeFalloffPx + 0.5f);
                    float alpha = t * t * (3f - 2f * t);

                    // Force the top band fully opaque inside the half-circle so the mask cuts
                    // the ground sprite all the way to the ground line — no leftover sliver.
                    if (y >= size - topOpaqueBand && Mathf.Abs(dx) < jaggedRadius)
                        alpha = 1f;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear; // smooth scaling — no pixel jaggies
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 1f), size);
        }

    }
}
