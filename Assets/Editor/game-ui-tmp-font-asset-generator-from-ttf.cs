using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace RocketLauncher.Editor
{
    /// <summary>
    /// One-shot Editor tool: generates TMP_FontAsset (.asset) files from raw .ttf files in
    /// Assets/Fonts/. Tools → Rocket Launcher → Generate Game-Style Fonts.
    ///
    /// TMP can't render .ttf directly — it needs a signed-distance-field font atlas baked as a
    /// ScriptableObject .asset. This script imports the two project fonts and saves their atlas
    /// assets so the scene-setup tool can wire them onto HUD labels.
    ///
    /// Run ONCE after pulling the .ttf files. Re-running rebuilds the atlases (safe to redo if
    /// the .ttf files change). Output assets live next to the source .ttf in Assets/Fonts/.
    /// </summary>
    public static class GameUiTmpFontAssetGeneratorFromTtf
    {
        private const string FontsFolder = "Assets/Fonts";
        // Atlas size — 512² is plenty for ASCII + extended Latin glyphs at SDF padding 5.
        private const int AtlasSize = 512;
        private const int AtlasPadding = 5;

        [MenuItem("Tools/Rocket Launcher/Generate Game-Style Fonts")]
        public static void Generate()
        {
            if (!Directory.Exists(FontsFolder))
            {
                Debug.LogError("[FontGen] Assets/Fonts not found. Place .ttf files there first.");
                return;
            }

            string[] ttfFiles = Directory.GetFiles(FontsFolder, "*.ttf");
            if (ttfFiles.Length == 0)
            {
                Debug.LogError("[FontGen] No .ttf files in Assets/Fonts. Aborting.");
                return;
            }

            int built = 0;
            foreach (var ttfPath in ttfFiles)
            {
                if (TryBuildAtlasForTtf(ttfPath)) built++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FontGen] Built {built} TMP_FontAsset(s) from {ttfFiles.Length} .ttf source(s). " +
                      $"Run Tools → Rocket Launcher → Setup Scene next so HUD picks them up.");
        }

        private static bool TryBuildAtlasForTtf(string ttfPath)
        {
            string normalizedPath = ttfPath.Replace('\\', '/');
            var fontSource = AssetDatabase.LoadAssetAtPath<Font>(normalizedPath);
            if (fontSource == null)
            {
                Debug.LogWarning($"[FontGen] Skipped (couldn't load as Font): {normalizedPath}");
                return false;
            }

            string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
            string atlasPath = $"{FontsFolder}/{fileName}-SDF.asset";

            // Build font asset with Dynamic atlas so missing glyphs are rasterized on demand.
            // BUT: the atlas Texture2D + font material must be serialized as SUB-ASSETS of the
            // .asset file, otherwise gluing the font to a TMP component throws ArgumentNullException
            // (Parameter 'source') because TMP's renderer can't find the atlas texture.
            var asset = TMP_FontAsset.CreateFontAsset(
                fontSource,
                samplingPointSize: 90,
                atlasPadding: AtlasPadding,
                renderMode: UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                atlasWidth: AtlasSize,
                atlasHeight: AtlasSize,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (asset == null)
            {
                Debug.LogError($"[FontGen] CreateFontAsset returned null for {normalizedPath}");
                return false;
            }

            // Pre-warm the atlas with ASCII so the texture is non-null by the time we save.
            // Without this, atlasTextures[0] is created but never given dimensions, and any TMP
            // label assigned this font logs "Font Atlas Texture ... missing" + ArgumentNullException.
            asset.TryAddCharacters("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?:·°-+/'\"()");

            // Overwrite any existing atlas so re-runs don't pile up stale copies.
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(atlasPath);
            if (existing != null) AssetDatabase.DeleteAsset(atlasPath);

            AssetDatabase.CreateAsset(asset, atlasPath);

            // Add the atlas texture + material as SUB-ASSETS so they get serialized into the
            // same .asset file. Without this, the texture lives only in memory and dies when
            // Unity recompiles or reopens the project.
            if (asset.atlasTextures != null)
            {
                foreach (var tex in asset.atlasTextures)
                {
                    if (tex != null && !AssetDatabase.IsSubAsset(tex))
                        AssetDatabase.AddObjectToAsset(tex, asset);
                }
            }
            if (asset.material != null && !AssetDatabase.IsSubAsset(asset.material))
                AssetDatabase.AddObjectToAsset(asset.material, asset);

            EditorUtility.SetDirty(asset);
            Debug.Log($"[FontGen] Created {atlasPath} with {(asset.atlasTextures?.Length ?? 0)} atlas texture(s)");
            return true;
        }
    }
}
