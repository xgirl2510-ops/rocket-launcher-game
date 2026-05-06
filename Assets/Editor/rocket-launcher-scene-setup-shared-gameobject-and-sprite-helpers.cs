using UnityEngine;
using UnityEditor;
using RocketLauncher;

namespace RocketLauncher.Editor
{
    /// <summary>
    /// Shared factory helpers for SceneSetupTool.
    /// Creates Texture2D + Sprite assets directly via AssetDatabase.CreateAsset().
    /// No PNG import, no TextureImporter — avoids all import pipeline issues.
    /// </summary>
    public partial class SceneSetupTool
    {
        private static Sprite _cachedSquare;
        private static Sprite _cachedCircle;

        /// <summary>
        /// Creates Texture2D assets and Sprite sub-assets. Must be called BEFORE scene creation.
        /// </summary>
        private static void PreGenerateSprites()
        {
            _cachedSquare = CreateOrLoadSpriteAsset("square-100x100", false);
            _cachedCircle = CreateOrLoadSpriteAsset("circle-100x100", true);

            Debug.Log($"[SceneSetupTool] Sprites ready — square: {_cachedSquare != null}, circle: {_cachedCircle != null}");
        }

        private static Sprite CreateOrLoadSpriteAsset(string name, bool isCircle)
        {
            string dir = "Assets/Sprites/Generated";
            string assetPath = $"{dir}/{name}.asset";

            if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
                AssetDatabase.CreateFolder("Assets", "Sprites");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Sprites", "Generated");

            AssetDatabase.DeleteAsset(assetPath);

            int size = 100;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.name = name;

            if (isCircle)
            {
                float c = (size - 1) / 2f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - c, dy = y - c;
                        tex.SetPixel(x, y, Mathf.Sqrt(dx * dx + dy * dy) <= c ? Color.white : Color.clear);
                    }
            }
            else
            {
                var px = new Color[size * size];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                tex.SetPixels(px);
            }
            tex.Apply();

            AssetDatabase.CreateAsset(tex, assetPath);

            var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            var sprite = Sprite.Create(savedTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name + "_sprite";
            AssetDatabase.AddObjectToAsset(sprite, assetPath);
            AssetDatabase.SaveAssets();

            var loaded = LoadSpriteFromAsset(assetPath);
            return loaded;
        }

        private static Sprite LoadSpriteFromAsset(string assetPath)
        {
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in allAssets)
            {
                if (asset is Sprite s) return s;
            }
            Debug.LogError($"[SceneSetupTool] No Sprite found in {assetPath}");
            return null;
        }

        private static GameObject CreateEmpty(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static GameObject CreateUIElement(string name, GameObject parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static GameObject CreateSprite(string name, GameObject parent, Vector3 localPos,
            Vector3 localScale, Color color, string sortingLayer, string spriteType = "Square")
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent.transform, false);
                go.transform.localPosition = localPos;
            }
            else
            {
                go.transform.position = localPos;
            }
            go.transform.localScale = localScale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSprite(spriteType);
            sr.color = color;
            sr.sortingLayerName = sortingLayer;

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static Sprite GetSprite(string type)
        {
            bool isCircle = type == "Circle";
            var sprite = isCircle ? _cachedCircle : _cachedSquare;
            if (sprite == null)
                Debug.LogError($"[SceneSetupTool] Sprite '{type}' is null! Was PreGenerateSprites() called?");
            return sprite;
        }

        /// <summary>Load a PNG sprite from Assets, force-ensuring Sprite import settings.</summary>
        private static Sprite LoadSpriteFromPng(string assetPath, int maxSize = 4096)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = maxSize;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
                Debug.LogError($"[SceneSetupTool] Sprite not found at {assetPath}");
            return sprite;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
