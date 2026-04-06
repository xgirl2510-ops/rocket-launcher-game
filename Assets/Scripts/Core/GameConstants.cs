namespace RocketLauncher
{
    /// <summary>Shared game constants — single source of truth for layout values and tags.</summary>
    public static class GameConstants
    {
        /// <summary>World Y position of the ground surface.</summary>
        public const float GroundTop = -5f;
        /// <summary>Tag applied to the ground GameObject.</summary>
        public const string TagGround = "Ground";
        /// <summary>Tag applied to the target GameObject.</summary>
        public const string TagTarget = "Target";

        /// <summary>Minimum launch force (slingshot fully relaxed).</summary>
        public const float MinLaunchForce = 5f;
        /// <summary>Maximum launch force (slingshot fully stretched).</summary>
        public const float MaxLaunchForce = 30f;

        /// <summary>Height threshold below which a crater is spawned on ground hit.</summary>
        public const float CraterSpawnHeightThreshold = 1.5f;

        /// <summary>Name of the ground GameObject for editor/fallback lookup.</summary>
        public const string GroundObjectName = "Ground";
        /// <summary>Unity layer index for Default (always 0).</summary>
        public const int DefaultLayer = 0;
        /// <summary>Sorting layer name for gameplay objects (obstacles, target).</summary>
        public const string SortingLayerGameplay = "Gameplay";
        /// <summary>Unity layer index for the Rocket (user layer 8).</summary>
        public const int RocketLayer = 8;

        /// <summary>Angle offset to align sprites (which point UP) to velocity direction.</summary>
        public const float SpriteAngleOffset = -90f;
    }
}
