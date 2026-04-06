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
        private static Material _particleMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_solidSprite != null)
            {
                Object.Destroy(_solidSprite.texture);
                Object.Destroy(_solidSprite);
            }
            _solidSprite = null;

            if (_particleMaterial != null)
                Object.Destroy(_particleMaterial);
            _particleMaterial = null;
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
    }
}
