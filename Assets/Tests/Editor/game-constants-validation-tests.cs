using NUnit.Framework;

namespace RocketLauncher.Tests
{
    /// <summary>
    /// Validates GameConstants invariants — catches SSOT drift if someone
    /// changes values to invalid ranges that would break physics or gameplay.
    /// </summary>
    public class GameConstantsValidationTests
    {
        [Test]
        public void MinLaunchForce_LessThan_MaxLaunchForce()
        {
            Assert.Less(GameConstants.MinLaunchForce, GameConstants.MaxLaunchForce);
        }

        [Test]
        public void LaunchForces_ArePositive()
        {
            Assert.Greater(GameConstants.MinLaunchForce, 0f);
            Assert.Greater(GameConstants.MaxLaunchForce, 0f);
        }

        [Test]
        public void CraterSpawnHeightThreshold_IsPositive()
        {
            Assert.Greater(GameConstants.CraterSpawnHeightThreshold, 0f);
        }

        [Test]
        public void Tags_AreNonEmpty()
        {
            Assert.IsNotEmpty(GameConstants.TagGround);
            Assert.IsNotEmpty(GameConstants.TagTarget);
        }

        [Test]
        public void Tags_AreDistinct()
        {
            Assert.AreNotEqual(GameConstants.TagGround, GameConstants.TagTarget);
        }

        [Test]
        public void GroundTop_IsBelowZero()
        {
            Assert.Less(GameConstants.GroundTop, 0f,
                "GroundTop must be below camera center (Y=0) for correct scene layout.");
        }
    }
}
