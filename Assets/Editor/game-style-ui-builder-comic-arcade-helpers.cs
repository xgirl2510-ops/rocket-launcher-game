using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RocketLauncher.Editor
{
    /// <summary>
    /// Reusable Editor builders for the comic / arcade game-style HUD:
    ///   • <see cref="BuildOutlinedTmp"/> — TextMeshProUGUI with chunky outline + drop shadow,
    ///     using the project's custom font when available (Lilita One / Bangers).
    ///   • <see cref="BuildGameStyleButton"/> — gradient button background sprite + outlined label,
    ///     with a press-scale animation component attached.
    ///
    /// All helpers degrade gracefully when the SDF font assets haven't been generated yet — text
    /// still renders with the default TMP font, just without the custom glyph shapes. Run
    /// Tools → Rocket Launcher → Generate Game-Style Fonts first for the full look.
    /// </summary>
    public static class GameStyleUiBuilder
    {
        // Resolve the SDF font assets generated from Assets/Fonts/*.ttf. Re-loaded each call so
        // re-running Setup Scene picks up freshly-baked atlases without an editor restart.
        public static TMP_FontAsset BodyFont => LoadFont("Assets/Fonts/LilitaOne-Regular-SDF.asset");
        public static TMP_FontAsset HeadlineFont => LoadFont("Assets/Fonts/Bangers-Regular-SDF.asset");

        private static TMP_FontAsset LoadFont(string path)
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        /// <summary>
        /// Configure a TMP label with comic-style outline + drop shadow + custom font.
        /// </summary>
        /// <param name="useHeadlineFont">true for big titles (Bangers), false for body/buttons (Lilita One).</param>
        public static void StyleAsGameText(TextMeshProUGUI tmp, bool useHeadlineFont,
                                           float outlineWidth = 0.25f, Color? outlineColor = null)
        {
            var font = useHeadlineFont ? HeadlineFont : BodyFont;
            // Only swap font if the custom SDF asset exists AND has a baked atlas. Without the
            // atlas check, Unity throws ArgumentNullException(source) the moment we touch
            // fontMaterial below because the renderer can't find the atlas texture.
            if (font != null && font.material != null && HasBakedAtlas(font))
            {
                tmp.font = font;
            }
            else if (font != null)
            {
                Debug.LogWarning($"[GameStyleUI] Font '{font.name}' has no baked atlas — falling back to default TMP font. Re-run Tools → Rocket Launcher → Generate Game-Style Fonts.");
            }

            // Safety: bail out before touching fontMaterial if TMP hasn't initialized one yet
            // (only happens with broken/atlas-less font assets).
            if (tmp.fontMaterial == null) return;

            // FontMaterial outline + underlay (drop shadow). TMP exposes these via material properties.
            tmp.fontMaterial.SetFloat("_OutlineWidth", outlineWidth);
            tmp.fontMaterial.SetColor("_OutlineColor", outlineColor ?? Color.black);

            // Underlay = built-in drop shadow. Subtle offset + soft falloff.
            tmp.fontMaterial.SetFloat("_UnderlayOffsetX", 0.5f);
            tmp.fontMaterial.SetFloat("_UnderlayOffsetY", -0.5f);
            tmp.fontMaterial.SetFloat("_UnderlaySoftness", 0.3f);
            tmp.fontMaterial.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.5f));

            // ENABLE_UNDERLAY keyword must be on for the underlay to render.
            tmp.fontMaterial.EnableKeyword("UNDERLAY_ON");
        }

        /// <summary>True if the SDF atlas asset has at least one baked atlas texture with non-zero dimensions.</summary>
        private static bool HasBakedAtlas(TMP_FontAsset font)
        {
            if (font.atlasTextures == null || font.atlasTextures.Length == 0) return false;
            var tex = font.atlasTextures[0];
            return tex != null && tex.width > 0 && tex.height > 0;
        }

        /// <summary>
        /// Build a game-style button: rounded sprite background, two-tone gradient (lighter top,
        /// darker bottom), white outlined label, and an attached <c>ButtonPressScaleAnimator</c>
        /// so the button feels tactile when clicked.
        /// </summary>
        public static GameObject BuildGameStyleButton(GameObject parent, string name, string label,
                                                      Vector2 sizeDelta, Color topColor, Color bottomColor,
                                                      float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = sizeDelta;

            // Background: use the built-in rounded UISprite that ships with Unity for the soft pill
            // look. Outline + shadow are added as separate Image children rather than baked into the
            // sprite so the gradient stays editable.
            var img = go.AddComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = bottomColor;

            // Vertical-gradient highlight: a smaller Image child anchored to the top half blends the
            // top color over the base. Keeps the look gradient-y without requiring a custom shader.
            var grad = new GameObject("Gradient", typeof(RectTransform));
            grad.transform.SetParent(go.transform, false);
            var gradRect = (RectTransform)grad.transform;
            gradRect.anchorMin = new Vector2(0, 0.5f);
            gradRect.anchorMax = new Vector2(1, 1);
            gradRect.offsetMin = new Vector2(2, 0);
            gradRect.offsetMax = new Vector2(-2, -2);
            var gradImg = grad.AddComponent<Image>();
            gradImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            gradImg.type = Image.Type.Sliced;
            gradImg.color = topColor;
            gradImg.raycastTarget = false;

            go.AddComponent<Button>();
            go.AddComponent<RocketLauncher.ButtonPressScaleAnimator>();

            // Label
            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textGO.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            StyleAsGameText(tmp, useHeadlineFont: false, outlineWidth: 0.25f);

            return go;
        }
    }
}
