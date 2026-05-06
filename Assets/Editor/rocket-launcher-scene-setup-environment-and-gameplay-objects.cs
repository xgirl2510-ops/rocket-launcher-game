using UnityEngine;
using UnityEditor;
using RocketLauncher;

namespace RocketLauncher.Editor
{
    /// <summary>
    /// Partial class: creates Ground, Target, LauncherVehicle, Rocket, and AimArrow GameObjects.
    /// Positions adjusted for camera view: ortho=9 at y=2 -> visible y range [-7, 11].
    /// Ground sits at bottom edge, vehicle sits on top of ground.
    /// </summary>
    public partial class SceneSetupTool
    {
        // -- Layout formulas: everything derives from Camera -> Ground -> objects on ground --

        // Step 1: Camera defines visible area
        private const float CamY         = 2f;
        private const float CamOrthoSize = 9f;
        private static readonly float CamTop    = CamY + CamOrthoSize;

        // Step 1b: Camera defines visible X range (iPhone 15 Pro Max aspect 19.5:9)
        private const float TargetAspect   = 9f / 19.5f;
        private static readonly float CamHalfWidth = CamOrthoSize * TargetAspect;
        private static readonly float CamLeft      = -CamHalfWidth;

        // Step 2: Ground
        private const float GroundVisibleHeight = 2f;
        private const float GroundTop = GameConstants.GroundTop;
        private const float GroundSpriteHeight = 50f;
        private static readonly float GroundCenterY = GroundTop - GroundSpriteHeight / 2f;

        // Step 2b: Target position
        private static readonly float TargetX = CamHalfWidth * 4f;

        // Step 2c: Ground width
        private const float GroundWidth   = 500f;
        private const float GroundCenterX = 0f;

        // Sprite asset paths for PNG sprites
        private const string RocketSpritePath = "Assets/Sprites/Generated/rocket2.png";
        private const string LauncherSpritePath = "Assets/Sprites/Generated/car2.png";
        private const string GroundSpritePath = "Assets/Sprites/Generated/ground.png";

        // Step 3: Vehicle sits ON ground — car2.png (1462x780 @ PPU 100)
        private const float VehicleVisualScale = 0.24f;
        private static readonly float VehicleY = GroundTop + 780f / 100f * VehicleVisualScale / 2f;
        private const float VehicleOffsetFromLeft = 2.5f;
        private static readonly float VehicleX   = CamLeft + VehicleOffsetFromLeft;

        // Step 4: Rocket spawn = original Y position preserved
        private const float RocketVisualScale = 0.24f;
        private const float SpawnOffsetY = 0.8f;
        private static readonly Vector3 RocketSpawnWorld = new Vector3(VehicleX, VehicleY + SpawnOffsetY, 0f);

        // Step 5: Target floats in the sky
        private const float TargetScaleY = 1.5f;
        private static readonly float TargetY = (GroundTop + CamTop) / 2f;

        private static void CreateEnvironment(GameObject parent)
        {
            // ground.png 25000x2048 @ PPU100 = 250 x 20.48 world units at scale 1
            var ground = CreateEmpty("Ground", parent);
            float groundNaturalH = 2048f / 100f; // 20.48
            ground.transform.position = new Vector3(GroundCenterX, GroundTop - groundNaturalH / 2f, 0f);
            ground.transform.localScale = Vector3.one;
            ground.tag = GameConstants.TagGround;
            var groundCol = ground.AddComponent<BoxCollider2D>();
            groundCol.size = new Vector2(GroundWidth, groundNaturalH); // 500 wide collider
            var groundSr = ground.AddComponent<SpriteRenderer>();
            groundSr.sprite = LoadSpriteFromPng(GroundSpritePath, 16384);
            groundSr.sortingLayerName = "Environment";

            var target = CreateSprite("Target", parent,
                new Vector3(TargetX, TargetY, 0f), new Vector3(1.5f, TargetScaleY, 1f),
                Hex("#FF0000"), "Gameplay");
            target.tag = GameConstants.TagTarget;
            target.AddComponent<BoxCollider2D>().isTrigger = true;
        }

        private static void CreateGameplay(GameObject parent)
        {
            CreateLauncherVehicle(parent);
            CreateRocket(parent);
            CreateAimArrow(parent);
        }

        private static void CreateLauncherVehicle(GameObject parent)
        {
            var vehicle = CreateEmpty("LauncherVehicle", parent);
            vehicle.transform.position = new Vector3(VehicleX, VehicleY, 0f);

            var col = vehicle.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(3f, 1.2f);

            // Single sprite using car2.png
            var visual = CreateEmpty("Visual", vehicle);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSpriteFromPng(LauncherSpritePath);
            sr.sortingLayerName = "Gameplay";
            visual.transform.localScale = new Vector3(VehicleVisualScale, VehicleVisualScale, 1f);

            CreateEmpty("RocketSpawnPoint", vehicle).transform.localPosition =
                new Vector3(0f, SpawnOffsetY, 0f);
        }

        private static void CreateRocket(GameObject parent)
        {
            var go = CreateEmpty("Rocket", parent);
            go.transform.position = RocketSpawnWorld;
            go.tag = "Player";
            go.layer = GameConstants.RocketLayer;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 1;
            rb.mass = 1;
            rb.linearDamping = 0;
            rb.angularDamping = 0;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.15f;
            col.offset = new Vector2(0f, 0.5f);

            var rocketComp = go.AddComponent<Rocket>();
            go.AddComponent<RocketTrail>();
            go.AddComponent<ImpactEffectsHandler>();

            // Wire ImpactEffectsHandler._rocket and _ground via SerializedObject
            var impactHandler = go.GetComponent<ImpactEffectsHandler>();
            var ihSo = new SerializedObject(impactHandler);
            ihSo.FindProperty("_rocket").objectReferenceValue = rocketComp;
            var groundGo = GameObject.Find("Ground");
            if (groundGo != null)
                ihSo.FindProperty("_ground").objectReferenceValue = groundGo.transform;
            ihSo.ApplyModifiedProperties();

            // Single sprite using rocket.png
            var visual = CreateEmpty("Visual", go);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSpriteFromPng(RocketSpritePath);
            sr.sortingLayerName = "Projectile";
            visual.transform.localScale = new Vector3(RocketVisualScale, RocketVisualScale, 1f);
        }

        private static void CreateAimArrow(GameObject parent)
        {
            var go = CreateSprite("AimArrow", parent,
                RocketSpawnWorld, new Vector3(0.15f, 1f, 1f),
                new Color(1f, 1f, 1f, 0.7f), "Projectile");
            go.GetComponent<SpriteRenderer>().enabled = false;
            go.AddComponent<AimArrow>();
        }
    }
}
