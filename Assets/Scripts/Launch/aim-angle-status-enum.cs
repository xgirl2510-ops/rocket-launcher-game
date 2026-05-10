namespace RocketLauncher
{
    /// <summary>
    /// Status of the current aim angle relative to allowed launch range.
    /// Used by AimArrow to color-code feedback (green/yellow/red).
    /// </summary>
    public enum AimAngleStatus
    {
        /// <summary>Within valid range and away from the limits.</summary>
        Valid,
        /// <summary>Within valid range but close to the min/max limit.</summary>
        NearLimit,
        /// <summary>Outside valid range — clamped back to the nearest limit.</summary>
        Clamped
    }
}
