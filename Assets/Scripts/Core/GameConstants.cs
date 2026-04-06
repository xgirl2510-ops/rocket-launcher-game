namespace RocketLauncher
{
    /// <summary>Shared game constants — single source of truth for layout values and tags.</summary>
    public static class GameConstants
    {
        public const float GroundTop = -5f;
        public const string TagGround = "Ground";
        public const string TagTarget = "Target";

        // Launch force range — single source of truth, LaunchController reads these directly
        public const float MinLaunchForce = 5f;
        public const float MaxLaunchForce = 30f;

        // Height threshold below which a crater/scorch mark is spawned on ground hit
        public const float CraterSpawnHeightThreshold = 1.5f;

        public const string GroundObjectName = "Ground";
        public const int DefaultLayer = 0;
        public const string SortingLayerGameplay = "Gameplay";
        public const int RocketLayer = 8;
    }
}
