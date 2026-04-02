using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Partial class: creates the Canvas with WinText, MissText, and RestartButton HUD elements.
/// Requires TextMeshPro package. Part of the SceneSetupTool suite.
/// NOTE: Add an EventSystem to the scene (GameObject > UI > Event System) for button input.
/// </summary>
public partial class SceneSetupTool
{
    private static void CreateCanvas(GameObject uiParent)
    {
        var canvasGO = CreateEmpty("Canvas", uiParent);
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Scale UI for portrait mobile — spec: 1080x1920 reference, match 0.5
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        CreateTMPLabel(canvasGO, "WinText",  "YOU WIN!", 72, "#FFD700", new Vector2(0, 200), new Vector2(700, 130));
        CreateTMPLabel(canvasGO, "MissText", "MISS!",    60, "#FFFFFF", new Vector2(0, 200), new Vector2(600, 110));
        CreateRestartButton(canvasGO);
        CreateAutoPlayButton(canvasGO);

        CreateLookTargetButton(canvasGO);

        // Hint labels (bottom-left, inactive until 5 misses)
        CreateHintLabel(canvasGO, "AngleText", "Góc: 0°",  new Vector2(30, 80), false);
        CreateHintLabel(canvasGO, "ForceText", "Lực: 0",   new Vector2(30, 30), false);

        // Stats labels (top-right, always visible)
        CreateHintLabel(canvasGO, "RoundShotsText",  "Bắn: 0",      new Vector2(-30, -30),  true, TextAlignmentOptions.Right);
        CreateHintLabel(canvasGO, "TotalShotsText",  "Tổng: 0",     new Vector2(-30, -75),  true, TextAlignmentOptions.Right);
        CreateHintLabel(canvasGO, "RoundNumberText", "Ván: 1",      new Vector2(-30, -120), true, TextAlignmentOptions.Right);
        CreateHintLabel(canvasGO, "BestScoreText",   "Kỷ lục: --",  new Vector2(-30, -165), true, TextAlignmentOptions.Right);
    }

    /// <summary>Creates a HUD label. Anchored bottom-left (hints) or top-right (stats).</summary>
    private static void CreateHintLabel(GameObject parent, string name, string defaultText,
        Vector2 anchoredPos, bool activeByDefault,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var go = CreateUIElement(name, parent);
        if (!activeByDefault) go.SetActive(false);

        var rect = (RectTransform)go.transform;

        bool isTopRight = align == TextAlignmentOptions.Right;
        if (isTopRight)
        {
            rect.anchorMin = new Vector2(1f, 1f); // top-right
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot     = new Vector2(1f, 1f);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 0f); // bottom-left
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot     = new Vector2(0f, 0f);
        }

        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(300, 50);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = defaultText;
        tmp.fontSize  = 32;
        tmp.alignment = align;
        tmp.color     = Color.white;
    }

    private static void CreateAutoPlayButton(GameObject parent)
    {
        var go = CreateUIElement("AutoPlayButton", parent);

        var rect = (RectTransform)go.transform;
        rect.anchorMin        = new Vector2(1f, 0f); // bottom-right
        rect.anchorMax        = new Vector2(1f, 0f);
        rect.pivot            = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-30, 30);
        rect.sizeDelta        = new Vector2(250, 70);

        var img = go.AddComponent<Image>();
        ColorUtility.TryParseHtmlString("#1A5276", out Color bgColor);
        img.color = bgColor;
        go.AddComponent<Button>();

        var textGO   = CreateUIElement("Text", go);
        var textRect = (RectTransform)textGO.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "MÁY CHƠI";
        tmp.fontSize  = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
    }

    /// <summary>Creates the Look Target button (top-left, always visible).</summary>
    private static void CreateLookTargetButton(GameObject parent)
    {
        var go = CreateUIElement("LookTargetButton", parent);

        var rect = (RectTransform)go.transform;
        rect.anchorMin        = new Vector2(0f, 1f); // top-left
        rect.anchorMax        = new Vector2(0f, 1f);
        rect.pivot            = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(30, -30);
        rect.sizeDelta        = new Vector2(250, 70);

        var img = go.AddComponent<Image>();
        ColorUtility.TryParseHtmlString("#2C3E50", out Color bgColor);
        img.color = bgColor;
        go.AddComponent<Button>();

        var textGO   = CreateUIElement("Text", go);
        var textRect = (RectTransform)textGO.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "XEM MỤC TIÊU";
        tmp.fontSize  = 30;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
    }

    /// <summary>Creates a centered TextMeshProUGUI label, inactive by default.</summary>
    private static void CreateTMPLabel(GameObject parent, string name, string text,
        float fontSize, string hexColor, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go   = CreateUIElement(name, parent);
        go.SetActive(false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = sizeDelta;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        ColorUtility.TryParseHtmlString(hexColor, out Color color);
        tmp.color = color;
    }

    /// <summary>Creates the restart button with dark-green background, inactive by default.</summary>
    private static void CreateRestartButton(GameObject parent)
    {
        var go   = CreateUIElement("RestartButton", parent);
        go.SetActive(false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, 50);
        rect.sizeDelta        = new Vector2(300, 80);

        var img = go.AddComponent<Image>();
        ColorUtility.TryParseHtmlString("#2D5016", out Color bgColor);
        img.color = bgColor;
        go.AddComponent<Button>();
        // OnClick → GameManager.Instance.ResetRound() — wire up in Inspector or via GameManager.cs

        // Label text child
        var textGO   = CreateUIElement("Text", go);
        var textRect = (RectTransform)textGO.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "RESTART";
        tmp.fontSize  = 40;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
    }
}
