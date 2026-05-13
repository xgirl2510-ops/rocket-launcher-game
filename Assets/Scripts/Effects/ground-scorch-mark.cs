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

            // Crater Y is the caller-provided impact Y (the ImpactEffectsHandler clamps this
            // to the ground sprite's actual top edge). Don't fall back to GameConstants.GroundTop
            // here — that constant is the PHYSICS collider top, which may differ from the visual
            // sprite top after the ground/BG layout was tuned to align with the car.
            float groundY = impactPosition.y;
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
            // Remove the old crater whenever the new crater's footprint reaches its center
            // OR the old crater's footprint reaches the new center — i.e. dx < max(halfW).
            // Previous formula (newHalfW + oldHalfW*0.5) left visible old rims when the new
            // crater was smaller; this max() form guarantees the new shot fully replaces any
            // crater it lands inside, including exact-same-spot repeats.
            float newHalfW = newCraterWidth * 0.4f;
            for (int i = _craters.Count - 1; i >= 0; i--)
            {
                float oldHalfW = _craters[i].Width * 0.4f;
                float dx = Mathf.Abs(impactX - _craters[i].X);
                if (dx < Mathf.Max(newHalfW, oldHalfW))
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

            int variantIdx = Random.Range(0, MaskVariantCount);
            Sprite blob = _maskVariants[variantIdx];

            // ===== AAA-style realistic bomb crater =====
            //
            // Reference: Worms / Battlefield / Scorched-Earth 2D craters typically use:
            //   • A pixel-perfect cut hole in the terrain (the SpriteMask below)
            //   • A soft DARK GRADIENT RING just outside the rim — alpha peaks AT the rim and
            //     fades outward (like an inner shadow). This is the "burnt zone".
            //   • A handful of SCATTERED SMUDGE BLOBS around the rim — soft dark spots at
            //     irregular positions that break up the perfect ellipse and look more organic.
            //   • TINY DEBRIS SPECKS scattered further out — small dark dots like soot fallout.
            //
            // No clean streak lines (those read as "cartoon"). The look is built entirely from
            // ellipse blobs at varied sizes/positions/opacities, layered to create soft gradient.

            // All scorch elements are placed BELOW the rim (local Y <= 0). The crater's
            // local origin sits at the ground top, so anything with positive Y would poke
            // above the dirt surface and look wrong.

            // --- 1. OUTER SOFT HALO --------------------------------------------------------
            // Big diffuse dark ellipse, anchored so its top edge sits at the ground line.
            // Offset down by half its height so only the lower half is visible.
            float haloW = craterW * 1.6f;
            float haloH = craterH * 1.5f;
            SpawnScorchBlob(parent.transform, blob,
                new Vector2(0f, -haloH * 0.5f),
                new Vector2(haloW, haloH),
                new Color(0.05f, 0.04f, 0.03f, 0.30f),
                sortingOrder: 11);

            // --- 2. INNER DARK RIM RING ----------------------------------------------------
            // Tighter, darker ellipse hugging the rim. Also pushed down so it never extends
            // above the ground surface.
            float rimW = craterW * 1.25f;
            float rimH = craterH * 1.18f;
            SpawnScorchBlob(parent.transform, blob,
                new Vector2(0f, -rimH * 0.5f),
                new Vector2(rimW, rimH),
                new Color(0.02f, 0.015f, 0.01f, 0.70f),
                sortingOrder: 12);

            // --- 3. SCATTERED SMUDGE BLOBS -------------------------------------------------
            // Angles restricted to the LOWER semicircle (180°..360° → sin < 0 → below rim).
            int smudgeCount = Random.Range(8, 13);
            float rimRX = craterW * 0.55f;
            float rimRY = craterH * 0.55f;
            for (int i = 0; i < smudgeCount; i++)
            {
                float ang = Random.Range(Mathf.PI, Mathf.PI * 2f);
                float radialOffset = Random.Range(0f, 0.4f);
                Vector2 pos = new Vector2(
                    (rimRX + craterW * 0.5f * radialOffset) * Mathf.Cos(ang),
                    (rimRY + craterH * 0.5f * radialOffset) * Mathf.Sin(ang));
                float blobSize = craterW * Random.Range(0.22f, 0.45f);
                float aspectStretch = Random.Range(0.7f, 1.3f);
                Vector2 blobScale = new Vector2(blobSize * aspectStretch, blobSize / aspectStretch);
                Color smudgeColor = new Color(0.02f, 0.015f, 0.01f, Random.Range(0.45f, 0.75f));
                SpawnScorchBlob(parent.transform, blob, pos, blobScale, smudgeColor, sortingOrder: 13);
            }

            // --- 4. BURNT DEBRIS SPECKS ----------------------------------------------------
            // Angles also restricted to the lower semicircle so soot only scatters into dirt.
            int speckCount = Random.Range(18, 28);
            float speckRadiusMin = craterW * 0.55f;
            float speckRadiusMax = craterW * 0.95f;
            for (int i = 0; i < speckCount; i++)
            {
                float ang = Random.Range(Mathf.PI, Mathf.PI * 2f);
                float r = Random.Range(speckRadiusMin, speckRadiusMax);
                Vector2 pos = new Vector2(r * Mathf.Cos(ang), r * Mathf.Sin(ang) * 0.85f);
                float speckSize = craterW * Random.Range(0.04f, 0.10f);
                Color speckColor = new Color(0.02f, 0.01f, 0.01f, Random.Range(0.65f, 1.0f));
                SpawnScorchBlob(parent.transform, blob, pos,
                    new Vector2(speckSize, speckSize), speckColor, sortingOrder: 14);
            }

            // --- 5. THE HOLE ITSELF (SpriteMask cuts terrain) -------------------------------
            var maskGo = new GameObject("CraterMask");
            maskGo.transform.SetParent(parent.transform, false);
            float maskW = craterW * 1.15f;
            float maskH = craterH * 1.1f;
            maskGo.transform.localScale = new Vector3(maskW, maskH, 1f);
            var mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = blob;
            mask.alphaCutoff = 0.5f;

            return parent;
        }

        /// <summary>
        /// Spawn one ellipse blob sprite as part of the scorch composition.
        /// Position is LOCAL to the crater parent. Scale is in world units.
        /// </summary>
        private static void SpawnScorchBlob(Transform craterParent, Sprite sprite,
                                            Vector2 localPos, Vector2 worldScale,
                                            Color color, int sortingOrder)
        {
            var go = new GameObject("ScorchBlob");
            go.transform.SetParent(craterParent, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            go.transform.localScale = new Vector3(worldScale.x, worldScale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingLayerName = "Environment";
            sr.sortingOrder = sortingOrder;
            sr.maskInteraction = SpriteMaskInteraction.None;
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
