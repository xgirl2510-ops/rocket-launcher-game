namespace RocketLauncher
{
    /// <summary>Shared game constants — single source of truth for layout values and tags.</summary>
    public static class GameConstants
    {
        public const float GroundTop = -5f;
        public const string TagGround = "Ground";
        public const string TagTarget = "Target";

        // Launch force range — must match LaunchController serialized defaults
        public const float MinLaunchForce = 5f;
        public const float MaxLaunchForce = 30f;
    }
}
