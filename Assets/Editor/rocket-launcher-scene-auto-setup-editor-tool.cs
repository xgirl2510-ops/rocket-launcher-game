using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using RocketLauncher;

namespace RocketLauncher.Editor
{
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

            RunCoreSetup();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[SceneSetupTool] Scene setup complete. Press Ctrl+S to save.");
        }

        /// <summary>Batch-mode entry point — skips confirmation dialog, saves scene automatically.</summary>
        public static void SetupSceneBatchMode()
        {
            string tmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (!System.IO.File.Exists(tmpSettingsPath))
            {
                // TMP_PackageUtilities was removed in Unity 6 — use reflection to stay compatible
                var utilType = System.Type.GetType("TMPro.TMP_PackageUtilities, Unity.TextMeshPro.Editor");
                if (utilType != null)
                {
                    var importMethod = utilType.GetMethod(
                        "ImportProjectResourcesMenu", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                    importMethod?.Invoke(null, null);
                }
                else
                {
                    Debug.LogWarning("[SceneSetupTool] TMP_PackageUtilities not found — TMP essentials may need manual import via Window > TextMeshPro > Import TMP Essential Resources");
                }
            }

            RunCoreSetup();

            var scene = SceneManager.GetActiveScene();
            string scenePath = "Assets/Scenes/GameScene.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[SceneSetupTool] Batch setup complete. Scene saved to " + scenePath);
        }

        /// <summary>Shared scene setup: sprites, camera, environment, gameplay, UI, wiring.</summary>
        private static void RunCoreSetup()
        {
            PreGenerateSprites();

            ClearScene();
            SetupTags();
            SetupSortingLayers();
            SetupLayer(8, "Rocket");

            SetupCamera();

            var managers  = CreateEmpty("--- MANAGERS ---");
            var envParent = CreateEmpty("--- ENVIRONMENT ---");
            var gameplay  = CreateEmpty("--- GAMEPLAY ---");
            var inputSep  = CreateEmpty("--- INPUT ---");
            var uiSep     = CreateEmpty("--- UI ---");

            CreateEmpty("GameManager", managers);
            SetupAudio(managers);
            CreateEnvironment(envParent);
            CreateGameplay(gameplay);
            CreateCanvas(uiSep);
            WireRoundManager(inputSep);
            WireLaunchController(inputSep);
            WireRoundManagerHUD(inputSep);
            WireCameraController();

            var eventSystem = CreateEmpty("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // -- Audio --

        private static void SetupAudio(GameObject parent)
        {
            var go = CreateEmpty("AudioManager", parent);
            var am = go.AddComponent<AudioManager>();

            var so = new SerializedObject(am);
            var launchClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/rocket-start.mp3");
            var thrustClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/rocket-flight.mp3");
            var boomClip   = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/rocket-boom.mp3");

            if (launchClip != null) so.FindProperty("_launchClip").objectReferenceValue = launchClip;
            else Debug.LogWarning("[SceneSetupTool] Audio clip not found: Assets/Audio/rocket-start.mp3");
            if (thrustClip != null) so.FindProperty("_thrustClip").objectReferenceValue = thrustClip;
            else Debug.LogWarning("[SceneSetupTool] Audio clip not found: Assets/Audio/rocket-flight.mp3");
            if (boomClip != null)   so.FindProperty("_boomClip").objectReferenceValue = boomClip;
            else Debug.LogWarning("[SceneSetupTool] Audio clip not found: Assets/Audio/rocket-boom.mp3");
            so.ApplyModifiedProperties();

            Undo.RegisterCreatedObjectUndo(go, "Create AudioManager");
            Debug.Log("[SceneSetupTool] AudioManager created + audio clips wired.");
        }

        // -- RoundManager wiring --

        private static void WireRoundManager(GameObject parent)
        {
            var go = CreateEmpty("RoundManager", parent);
            var rm = go.AddComponent<RoundManager>();

            var os = go.AddComponent<ObstacleSpawner>();

            var rocket = GameObject.Find("Rocket");
            var vehicle = GameObject.Find("LauncherVehicle");
            var spawnPoint = vehicle?.transform.Find("RocketSpawnPoint");

            var so = new SerializedObject(rm);
            if (rocket != null)
                so.FindProperty("_rocket").objectReferenceValue = rocket.GetComponent<Rocket>();
            if (spawnPoint != null)
                so.FindProperty("_spawnPoint").objectReferenceValue = spawnPoint;

            var camGo = GameObject.Find("Main Camera");
            if (camGo != null)
                so.FindProperty("_cameraController").objectReferenceValue = camGo.GetComponent<CameraController>();

            var target = GameObject.Find("Target");
            if (target != null)
                so.FindProperty("_targetTransform").objectReferenceValue = target.transform;

            so.FindProperty("_obstacleSpawner").objectReferenceValue = os;
            // LaunchController wired after it's created
            so.ApplyModifiedProperties();

            // Wire ObstacleSpawner references
            var osSo = new SerializedObject(os);
            if (spawnPoint != null)
                osSo.FindProperty("_spawnPoint").objectReferenceValue = spawnPoint;
            if (target != null)
                osSo.FindProperty("_targetTransform").objectReferenceValue = target.transform;
            osSo.ApplyModifiedProperties();

            Debug.Log("[SceneSetupTool] RoundManager + ObstacleSpawner wired.");
        }

        // -- LaunchController wiring (input only) --

        private static void WireLaunchController(GameObject parent)
        {
            var go = CreateEmpty("LaunchController", parent);
            var lc = go.AddComponent<LaunchController>();

            var rocket = GameObject.Find("Rocket");
            var aimArrow = GameObject.Find("AimArrow");
            var vehicle = GameObject.Find("LauncherVehicle");
            var spawnPoint = vehicle?.transform.Find("RocketSpawnPoint");

            var so = new SerializedObject(lc);
            if (rocket != null)
                so.FindProperty("_rocket").objectReferenceValue = rocket.GetComponent<Rocket>();
            if (aimArrow != null)
                so.FindProperty("_aimArrow").objectReferenceValue = aimArrow.GetComponent<AimArrow>();
            if (spawnPoint != null)
                so.FindProperty("_spawnPoint").objectReferenceValue = spawnPoint;
            if (vehicle != null)
                so.FindProperty("_vehicleCollider").objectReferenceValue = vehicle.GetComponent<Collider2D>();

            // Wire RoundManager reference
            var rmGo = GameObject.Find("RoundManager");
            if (rmGo != null)
                so.FindProperty("_roundManager").objectReferenceValue = rmGo.GetComponent<RoundManager>();

            so.ApplyModifiedProperties();

            // Now wire LaunchController back into RoundManager
            if (rmGo != null)
            {
                var rmSo = new SerializedObject(rmGo.GetComponent<RoundManager>());
                rmSo.FindProperty("_launchController").objectReferenceValue = lc;
                rmSo.ApplyModifiedProperties();
            }

            Debug.Log("[SceneSetupTool] LaunchController wired (input only).");
        }

        // -- RoundManagerHUD wiring --

        private static void WireRoundManagerHUD(GameObject parent)
        {
            var go = CreateEmpty("RoundManagerHUD", parent);
            var hud = go.AddComponent<RoundManagerHUD>();

            var so = new SerializedObject(hud);

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

                var statsTransform = canvas.transform.Find("StatsText");
                if (statsTransform != null)
                    so.FindProperty("_statsText").objectReferenceValue = statsTransform.GetComponent<TMPro.TextMeshProUGUI>();
            }

            var rmGo = GameObject.Find("RoundManager");
            if (rmGo != null)
                so.FindProperty("_roundManager").objectReferenceValue = rmGo.GetComponent<RoundManager>();

            so.ApplyModifiedProperties();

            Debug.Log("[SceneSetupTool] RoundManagerHUD wired.");
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

            Debug.Log("[SceneSetupTool] CameraController wired.");
        }

        // -- Scene clearing --

        private static void ClearScene()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                Undo.DestroyObjectImmediate(go);
        }

        // -- Project settings --

        private static void SetupTags()
        {
            EnsureTag(GameConstants.TagGround);
            EnsureTag(GameConstants.TagTarget);
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

        private static readonly Dictionary<string, int> SortingLayerIDs = new()
        {
            { "Background",  100 },
            { "Environment", 200 },
            { "Gameplay",    300 },
            { "Projectile",  400 },
        };

        private static void SetupSortingLayers()
        {
            var tm     = GetTagManager();
            // Note: m_SortingLayers is an internal Unity API — may change in future Unity versions
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
                entry.FindPropertyRelative("uniqueID").intValue = SortingLayerIDs[name];
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

        // -- Export current scene positions --

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

                foreach (Transform child in t)
                    sb.AppendLine($"  {child.name}: localPos=({child.localPosition.x:F2}, {child.localPosition.y:F2}, {child.localPosition.z:F2})  scale=({child.localScale.x:F2}, {child.localScale.y:F2}, {child.localScale.z:F2})");
            }
            sb.AppendLine("\n=== END ===");
            Debug.Log(sb.ToString());
        }

        // -- Camera --

        private static void SetupCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
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
}
