using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool that auto-creates the entire Rocket Launcher game scene.
/// Usage: Tools > Rocket Launcher > Setup Scene
/// Split across partial class files:
///   rocket-launcher-scene-setup-environment-and-gameplay-objects.cs
///   rocket-launcher-scene-setup-ui-canvas-and-hud-elements.cs
///   rocket-launcher-scene-setup-shared-gameobject-and-sprite-helpers.cs
/// </summary>
public partial class SceneSetupTool
{
    [MenuItem("Tools/Rocket Launcher/Setup Scene")]
    public static void SetupScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Setup Rocket Launcher Scene",
            "Clear the current scene and rebuild all GameObjects?\n\nThis cannot be undone.",
            "Setup Scene", "Cancel"))
            return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Rocket Launcher Scene");

        // Pre-generate sprites BEFORE any scene work — avoids AssetDatabase conflicts
        PreGenerateSprites();

        ClearScene();
        SetupTags();
        SetupSortingLayers();
        SetupLayer(6, "Rocket");

        SetupCamera();

        // Section separators — establish hierarchy order in Scene view
        var managers  = CreateEmpty("--- MANAGERS ---");
        var envParent = CreateEmpty("--- ENVIRONMENT ---");
        var gameplay  = CreateEmpty("--- GAMEPLAY ---");
        var inputSep  = CreateEmpty("--- INPUT ---");
        var uiSep     = CreateEmpty("--- UI ---");

        CreateEmpty("GameManager", managers);
        CreateEnvironment(envParent);
        CreateGameplay(gameplay);
        CreateCanvas(uiSep);

        // Wire LaunchController with references
        WireLaunchController(inputSep);

        // Wire CameraController with references (must happen after gameplay objects exist)
        WireCameraController();

        // Add EventSystem for button input
        var eventSystem = CreateEmpty("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[SceneSetupTool] Scene setup complete. Press Ctrl+S to save.");
    }

    /// <summary>Batch-mode entry point — skips confirmation dialog, saves scene automatically.</summary>
    public static void SetupSceneBatchMode()
    {
        // Import TMP Essential Resources if not already present
        string tmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        if (!System.IO.File.Exists(tmpSettingsPath))
        {
            var importMethod = typeof(TMPro.TMP_PackageUtilities).GetMethod(
                "ImportProjectResourcesMenu", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            importMethod?.Invoke(null, null);
        }

        // Pre-generate sprites BEFORE any scene work — avoids AssetDatabase conflicts
        PreGenerateSprites();

        ClearScene();
        SetupTags();
        SetupSortingLayers();
        SetupLayer(6, "Rocket");
        SetupCamera();

        var managers  = CreateEmpty("--- MANAGERS ---");
        var envParent = CreateEmpty("--- ENVIRONMENT ---");
        var gameplay  = CreateEmpty("--- GAMEPLAY ---");
        var inputSep  = CreateEmpty("--- INPUT ---");
        var uiSep     = CreateEmpty("--- UI ---");

        CreateEmpty("GameManager", managers);
        CreateEnvironment(envParent);
        CreateGameplay(gameplay);
        CreateCanvas(uiSep);
        WireLaunchController(inputSep);
        WireCameraController();

        var eventSystem = CreateEmpty("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Save scene as GameScene
        var scene = SceneManager.GetActiveScene();
        string scenePath = "Assets/Scenes/GameScene.unity";
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[SceneSetupTool] Batch setup complete. Scene saved to " + scenePath);
    }

    // ── Launch controller wiring ────────────────────────────────────────────

    private static void WireLaunchController(GameObject parent)
    {
        var go = CreateEmpty("LaunchController", parent);
        var lc = go.AddComponent<LaunchController>();

        // Add ObstacleSpawner on same GO
        var os = go.AddComponent<ObstacleSpawner>();

        // Find scene references
        var rocket = GameObject.Find("Rocket");
        var aimArrow = GameObject.Find("AimArrow");
        var vehicle = GameObject.Find("LauncherVehicle");
        var spawnPoint = vehicle?.transform.Find("RocketSpawnPoint");

        // Wire via SerializedObject to set private [SerializeField] fields
        var so = new SerializedObject(lc);
        if (rocket != null)
            so.FindProperty("_rocket").objectReferenceValue = rocket.GetComponent<Rocket>();
        if (aimArrow != null)
            so.FindProperty("_aimArrow").objectReferenceValue = aimArrow.GetComponent<AimArrow>();
        if (spawnPoint != null)
            so.FindProperty("_spawnPoint").objectReferenceValue = spawnPoint;
        if (vehicle != null)
            so.FindProperty("_vehicleCollider").objectReferenceValue = vehicle.GetComponent<Collider2D>();

        var camGo = GameObject.Find("Main Camera");
        if (camGo != null)
            so.FindProperty("_cameraController").objectReferenceValue = camGo.GetComponent<CameraController>();

        // Wire Target reference
        var target = GameObject.Find("Target");
        if (target != null)
            so.FindProperty("_targetTransform").objectReferenceValue = target.transform;

        // Wire UI references (inactive GOs — use transform.Find)
        var canvas = GameObject.Find("Canvas");
        if (canvas != null)
        {
            var winTextTransform = canvas.transform.Find("WinText");
            if (winTextTransform != null)
                so.FindProperty("_winText").objectReferenceValue = winTextTransform.GetComponent<TMPro.TextMeshProUGUI>();

            var restartTransform = canvas.transform.Find("RestartButton");
            if (restartTransform != null)
                so.FindProperty("_restartButton").objectReferenceValue = restartTransform.GetComponent<UnityEngine.UI.Button>();

            var autoPlayTransform = canvas.transform.Find("AutoPlayButton");
            if (autoPlayTransform != null)
                so.FindProperty("_autoPlayButton").objectReferenceValue = autoPlayTransform.GetComponent<UnityEngine.UI.Button>();

            var lookTargetTransform = canvas.transform.Find("LookTargetButton");
            if (lookTargetTransform != null)
                so.FindProperty("_lookTargetButton").objectReferenceValue = lookTargetTransform.GetComponent<UnityEngine.UI.Button>();

            var angleTextTransform = canvas.transform.Find("AngleText");
            if (angleTextTransform != null)
                so.FindProperty("_angleText").objectReferenceValue = angleTextTransform.GetComponent<TMPro.TextMeshProUGUI>();

            var forceTextTransform = canvas.transform.Find("ForceText");
            if (forceTextTransform != null)
                so.FindProperty("_forceText").objectReferenceValue = forceTextTransform.GetComponent<TMPro.TextMeshProUGUI>();

            var roundShotsTransform = canvas.transform.Find("RoundShotsText");
            if (roundShotsTransform != null)
                so.FindProperty("_roundShotsText").objectReferenceValue = roundShotsTransform.GetComponent<TMPro.TextMeshProUGUI>();

            var totalShotsTransform = canvas.transform.Find("TotalShotsText");
            if (totalShotsTransform != null)
                so.FindProperty("_totalShotsText").objectReferenceValue = totalShotsTransform.GetComponent<TMPro.TextMeshProUGUI>();

            var roundNumberTransform = canvas.transform.Find("RoundNumberText");
            if (roundNumberTransform != null)
                so.FindProperty("_roundNumberText").objectReferenceValue = roundNumberTransform.GetComponent<TMPro.TextMeshProUGUI>();

            var bestScoreTransform = canvas.transform.Find("BestScoreText");
            if (bestScoreTransform != null)
                so.FindProperty("_bestScoreText").objectReferenceValue = bestScoreTransform.GetComponent<TMPro.TextMeshProUGUI>();
        }

        // Wire ObstacleSpawner
        so.FindProperty("_obstacleSpawner").objectReferenceValue = os;
        so.ApplyModifiedProperties();

        // Wire ObstacleSpawner references
        var osSo = new SerializedObject(os);
        if (spawnPoint != null)
            osSo.FindProperty("_spawnPoint").objectReferenceValue = spawnPoint;
        if (target != null)
            osSo.FindProperty("_targetTransform").objectReferenceValue = target.transform;
        osSo.ApplyModifiedProperties();

        Debug.Log("[SceneSetupTool] LaunchController + ObstacleSpawner wired.");
    }

    private static void WireCameraController()
    {
        var camGo = GameObject.Find("Main Camera");
        if (camGo == null) return;

        var cc = camGo.GetComponent<CameraController>();
        if (cc == null) return;

        var rocket = GameObject.Find("Rocket");
        var vehicle = GameObject.Find("LauncherVehicle");
        var target = GameObject.Find("Target");

        var so = new SerializedObject(cc);
        if (rocket != null)
            so.FindProperty("_rocket").objectReferenceValue = rocket.GetComponent<Rocket>();
        if (vehicle != null)
            so.FindProperty("_vehicleTransform").objectReferenceValue = vehicle.transform;
        if (target != null)
            so.FindProperty("_targetTransform").objectReferenceValue = target.transform;
        so.ApplyModifiedProperties();

        Debug.Log("[SceneSetupTool] CameraController wired — rocket, vehicle, target.");
    }

    // ── Scene clearing ───────────────────────────────────────────────────────

    private static void ClearScene()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            Undo.DestroyObjectImmediate(go);
    }

    // ── Project settings — Tags, Sorting Layers, Physics Layers ─────────────

    private static void SetupTags()
    {
        EnsureTag("Ground");
        EnsureTag("Target");
        // "Player" tag exists in Unity by default
    }

    private static void EnsureTag(string tagName)
    {
        var tm   = GetTagManager();
        var tags = tm.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tagName) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tagName;
        tm.ApplyModifiedProperties();
    }

    private static void SetupSortingLayers()
    {
        // Default layer always exists; append: Background, Environment, Gameplay, Projectile
        var tm     = GetTagManager();
        var layers = tm.FindProperty("m_SortingLayers");
        foreach (string name in new[] { "Background", "Environment", "Gameplay", "Projectile" })
        {
            bool exists = false;
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name)
                { exists = true; break; }
            if (exists) continue;

            layers.InsertArrayElementAtIndex(layers.arraySize);
            var entry = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            entry.FindPropertyRelative("name").stringValue  = name;
            entry.FindPropertyRelative("uniqueID").intValue = name.GetHashCode();
        }
        tm.ApplyModifiedProperties();
    }

    private static void SetupLayer(int index, string layerName)
    {
        var tm = GetTagManager();
        tm.FindProperty("layers").GetArrayElementAtIndex(index).stringValue = layerName;
        tm.ApplyModifiedProperties();
    }

    private static SerializedObject GetTagManager() =>
        new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

    // ── Export current scene positions (for saving manual adjustments) ──────

    [MenuItem("Tools/Rocket Launcher/Log Scene Positions")]
    public static void LogScenePositions()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== SCENE POSITIONS (copy these values) ===\n");

        string[] names = { "Main Camera", "Ground", "Target", "LauncherVehicle", "Rocket", "AimArrow" };
        foreach (string name in names)
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            var t = go.transform;
            sb.AppendLine($"{name}: pos=({t.position.x:F2}, {t.position.y:F2}, {t.position.z:F2})  scale=({t.localScale.x:F2}, {t.localScale.y:F2}, {t.localScale.z:F2})");

            // Log children too
            foreach (Transform child in t)
                sb.AppendLine($"  {child.name}: localPos=({child.localPosition.x:F2}, {child.localPosition.y:F2}, {child.localPosition.z:F2})  scale=({child.localScale.x:F2}, {child.localScale.y:F2}, {child.localScale.z:F2})");
        }
        sb.AppendLine("\n=== END ===");
        Debug.Log(sb.ToString());
    }

    // ── Camera ───────────────────────────────────────────────────────────────

    private static void SetupCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        // Position uses CamY/CamOrthoSize — single source of truth in environment partial
        go.transform.position = new Vector3(0f, CamY, -10f);
        var cam = go.AddComponent<Camera>();
        cam.orthographic     = true;
        cam.orthographicSize = CamOrthoSize;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        ColorUtility.TryParseHtmlString("#87CEEB", out Color bg);
        cam.backgroundColor = bg;
        go.AddComponent<AudioListener>();
        go.AddComponent<CameraController>();
        Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
    }
}
