using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace RocketLauncher.Editor
{
    /// <summary>
    /// Partial class: creates the Canvas with HUD elements styled in the comic / arcade game look.
    /// All labels and buttons go through GameStyleUiBuilder so they share the same font + outline
    /// + press-feedback behaviour. Layout: stats top-left, hints bottom-left, action buttons
    /// bottom-center, win/game-over labels + CONTINUE button centered.
    /// </summary>
    public partial class SceneSetupTool
    {
        // Vibrant gradient pairs — top color (lighter) → bottom color (deeper) for each button.
        private static readonly Color BtnContinueTop = HexToColor("#7DC74A");
        private static readonly Color BtnContinueBot = HexToColor("#2D5016");
        private static readonly Color BtnLookTop = HexToColor("#6E8AA8");
        private static readonly Color BtnLookBot = HexToColor("#2C3E50");
        private static readonly Color BtnAutoTop = HexToColor("#4FA8D8");
        private static readonly Color BtnAutoBot = HexToColor("#1A5276");

        private static void CreateCanvas(GameObject uiParent)
        {
            var canvasGO = CreateEmpty("Canvas", uiParent);
            var canvas   = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // Headlines: Bangers comic-book font, big chunky outline.
            CreateHeadlineLabel(canvasGO, "WinText",  "YOU WIN!",  HexToColor("#FFD700"), new Vector2(0, 200), new Vector2(700, 160));
            CreateHeadlineLabel(canvasGO, "GameOverText", "GAME OVER", HexToColor("#FF3030"), new Vector2(0, 200), new Vector2(700, 160));
            CreateRestartButton(canvasGO);

            CreateBottomButtons(canvasGO);

            CreateHintLabel(canvasGO, "AngleText", "Angle: 0°", new Vector2(30, 80));
            CreateHintLabel(canvasGO, "ForceText", "Force: 0",  new Vector2(30, 30));

            CreateStatsLabel(canvasGO);
        }

        /// <summary>Single compact stats line: "Round 1 . Shots 0 . Best --" — Lilita One body.</summary>
        private static void CreateStatsLabel(GameObject parent)
        {
            var go = CreateUIElement("StatsText", parent);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot     = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(30, -30);
            rect.sizeDelta        = new Vector2(600, 50);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = "Round 1  ·  Shots 0  ·  Best --";
            tmp.fontSize  = 32;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color     = Color.white;
            GameStyleUiBuilder.StyleAsGameText(tmp, useHeadlineFont: false, outlineWidth: 0.2f);
        }

        /// <summary>Bottom hint labels — Lilita One body with subtle outline.</summary>
        private static void CreateHintLabel(GameObject parent, string name, string defaultText,
            Vector2 anchoredPos)
        {
            var go = CreateUIElement(name, parent);
            go.SetActive(false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot     = new Vector2(0f, 0f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta        = new Vector2(300, 50);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = defaultText;
            tmp.fontSize  = 36;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color     = Color.white;
            GameStyleUiBuilder.StyleAsGameText(tmp, useHeadlineFont: false, outlineWidth: 0.2f);
        }

        /// <summary>VIEW TARGET + AUTO PLAY buttons at bottom-center.</summary>
        private static void CreateBottomButtons(GameObject parent)
        {
            var lookGo = GameStyleUiBuilder.BuildGameStyleButton(parent, "LookTargetButton", "VIEW TARGET",
                new Vector2(260, 70), BtnLookTop, BtnLookBot, fontSize: 30);
            AnchorBottomCenter(lookGo, new Vector2(-145, 35));

            var autoGo = GameStyleUiBuilder.BuildGameStyleButton(parent, "AutoPlayButton", "AUTO PLAY",
                new Vector2(260, 70), BtnAutoTop, BtnAutoBot, fontSize: 30);
            AnchorBottomCenter(autoGo, new Vector2(145, 35));
            autoGo.SetActive(false);
        }

        private static void AnchorBottomCenter(GameObject go, Vector2 anchoredPos)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot     = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPos;
        }

        /// <summary>Headline labels (WIN / GAME OVER) — Bangers font, big chunky outline.</summary>
        private static void CreateHeadlineLabel(GameObject parent, string name, string text,
            Color textColor, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = CreateUIElement(name, parent);
            go.SetActive(false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin        = new Vector2(0.5f, 0.5f);
            rect.anchorMax        = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta        = sizeDelta;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 96;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = textColor;
            // Headline outline thick + dark for the comic-book punch.
            GameStyleUiBuilder.StyleAsGameText(tmp, useHeadlineFont: true, outlineWidth: 0.4f, outlineColor: Color.black);
        }

        /// <summary>CONTINUE button at screen center — green gradient, game-style.</summary>
        private static void CreateRestartButton(GameObject parent)
        {
            var go = GameStyleUiBuilder.BuildGameStyleButton(parent, "RestartButton", "CONTINUE",
                new Vector2(320, 90), BtnContinueTop, BtnContinueBot, fontSize: 42);
            go.SetActive(false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin        = new Vector2(0.5f, 0.5f);
            rect.anchorMax        = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 50);
        }

        private static Color HexToColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
