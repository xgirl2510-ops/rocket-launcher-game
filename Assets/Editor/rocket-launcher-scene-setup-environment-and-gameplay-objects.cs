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
        private const string BackgroundSpritePath = "Assets/Sprites/Generated/bg.jpg";
        // target.png 2103×479 @ PPU 100 → world width 21.03; scale 0.27 → 5.68 unit (~60% larger than jets).
        private const string TargetSpritePath = "Assets/Sprites/Generated/target.png";
        private const float TargetVisualScale = 0.27f;

        // Step 3: Vehicle sits ON ground — car2.png (1462x780 @ PPU 100)
        private const float VehicleVisualScale = 0.24f;
        // Vehicle is fixed at a specific world position that visually aligns with the desert
        // strip drawn into bg.jpg (since the background is now a static world sprite).
        // Y just above GroundTop so the car sits on terrain rather than floating in the sky.
        private const float VehicleY = -4.8f;
        private const float VehicleX = -7f;

        // Step 4: Rocket spawn = original Y position preserved
        private const float RocketVisualScale = 0.24f;
        private const float SpawnOffsetY = 0.8f;
        private static readonly Vector3 RocketSpawnWorld = new Vector3(VehicleX, VehicleY + SpawnOffsetY, 0f);

        // Step 5: Target floats in the sky
        private const float TargetScaleY = 1.5f;
        private static readonly float TargetY = (GroundTop + CamTop) / 2f;

        private static void CreateEnvironment(GameObject parent)
        {
            // Static world-space background at NATIVE sprite size (no scaling, no warping).
            // bg.jpg is 4096×1936 @ PPU 100 → world size 40.96 × 19.36.
            //   bgCentreY = bgBottomY + bgHeight/2 = -5.74 + 9.68 = 3.94
            //   bgLeftX = carLeftX - 2 × carWidth = (-7 - 1.755) - 2×3.51 = -15.775
            //   bgCentreX = bgLeftX + bgWidth/2 = -15.775 + 20.48 = 4.71
            // Component-side scaling is disabled (_targetWorldHeight = 0).
            var bg = CreateEmpty("Background", parent);
            bg.transform.position = new Vector3(4.71f, 3.94f, 50f);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = LoadSpriteFromPng(BackgroundSpritePath, 4096);
            if (bgSr.sprite != null)
            {
                bg.AddComponent<CameraFitHeightBackground>();
            }
            else
            {
                Debug.LogWarning("[SceneSetupTool] bg.jpg not loaded — Background object created but empty.");
            }

            // Ground = INVISIBLE physics collider only. No sprite renderer here — the visible
            // dirt area below the painted BG is rendered by a separate GroundVisual object.
            // Collider anchored at GroundTop so rocket OnCollisionEnter2D ground check fires
            // when the rocket lands.
            var groundPhysics = CreateEmpty("Ground", parent);
            const float groundColliderHeight = 20f;
            groundPhysics.transform.position = new Vector3(GroundCenterX, GroundTop - groundColliderHeight / 2f, 0f);
            groundPhysics.transform.localScale = Vector3.one;
            groundPhysics.tag = GameConstants.TagGround;
            var groundCol = groundPhysics.AddComponent<BoxCollider2D>();
            groundCol.size = new Vector2(GroundWidth, groundColliderHeight);

            // GroundVisual: static world-space dirt sprite. Top edge sits at the car's bottom
            // edge (VehicleY - half car sprite height = -4.8 - 0.94 = -5.74), so the car sits
            // ON top of the dirt strip. BG's bottom edge sits flush with this.
            // Sprite is 25000×2048 px @ PPU 100 = 250×20.48 world units.
            const float groundTopY = -5.74f;
            const float groundNaturalH = 2048f / 100f;
            var groundVisual = CreateEmpty("GroundVisual", parent);
            groundVisual.transform.position = new Vector3(GroundCenterX, groundTopY - groundNaturalH / 2f, 0f);
            groundVisual.transform.localScale = Vector3.one;
            var groundVisualSr = groundVisual.AddComponent<SpriteRenderer>();
            groundVisualSr.sprite = LoadSpriteFromPng(GroundSpritePath, 16384);
            groundVisualSr.sortingLayerName = "Environment";
            groundVisualSr.sortingOrder = 0;

            var target = CreateEmpty("Target", parent);
            target.transform.position = new Vector3(TargetX, TargetY, 0f);
            target.transform.localScale = new Vector3(TargetVisualScale, TargetVisualScale, 1f);
            target.tag = GameConstants.TagTarget;

            var targetSr = target.AddComponent<SpriteRenderer>();
            targetSr.sprite = LoadSpriteFromPng(TargetSpritePath);
            targetSr.sortingLayerName = "Gameplay";

            // PolygonCollider2D from sprite alpha so the rocket only "hits" where the bomber is
            // visible. Trigger so OnTriggerEnter2D fires the win flow without bouncing.
            if (targetSr.sprite != null)
            {
                var poly = target.AddComponent<PolygonCollider2D>();
                poly.isTrigger = true;
            }
            else
            {
                var box = target.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                Debug.LogError("[SceneSetupTool] target.png failed to load — using box collider fallback.");
            }

            // Exhaust trail for visual life. Bobbing animation INTENTIONALLY OMITTED — the target
            // must stay at its randomized position so the analytical trajectory solver hits it.
            // A bobbing target would shift after RespawnObstacles computes the safe arc, causing
            // auto-play to miss by the bob amplitude.
            var targetTrail = target.AddComponent<JetExhaustTrail>();
            // Distinctive cyan/violet exhaust + much longer trail so the target reads as the "boss"
            // aircraft (the prize) rather than another protector jet. Force nozzleXFraction=+1
            // (RIGHT edge = tail) explicitly so any stale scene-serialized value is overwritten.
            targetTrail.Configure(
                trailLengthScale: 2.2f,
                hueShiftDegrees: 0f,
                coreTint: new Color(0.6f, 0.9f, 1f, 1f),    // pale cyan core
                flameTint: new Color(0.5f, 0.4f, 1f, 1f),   // violet flame
                nozzleXFraction: 1f,
                emissionMultiplier: 1.6f);                  // denser, punchier flame than protector jets
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
            // Tag so Rocket can detect friendly-fire collision and trigger game-over.
            vehicle.tag = GameConstants.TagLauncherVehicle;

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
                RocketSpawnWorld, new Vector3(0.06f, 1f, 1f),  // thinner line
                new Color(1f, 1f, 1f, 0.7f), "Projectile");
            var sr = go.GetComponent<SpriteRenderer>();
            sr.enabled = false;
            sr.sortingOrder = -1;  // behind the rocket sprite (which sits at default order 0)
            go.AddComponent<AimArrow>();
        }
    }
}
