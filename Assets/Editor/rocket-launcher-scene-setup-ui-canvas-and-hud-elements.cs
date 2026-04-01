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
