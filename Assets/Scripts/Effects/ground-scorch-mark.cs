using System.Collections.Generic;
using UnityEngine;

namespace RocketLauncher
{
    /// <summary>
    /// Static utility: creates real crater holes in the ground at rocket impact points.
    /// Two layers per crater:
    ///   1. SpriteMask — cuts the ground sprite (VisibleOutsideMask)
    ///   2. Dark interior — behind ground, visible through the hole (shows depth)
    /// Also spawns dirt debris via RocketDebris.SpawnDirtDebris().
    /// Persists until ClearAll() is called (round restart).
    /// </summary>
    public static class GroundScorch
    {
        private static readonly List<GameObject> _allCraters = new List<GameObject>();
        private static readonly List<float> _craterXPositions = new List<float>();
        private static readonly List<float> _craterWidths = new List<float>();
        private static readonly List<float> _craterDepths = new List<float>();
        private static bool _groundPrepared;

        private const int MaskVariantCount = 8;
        private static Sprite[] _maskVariants;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _allCraters.Clear();
            _craterXPositions.Clear();
            _craterWidths.Clear();
            _craterDepths.Clear();
            DestroyMaskVariants();
            _groundPrepared = false;
        }

        /// <summary>Destroy cached mask variant textures/sprites to prevent GPU memory leak.</summary>
        private static void DestroyMaskVariants()
        {
            if (_maskVariants == null) return;
            for (int i = 0; i < _maskVariants.Length; i++)
            {
                if (_maskVariants[i] != null)
                {
                    Object.Destroy(_maskVariants[i].texture);
                    Object.Destroy(_maskVariants[i]);
                }
            }
            _maskVariants = null;
        }

        /// <summary>Find ground and enable mask interaction so SpriteMasks cut it.</summary>
        private static void PrepareGround()
        {
            if (_groundPrepared) return;
            var ground = GameObject.FindWithTag(GameConstants.TagGround);
            if (ground == null) return;
            var sr = ground.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            _groundPrepared = true;
        }

        /// <summary>Spawn a real crater hole in the ground. Size scales with max flight height.</summary>
        public static void Spawn(Vector2 impactPosition, float maxHeight = 10f)
        {
            PrepareGround();
            EnsureMaskVariants();

            float groundY = GameConstants.GroundTop;

            float heightAboveGround = Mathf.Max(0f, maxHeight - groundY);
            float scale;
            if (heightAboveGround <= 15f)
                scale = Random.Range(0.8f, 1.2f);
            else if (heightAboveGround <= 30f)
                scale = Random.Range(1.2f, 1.8f);
            else
                scale = Random.Range(1.8f, 2.5f);

            float craterW = Random.Range(1.2f, 1.6f) * scale;
            float craterH = Random.Range(0.8f, 1.2f) * scale;

            var parent = new GameObject("Crater");
            parent.transform.position = new Vector3(impactPosition.x, groundY, 0f);

            // Dark hole interior — sits behind ground, visible through the mask hole
            var holeGo = new GameObject("HoleInterior");
            holeGo.transform.SetParent(parent.transform, false);
            holeGo.transform.localPosition = new Vector3(0f, -craterH, 0f);
            holeGo.transform.localScale = new Vector3(craterW * 3f, craterH * 3f, 1f);
            var holeSr = holeGo.AddComponent<SpriteRenderer>();
            holeSr.sprite = RuntimeSpriteFactory.GetSolidSprite();
            holeSr.color = new Color(0.2f, 0.14f, 0.06f, 1f);
            holeSr.sortingLayerName = "Environment";
            holeSr.sortingOrder = -1;
            holeSr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            // SpriteMask — cuts a jagged hole in the ground
            var maskGo = new GameObject("CraterMask");
            maskGo.transform.SetParent(parent.transform, false);
            maskGo.transform.localScale = new Vector3(craterW * 1.15f, craterH * 1.1f, 1f);
            var mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = _maskVariants[Random.Range(0, MaskVariantCount)];
            mask.alphaCutoff = 0.5f;

            RocketDebris.SpawnDirtDebris(impactPosition, scale);

            _craterXPositions.Add(impactPosition.x);
            _craterWidths.Add(craterW);
            _craterDepths.Add(craterH);
            _allCraters.Add(parent);
        }

        /// <summary>
        /// Returns ground Y at given X, accounting for craters.
        /// Debris uses this to fall into crater holes instead of stopping at surface.
        /// </summary>
        public static float GetGroundY(float x)
        {
            float baseY = GameConstants.GroundTop;
            for (int i = 0; i < _craterXPositions.Count; i++)
            {
                float halfW = _craterWidths[i] * 0.4f;
                float dx = Mathf.Abs(x - _craterXPositions[i]);
                if (dx >= halfW) continue;

                float t = 1f - (dx / halfW);
                float depth = _craterDepths[i] * t * 0.7f;
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
            _craterXPositions.Clear();
            _craterWidths.Clear();
            _craterDepths.Clear();
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
        /// Jagged semicircle for the SpriteMask. Perlin noise on radius for irregular edges.
        /// Pivot at top-center so it hangs downward from ground line.
        /// </summary>
        private static Sprite BuildMaskSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size);
            float cx = size / 2f;
            float baseRadius = size / 2f;

            float noiseOffset = Random.Range(0f, 100f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - size;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    float noise = Mathf.PerlinNoise(angle * 3f + noiseOffset, 0.5f);
                    float jaggedRadius = baseRadius * (0.8f + noise * 0.4f);

                    bool inside = dist < jaggedRadius && y < size - 1;
                    tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 1f), size);
        }

    }
}
