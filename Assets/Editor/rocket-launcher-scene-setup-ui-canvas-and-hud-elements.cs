using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Partial class: creates the Canvas with HUD elements.
/// Layout: stats top-left (1 line), hints bottom-left, buttons bottom-center-right,
/// win/continue centered.
/// </summary>
public partial class SceneSetupTool
{
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

        // Center overlays (hidden by default)
        CreateTMPLabel(canvasGO, "WinText",  "YOU WIN!", 72, "#FFD700", new Vector2(0, 200), new Vector2(700, 130));
        CreateRestartButton(canvasGO);

        // Bottom-center buttons
        CreateBottomButtons(canvasGO);

        // Hint labels (bottom-left, inactive until 5 misses)
        CreateHintLabel(canvasGO, "AngleText", "Angle: 0°", new Vector2(30, 80));
        CreateHintLabel(canvasGO, "ForceText", "Force: 0",  new Vector2(30, 30));

        // Compact stats line (top-left, always visible)
        CreateStatsLabel(canvasGO);
    }

    /// <summary>Single compact stats line: "Round 1 · Shots 0 · Best --"</summary>
    private static void CreateStatsLabel(GameObject parent)
    {
        var go = CreateUIElement("StatsText", parent);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f); // top-left
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot     = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(30, -30);
        rect.sizeDelta        = new Vector2(600, 50);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Round 1  ·  Shots 0  ·  Best --";
        tmp.fontSize  = 28;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color     = Color.white;
    }

    /// <summary>Bottom hint labels (anchored bottom-left).</summary>
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
        tmp.fontSize  = 32;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color     = Color.white;
    }

    /// <summary>VIEW TARGET + AUTO PLAY buttons side by side at bottom-center-right.</summary>
    private static void CreateBottomButtons(GameObject parent)
    {
        // VIEW TARGET — bottom, slightly left of center-right
        CreateBottomButton(parent, "LookTargetButton", "VIEW TARGET",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-140, 30), new Vector2(250, 60), "#2C3E50", 28);

        // AUTO PLAY — bottom, right of VIEW TARGET (hidden until 5 misses)
        var autoGo = CreateBottomButton(parent, "AutoPlayButton", "AUTO PLAY",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(140, 30), new Vector2(250, 60), "#1A5276", 28);
        autoGo.SetActive(false);
    }

    private static GameObject CreateBottomButton(GameObject parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, string hexColor, float fontSize)
    {
        var go = CreateUIElement(name, parent);
        var rect = (RectTransform)go.transform;
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.pivot            = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = sizeDelta;

        var img = go.AddComponent<Image>();
        ColorUtility.TryParseHtmlString(hexColor, out Color bgColor);
        img.color = bgColor;
        go.AddComponent<Button>();

        var textGO   = CreateUIElement("Text", go);
        var textRect = (RectTransform)textGO.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;

        return go;
    }

    /// <summary>Creates a centered TextMeshProUGUI label, inactive by default.</summary>
    private static void CreateTMPLabel(GameObject parent, string name, string text,
        float fontSize, string hexColor, Vector2 anchoredPos, Vector2 sizeDelta)
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
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        ColorUtility.TryParseHtmlString(hexColor, out Color color);
        tmp.color = color;
    }

    /// <summary>CONTINUE button — center, inactive by default.</summary>
    private static void CreateRestartButton(GameObject parent)
    {
        var go = CreateUIElement("RestartButton", parent);
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

        var textGO   = CreateUIElement("Text", go);
        var textRect = (RectTransform)textGO.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "CONTINUE";
        tmp.fontSize  = 40;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
    }
}
